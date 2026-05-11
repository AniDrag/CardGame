using AniDrag.EventBus;
using OSCTools;
using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;



public class LobbyController : MonoBehaviour
{
    [SerializeField] private LobbyView view;
    [SerializeField] private HostRoomView hostRoomView;
    [SerializeField] private CreateRoomView createRoomView;
    [SerializeField] private WaitingForHostView waitingForHostView;

    private const string CREATE_ROOM_TIMEOUT = "create_room";
    private const string JOIN_ROOM_TIMEOUT = "join_room";
    private const string REFRESH_ROOMS_TIMEOUT = "refresh_rooms";

    private string pendingRoomName = null;
    private RoomEntryData currentRoom;    // room the player is in (host or joined)
    private bool gameStarted = false;
    private Dictionary<string, RoomEntryData> rooms = new Dictionary<string, RoomEntryData>();

    private EventBinding<StartGame> startGameBinding;
    private EventBinding<CloseHostedRoom> closeHostedRoomBinding;
    private EventBinding<LeaveRoom> leaveRoomBinding;


    private void Start()//---
    {
        if (Client.Instance == null)
        {
            Debug.LogError("Client instance missing!");
            return;
        }

        if (view == null) view = FindFirstObjectByType<LobbyView>();
        if (view == null)
        {
            Debug.LogError("LobbyView not assigned!");
            return;
        }
        if (hostRoomView == null) hostRoomView = FindFirstObjectByType<HostRoomView>();
        if (hostRoomView == null)
        {
            Debug.LogError("host Room View not assigned!");
            return;
        }
        if (createRoomView == null) createRoomView = FindFirstObjectByType<CreateRoomView>();
        if (createRoomView == null)
        {
            Debug.LogError("create Room View not assigned!");
            return;
        }
        if (waitingForHostView == null) waitingForHostView = FindFirstObjectByType<WaitingForHostView>();
        if (waitingForHostView == null)
        {
            Debug.LogError("waiting For Host View not assigned!");
            return;
        }


        view.SetPlayerName(Client.Instance.Username);

        // Subscribe to OSC messages
        Client.Instance.AddListener("/room_update", OnRoomUpdate, OSCUtil.STRING, OSCUtil.INT, OSCUtil.INT, OSCUtil.STRING, OSCUtil.INT, OSCUtil.BOOL);
        Client.Instance.AddListener("/game_started", OnGameStarted);
        Client.Instance.AddListener("/error", OnError, OSCUtil.STRING);
        Client.Instance.AddListener("/room_list", OnRoomList);
        Client.Instance.AddListener("/room_list_update", OnRoomListUpdate);
        Client.Instance.OnDisconnected += OnDisconnected;

        // Subscribe to UI events via EventBus
        startGameBinding = new EventBinding<StartGame>(HandleStartGame);
        closeHostedRoomBinding = new EventBinding<CloseHostedRoom>(HandleCloseRoom);
        leaveRoomBinding = new EventBinding<LeaveRoom>(HandleLeaveRoom);
        EventBus<StartGame>.Subscribe(startGameBinding);
        EventBus<CloseHostedRoom>.Subscribe(closeHostedRoomBinding);
        EventBus<LeaveRoom>.Subscribe(leaveRoomBinding);

        // Set up view button callbacks
        view.disconnectBtn.onClick.AddListener(HandleDisconnect);
        view.createRoomBtn.onClick.AddListener(() => createRoomView.gameObject.SetActive(true));
        view.refreshRoomsButton.onClick.AddListener(RefreshRoomList);
        createRoomView.createRoom.onClick.AddListener(HandleCreateRoom);

        // Request initial room list
        RefreshRoomList();
        Client.Log("Lobby scene loaded.");
    }

    #region View
    // subbed by refreshRoomList on view
    void LobbyViewSubtFuncs()
    {
        view.disconnectBtn.onClick.AddListener(() => Client.Instance.Disconnect());
        view.createRoomBtn.onClick.AddListener(() => createRoomView.gameObject.SetActive(true));

    }

