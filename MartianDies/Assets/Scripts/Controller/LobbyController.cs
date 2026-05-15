using AniDrag.EventBus;
using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;



public class LobbyController : MonoBehaviour
{
    [SerializeField] private LobbyView view;
    [SerializeField] private HostRoomView hostRoomView;
    [SerializeField] private CreateRoomView createRoomView;
    [SerializeField] private WaitingForHostView waitingForHostView;

   

    private bool pendingRoomCreation = false;
    private string pendingRoomName = null;
    private RoomDataModel currentRoom;    // room the player is in (host or joined)
    private bool gameStarted = false;
    private Dictionary<string, RoomDataModel> rooms = new Dictionary<string, RoomDataModel>();


    #region Timeouts 
    private const string CREATE_ROOM_TIMEOUT = "create_room";
    private const string JOIN_ROOM_TIMEOUT = "join_room";
    private const string REFRESH_ROOMS_TIMEOUT = "refresh_rooms";
    #endregion
    #region OCT Strings
    //OSC message Identifiers
    private const string DISCONECT = "/disconect";
    private const string REFRESH_ROOMS = "/refresh_rooms";
    private const string CREATE_ROOM = "/crate_room";
    private const string JOIN_ROOM = "/join_room";
    private const string START_GAME = "/start_game";
    private const string CLOSE_ROOM = "/close_room";
    private const string LEAVE_ROOM = "/leave_room";

    //OSC message Identifiers Subscriptions Replies
    private const string S_DISCONECT = "/disconected";
    private const string S_REFRESH_ROOMS = "/update_room_list";
    private const string S_CREATE_ROOM = "/room_created";
    private const string S_JOIN_ROOM = "/room_joined";
    private const string S_START_GAME = "/game_started";
    private const string S_CLOSE_ROOM = "/room_closed";
    private const string S_LEAVE_ROOM = "/room_left";

    //OSC other server message subscriptions
    private const string S_RECIVE_ROOM_LIST = "/room_list";
    private const string S_DISCONET_FROM_ROOM = "/host_closed_room";

    #endregion


    #region Event Bindings
    EventBinding<JoinRoom> joinRoomBinding;
    private EventBinding<StartGame> startGameBinding;
    private EventBinding<CloseHostedRoom> closeHostedRoomBinding;
    private EventBinding<LeaveRoom> leaveRoomBinding;
    private EventBinding<CreateRoom> createRoomBinding;
    #endregion

    private void Start()//---
    {
        if (Client.Instance == null)
        {
            Debug.LogError("Client instance missing!");
            return;
        }
        if (view == null) view = FindFirstObjectByType<LobbyView>();
        if (hostRoomView == null) hostRoomView = FindFirstObjectByType<HostRoomView>();
        if (createRoomView == null) createRoomView = FindFirstObjectByType<CreateRoomView>();
        if (waitingForHostView == null) waitingForHostView = FindFirstObjectByType<WaitingForHostView>();

        if (!NullChecks()) return;

        view.SetPlayerName(Client.Instance.Username);

        // Subscribe to OSC messages
        //Client.Instance.AddListener("/room_update", OnRoomUpdate, OSCUtil.STRING, OSCUtil.INT, OSCUtil.INT, OSCUtil.STRING, OSCUtil.INT, OSCUtil.BOOL);
        OSCListenersSetup();

        Client.Instance.OnDisconnected += OnDisconnected;

        // Subscribe to UI events via EventBus
        BindingsSetup();

        //RefreshRoomList();   // CONNECT
        Client.Log("Lobby scene loaded.");
    }

    void OSCListenersSetup()
    {
        Client.Instance.AddListener("/game_started", OnGameStarted);
        Client.Instance.AddListener("/error", OnError, OSCUtil.STRING);
        Client.Instance.AddListener("/room_list", OnRoomList);
        Client.Instance.AddListener("/room_list_update", OnRoomListUpdate);
    }
    void BindingsSetup()
    {
        startGameBinding = new EventBinding<StartGame>(OnStartGame);
        closeHostedRoomBinding = new EventBinding<CloseHostedRoom>(OnCloseRoom);
        leaveRoomBinding = new EventBinding<LeaveRoom>(OnLeaveRoom);
        createRoomBinding = new EventBinding<CreateRoom>(OnCrateRoom);
        EventBus<CreateRoom>.Subscribe(createRoomBinding);
        EventBus<StartGame>.Subscribe(startGameBinding);
        EventBus<CloseHostedRoom>.Subscribe(closeHostedRoomBinding);
        EventBus<LeaveRoom>.Subscribe(leaveRoomBinding);
    }

    bool NullChecks()
    {
        if (Client.Instance == null)
        {
            Debug.LogError("Client instance missing!");
            return false;
        }

        if (view == null)
        {
            Debug.LogError("LobbyView not assigned!");
            return false;
        }
        if (hostRoomView == null)
        {
            Debug.LogError("host Room View not assigned!");
            return false;
        }
        if (createRoomView == null)
        {
            Debug.LogError("create Room View not assigned!");
            return false;
        }
        if (waitingForHostView == null)
        {
            Debug.LogError("waiting For Host View not assigned!");
            return false;
        }
        return true;
    }

    void SetUsername() => view.SetPlayerName(Client.Instance.Username);


    #region Semd OSC messages
    void RefreshRoomList(RefreshRooms e) => Client.Instance.Send(new OSCMessageOut(REFRESH_ROOMS));

    private void OnJoinRoom(JoinRoom e)
    {
        if (string.IsNullOrEmpty(e.data.roomName)) { Client.Log("Join room failed: invalid room name."); return; }
        pendingRoomName = e.data.roomName;
        EventBus<DisableButtons>.Publish(new DisableButtons(false));
        
        //StartTimeout
        Client.Instance.StartTimeout(JOIN_ROOM_TIMEOUT, 5f, () =>
        {
            Client.Log("Join room timeout.");
            EventBus<DisableButtons>.Publish(new DisableButtons(true));
            pendingRoomName = null;
        });

        //Send message
        var msg = new OSCMessageOut(JOIN_ROOM);
        msg.AddString(e.data.roomName);
        Client.Instance.Send(msg);
    }
    private void OnLeaveRoom(LeaveRoom e)
    {
        Client.Instance.Send(new OSCMessageOut(LEAVE_ROOM));
        currentRoom = null;
        EventBus<DisableButtons>.Publish(new DisableButtons(true));
        waitingForHostView.gameObject.SetActive(false);
    }

    private void OnCrateRoom(CreateRoom e)
    {
        if (pendingRoomCreation) return;
        string roomName = e.roomName.Trim();
        int pointGoal = e.pointGoal;
        if (string.IsNullOrEmpty(roomName))
        {
            Client.Log("Create room failed: empty name.");
            return;
        }
        if (pointGoal < 10 || pointGoal > 80)
        {
            Client.Log("Create room failed: Point Goal must be between 10 and 80.");
            return;
        }
        createRoomView.gameObject.SetActive(true);
        pendingRoomName = roomName;
        EventBus<DisableButtons>.Publish(new DisableButtons(false));

        Client.Instance.StartTimeout(CREATE_ROOM_TIMEOUT, 8f, () =>
        {
            Client.Log("Create room timeout.");
            EventBus<DisableButtons>.Publish(new DisableButtons(true));
            createRoomView.gameObject.SetActive(false);
            pendingRoomName = null;
            pendingRoomCreation = false;
        });

        var msg = new OSCMessageOut(CREATE_ROOM);
        msg.AddString(roomName).AddInt(pointGoal);
        Client.Instance.Send(msg);
    }
    private void OnCloseRoom(CloseHostedRoom e)
    {
        Client.Instance.Send(new OSCMessageOut(CLOSE_ROOM));
        currentRoom = null;
        EventBus<DisableButtons>.Publish(new DisableButtons(true));
        hostRoomView.gameObject.SetActive(false);
    }