    void LobbyViewUNSubtFuncs()
    {
        view.disconnectBtn.onClick.RemoveAllListeners();
    }

    void RefreshRoomList() => Client.Instance.Send(new OSCMessageOut("/list_rooms"));//---

    private void HandleJoinRoom(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) { Client.Log("Join room failed: invalid room name."); return; }
        pendingRoomName = roomName;
        Client.Instance.StartTimeout(JOIN_ROOM_TIMEOUT, 5f, () =>
        {
            Client.Log("Join room timeout.");
            view.OnEnableButtons(true);
            pendingRoomName = null;
        });
        var msg = new OSCMessageOut("/join_room");
        msg.AddString(roomName);
        Client.Instance.Send(msg);
    }
    /* private void HandleJoinRoom(int id)
     {
         if (id < 0)
         {
             Client.Log("Join room failed: Room ID was set incorrectly or no room ID");
             return;
         }

         Client.Instance.StartTimeout(JOIN_ROOM_TIMEOUT, 5f, () =>
         {
             Client.Log("Join room timeout.");
             view.onEnableButtons(true);
         });

         var msg = new OSCMessageOut("/join_room"); // TODO: make server handle this
         msg.AddInt(id);
         Client.Instance.Send(msg);
     }*/
    private void GenerateRoomEntries()
    {
        view.ClearRoomList();
        foreach (var room in rooms.Values)
        {
            var entry = view.CreateRoomEntry(room);
            entry.joinBtn.onClick.AddListener(() => HandleJoinRoom(room.roomName));
            // Disable join button when UI buttons are disabled (e.g., while in a room)
            view.OnEnableButtons += (enabled) => entry.joinBtn.interactable = enabled;
        }
    }//---
    private void HandleDisconnect() => Client.Instance.Disconnect("User left lobby");//---

    #endregion

    #region Create Room View
    private void HandleCreateRoom()
    {
        string roomName = createRoomView.nameInput.text.Trim();
        int pointGoal = Mathf.RoundToInt(createRoomView.ptSlider.value);
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

        pendingRoomName = roomName;
        Client.Instance.StartTimeout(CREATE_ROOM_TIMEOUT, 8f, () =>
        {
            Client.Log("Create room timeout.");
            view.OnEnableButtons(true);
            createRoomView.gameObject.SetActive(false);
            pendingRoomName = null;
        });

        var msg = new OSCMessageOut("/create_room");
        msg.AddString(roomName).AddInt(pointGoal);
        Client.Instance.Send(msg);
    }

    public void OnCreateRoomSuccess(RoomEntryData newRoom)//---
    {
        currentRoom = newRoom;
        hostRoomView.OnCreate(newRoom.roomName, newRoom.currParticipants, newRoom.pointGoal);
        hostRoomView.gameObject.SetActive(true);
        createRoomView.gameObject.SetActive(false);
        view.OnEnableButtons(false);
    }
    #endregion

    #region Host Room View
    private void HandleCloseRoom()//---
    {
        Client.Instance.Send(new OSCMessageOut("/close_room"));
        currentRoom = null;
        view.OnEnableButtons(true);
        hostRoomView.gameObject.SetActive(false);
    }


    /// <summary>
    /// Server will see this user wishes to stat their servergame looks for it, if no server will say No server Found.
    /// </summary>
    private void HandleStartGame() => Client.Instance.Send(new OSCMessageOut("/start_game"));//---
    #endregion

    #region waiting for host View

    /// <summary>
    /// Server will see this user wishes to Leave joined room. will remove user from current room he is in.
    /// </summary>
    private void HandleLeaveRoom() //---
    {
        Client.Instance.Send(new OSCMessageOut("/leave_room"));
        currentRoom = null;
        view.OnEnableButtons(true);
        waitingForHostView.gameObject.SetActive(false);
    }

    #endregion


    // ---------------------- OSC Message Handlers ----------------------
    /// <summary>
    /// 
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="sender"></param>
    private void OnRoomUpdate(OSCMessageIn msg, IPEndPoint sender)
    {
        string roomName = msg.ReadString();
        int playerCount = msg.ReadInt();
        int maxPlayers = msg.ReadInt();
        string hostName = msg.ReadString();
        int pointGoal = msg.ReadInt();
        bool started = msg.ReadBool();

        if (!string.IsNullOrEmpty(pendingRoomName) && pendingRoomName == roomName)
        {
            currentRoom = new RoomEntryData(roomName.GetHashCode(), roomName, hostName, pointGoal, playerCount);
            pendingRoomName = null;
            Client.Instance.CancelTimeout(CREATE_ROOM_TIMEOUT);
            Client.Instance.CancelTimeout(JOIN_ROOM_TIMEOUT);

            if (hostName == Client.Instance.Username)
            {
                hostRoomView.OnCreate(roomName, playerCount, currentRoom.pointGoal);
                hostRoomView.gameObject.SetActive(true);
                createRoomView.gameObject.SetActive(false);
            }
            else
            {
                waitingForHostView.OnJoin(roomName, playerCount, currentRoom.pointGoal);
                waitingForHostView.gameObject.SetActive(true);
            }
            view.OnEnableButtons(false);
        }

        if (currentRoom != null && currentRoom.roomName == roomName)
        {
            currentRoom.currParticipants = playerCount;
            EventBus<UpdateRoomParticipants>.Publish(new UpdateRoomParticipants(playerCount));
        }

        if (started)
            OnGameStarted(null, null);
    }

    /// <summary>
    /// List of rooms recived from server
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="sender"></param>
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
            var room = new RoomEntryData(i, name, host, goal, playerCount);
            rooms[name] = room;
        }
        GenerateRoomEntries();
    }
    private void OnRoomListUpdate(OSCMessageIn msg, IPEndPoint sender)
    {
        string operation = msg.ReadString(); // "add", "update", "remove"
        string jsonRoom = msg.ReadString();   // JSON of RoomEntryData
        var room = JsonUtility.FromJson<RoomEntryData>(jsonRoom);

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
        GenerateRoomEntries();
    }//---
    private void OnGameStarted(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Game started – loading game scene.");
        SceneManager.LoadScene("2_Sc_Game"); // change to your game scene name
    }//---
    private void OnError(OSCMessageIn msg, IPEndPoint sender)
    {
        string error = msg.ReadString();
        Client.Log("Lobby Error", error);
    }//---
    private void OnDisconnected(string reason)
    {
        Client.Log("Disconnected from server in lobby: " + reason);
        Client.Instance.CancelTimeout(CREATE_ROOM_TIMEOUT);
        Client.Instance.CancelTimeout(JOIN_ROOM_TIMEOUT);
        SceneManager.LoadScene("0_SC_MainMenu");
    }//---

    // ---------------------- Cleanup ----------------------
    private void OnDestroy()
    {
        if (view != null)
        {
            view.disconnectBtn.onClick.RemoveListener(HandleDisconnect);
            view.createRoomBtn.onClick.RemoveAllListeners();
            view.refreshRoomsButton.onClick.RemoveAllListeners();
        }
        if (Client.Instance != null)
        {
            Client.Instance.RemoveListener("/room_update", OnRoomUpdate);
            Client.Instance.RemoveListener("/game_started", OnGameStarted);
            Client.Instance.RemoveListener("/error", OnError);
            Client.Instance.RemoveListener("/room_list", OnRoomList);
            Client.Instance.RemoveListener("/room_list_update", OnRoomListUpdate);
            Client.Instance.OnDisconnected -= OnDisconnected;
        }

        EventBus<StartGame>.Unsubscribe(startGameBinding);
        EventBus<CloseHostedRoom>.Unsubscribe(closeHostedRoomBinding);
        EventBus<LeaveRoom>.Unsubscribe(leaveRoomBinding);
        createRoomView.createRoom.onClick.RemoveListener(HandleCreateRoom);
    }
}