    private void OnStartGame() => Client.Instance.Send(new OSCMessageOut(START_GAME));
    #endregion
    


    #region OSC Msg recivers
    private void OnGameStarted(OSCMessageIn msg, IPEndPoint sender)
    {
        if (pendingRoomName != null)
        {
            Client.Log("Game started – loading game scene.");
            SceneManager.LoadScene("2_Sc_Game"); // change to your game scene name
            return;
        }

    }
    private void OnError(OSCMessageIn msg, IPEndPoint sender)
    {
        string error = msg.ReadString();
        Client.Log("Lobby Error", error);
    }
    private void OnDisconnected(string reason)
    {
        Client.Log("Disconnected from server in lobby: " + reason);
        Client.Instance.CancelTimeout(CREATE_ROOM_TIMEOUT);
        Client.Instance.CancelTimeout(JOIN_ROOM_TIMEOUT);
        SceneManager.LoadScene("0_SC_MainMenu");
    }
    public void OnCreateRoomSuccess(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Room Creation sucessfull");

        Client.Instance.CancelTimeout(CREATE_ROOM_TIMEOUT);
        bool succes = msg.ReadBool();

        if(!succes)
        {
            Client.Log($"Server: Failed to create room. Error: {msg.ReadString()}");
            return;
        }
        RoomDataModel entry = new RoomDataModel(
            msg.ReadString(),
            msg.ReadString(),
            msg.ReadInt(),
            msg.ReadInt(),
            msg.ReadBool()
            );
        currentRoom = entry;
        EventBus<RoomCreated>.Publish(new RoomCreated(succes, entry));
        view.Panel_HostRoom.gameObject.SetActive(true);
        view.Panel_CreateRoom.gameObject.SetActive(false);
    }

    private void OnJoinSucces(OSCMessageIn msg, IPEndPoint sender)
    {

    }
    private void OnStartGameSucces(OSCMessageIn msg, IPEndPoint sender)
    {

    }
    private void OnRoomList(OSCMessageIn msg, IPEndPoint sender)//---
    {
        int roomCount = msg.ReadInt();
        rooms.Clear();
        for (int i = 0; i < roomCount; i++)
        {
            string name = msg.ReadString();
            int goal = msg.ReadInt();
            string host = msg.ReadString();
            int playerCount = msg.ReadInt();
            int state = msg.ReadInt();
            var room = new RoomDataModel(name, host, goal, playerCount);
            rooms[name] = room;
        }
        view.PopulateRoomList(rooms.Values.ToList());
    }
    private void OnRoomListUpdate(OSCMessageIn msg, IPEndPoint sender)
    {
        string operation = msg.ReadString(); // "add", "update", "remove"
        string jsonRoom = msg.ReadString();   // JSON of RoomEntryData
        var room = JsonUtility.FromJson<RoomDataModel>(jsonRoom);

        switch (operation)
        {
            case "add":
            case "update":
                rooms[room.roomName] = room;
                break;
            case "remove":
                rooms.Remove(room.roomName);
                break;
        }
        view.PopulateRoomList(rooms.Values.ToList());
    }

    #endregion

    
    // ---------------------- Cleanup ----------------------
    private void OnDestroy()
    {
        
        if (Client.Instance != null)
        {
            Client.Instance.RemoveListener("/game_started", OnGameStarted);
            Client.Instance.RemoveListener("/error", OnError);
            Client.Instance.RemoveListener("/room_list", OnRoomList);
            Client.Instance.RemoveListener("/room_list_update", OnRoomListUpdate);
            Client.Instance.OnDisconnected -= OnDisconnected;
        }

        EventBus<StartGame>.Unsubscribe(startGameBinding);
        EventBus<CloseHostedRoom>.Unsubscribe(closeHostedRoomBinding);
        EventBus<LeaveRoom>.Unsubscribe(leaveRoomBinding);
    }
}
