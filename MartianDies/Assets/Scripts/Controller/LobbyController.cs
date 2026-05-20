using AniDrag.EventBus;
using OSCTools;
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
    private RoomDataModel currentRoom;
    private Dictionary<string, RoomDataModel> rooms = new();

    #region Event Bindings
    private EventBinding<JoinRoom> joinRoomBinding;
    private EventBinding<StartGame> startGameBinding;
    private EventBinding<CloseHostedRoom> closeHostedRoomBinding;
    private EventBinding<LeaveRoom> leaveRoomBinding;
    private EventBinding<CreateRoom> createRoomBinding;
    private EventBinding<RefreshRooms> refreshRoomsBinding;
    private EventBinding<Disconnect> disconnectBinding;
    #endregion

    private void Start()
    {
        if (!ValidateReferences()) return;
        view.SetPlayerName(Client.Instance.Username);
        SubscribeOSC();
        BindingsSetup();
        Client.Instance.OnDisconnected += OnDisconnected;
        RefreshRoomList();
        createRoomView.AddListener += RoomCreateListener;
    }

    private bool ValidateReferences()
    {
        if (Client.Instance == null) { Debug.LogError("Client instance missing!"); return false; }
        if (view == null) view = FindFirstObjectByType<LobbyView>();
        if (hostRoomView == null) hostRoomView = FindFirstObjectByType<HostRoomView>();
        if (createRoomView == null) createRoomView = FindFirstObjectByType<CreateRoomView>();
        if (waitingForHostView == null) waitingForHostView = FindFirstObjectByType<WaitingForHostView>();
        return view != null && hostRoomView != null && createRoomView != null && waitingForHostView != null;
    }

    private void SubscribeOSC()
    {
        var client = Client.Instance;
        client.AddListener(Msg.S_ROOM_LIST, OnRoomList);
        client.AddListener(Msg.S_ROOM_UPDATE, OnRoomUpdate);
        client.AddListener(Msg.S_GAME_STARTED, OnGameStarted);
        client.AddListener(Msg.S_ERROR, OnError, OSCUtil.STRING);
        client.AddListener(Msg.S_CREATED_ROOM, OnNewRoom);
        client.AddListener(Msg.S_JOINED, OnRoomJoined);

    }
    
    private void BindingsSetup()
    {
        createRoomBinding = new EventBinding<CreateRoom>(CreateRoom);
        joinRoomBinding = new EventBinding<JoinRoom>(JoinRoom);
        startGameBinding = new EventBinding<StartGame>(StartGame);
        closeHostedRoomBinding = new EventBinding<CloseHostedRoom>(CloseRoom);
        leaveRoomBinding = new EventBinding<LeaveRoom>(LeaveRoom);
        refreshRoomsBinding = new EventBinding<RefreshRooms>(e => RefreshRoomList());
        disconnectBinding = new EventBinding<Disconnect>(Disconect);

        EventBus<CreateRoom>.Subscribe(createRoomBinding);
        EventBus<JoinRoom>.Subscribe(joinRoomBinding);
        EventBus<StartGame>.Subscribe(startGameBinding);
        EventBus<CloseHostedRoom>.Subscribe(closeHostedRoomBinding);
        EventBus<LeaveRoom>.Subscribe(leaveRoomBinding);
        EventBus<RefreshRooms>.Subscribe(refreshRoomsBinding);
        EventBus<Disconnect>.Subscribe(disconnectBinding);
    }

    void RoomCreateListener(bool enable)
    {
        if (enable)
        {
            Client.Instance.AddListener(Msg.S_CREATED_ROOM, OnRoomCreated);
        }
        else
            Client.Instance.RemoveListener(Msg.S_CREATED_ROOM, OnRoomCreated);
    }

    private void OnDestroy()
    {
        if (Client.Instance != null)
        {
            Client.Instance.RemoveListener(Msg.S_ROOM_LIST, OnRoomList);
            Client.Instance.RemoveListener(Msg.S_ROOM_UPDATE, OnRoomUpdate);
            Client.Instance.RemoveListener(Msg.S_GAME_STARTED, OnGameStarted);
            Client.Instance.RemoveListener(Msg.S_ERROR, OnError);
            Client.Instance.OnDisconnected -= OnDisconnected;
        }
        createRoomView.AddListener -= RoomCreateListener;
        EventBus<CreateRoom>.Unsubscribe(createRoomBinding);
        EventBus<JoinRoom>.Unsubscribe(joinRoomBinding);
        EventBus<StartGame>.Unsubscribe(startGameBinding);
        EventBus<CloseHostedRoom>.Unsubscribe(closeHostedRoomBinding);
        EventBus<LeaveRoom>.Unsubscribe(leaveRoomBinding);
        EventBus<RefreshRooms>.Unsubscribe(refreshRoomsBinding);
        EventBus<Disconnect>.Unsubscribe(disconnectBinding);
    }

    #region Send OSC
    private void RefreshRoomList() => Client.Instance.Send(new OSCMessageOut(Msg.C_LIST_ROOMS));

    /// <summary>
    /// DONE
    /// Requests to create room, timeout will reset stuff
    /// </summary>
    /// <param name="e">[roomName, pointGoal]</param>
    private void CreateRoom(CreateRoom e)
    {
        if (pendingRoomCreation) return;
        string roomName = e.roomName.Trim();
        int pointGoal = e.pointGoal;
        if (string.IsNullOrEmpty(roomName)) 
        { 
            Client.Log("Room name empty"); 
            return; 
        }
        if (pointGoal < 10 || pointGoal > 80) 
        { 
            Client.Log("Point goal must be 10-80"); 
            return; 
        }

        pendingRoomCreation = true;
        pendingRoomName = roomName;

        EventBus<EnableButtons>.Publish(new EnableButtons(false));

        Client.Instance.StartTimeout(Msg.CREATE_ROOM_TIMEOUT, 8f, () =>
        {
            Client.Log("Create room timeout");
            pendingRoomCreation = false;
            pendingRoomName = null;
            EventBus<EnableButtons>.Publish(new EnableButtons(true));
            createRoomView.gameObject.SetActive(true);
        });

        var msg = new OSCMessageOut(Msg.C_CREATE_ROOM).AddString(roomName).AddInt(pointGoal);
        Client.Instance.Send(msg);
    }
    /// <summary>
    /// DONE
    /// Requests join room, cancles buttons so no requests can be added. timeout enables them.
    /// </summary>
    /// <param name="e"></param>
    private void JoinRoom(JoinRoom e)
    {
        if (string.IsNullOrEmpty(e.data.roomName)) return;
        pendingRoomName = e.data.roomName;
        EventBus<EnableButtons>.Publish(new EnableButtons(false));

        Client.Instance.StartTimeout(Msg.JOIN_ROOM_TIMEOUT, 5f, () =>
        {
            Client.Log("Join room timeout");
            EventBus<EnableButtons>.Publish(new EnableButtons(true));
            pendingRoomName = null;
        });

        var msg = new OSCMessageOut(Msg.C_JOIN_ROOM).AddString(e.data.roomName);
        Client.Instance.Send(msg);
    }

    private void Disconect(Disconnect e) => Client.Instance.Send(new OSCMessageOut(Msg.C_DISCONNECT));
    private void StartGame() => Client.Instance.Send(new OSCMessageOut(Msg.C_START_GAME));
    private void CloseRoom() => Client.Instance.Send(new OSCMessageOut(Msg.C_CLOSE_ROOM));
    private void LeaveRoom() => Client.Instance.Send(new OSCMessageOut(Msg.C_LEAVE_ROOM));
    #endregion

    #region OSC Receivers
    private void OnGameStarted(OSCMessageIn msg, IPEndPoint sender)
    {
        if (pendingRoomName == null) return;
        Client.Log("Game started – loading scene");
        SceneManager.LoadScene(Scenes.Game);
    }

    private void OnError(OSCMessageIn msg, IPEndPoint sender)
    {
        string error = msg.ReadString();
        Client.Log("Server error: " + error);
        EventBus<EnableButtons>.Publish(new EnableButtons(true));
        pendingRoomCreation = false;
        pendingRoomName = null;
        Client.Instance.CancelTimeout(Msg.CREATE_ROOM_TIMEOUT);
        Client.Instance.CancelTimeout(Msg.JOIN_ROOM_TIMEOUT);
        createRoomView.gameObject.SetActive(false);
    }

    private void OnDisconnected(string reason)
    {
        Client.Log("Disconnected from server in lobby: " + reason);
        Client.Instance.CancelTimeout(Msg.CREATE_ROOM_TIMEOUT);
        Client.Instance.CancelTimeout(Msg.JOIN_ROOM_TIMEOUT);
        SceneManager.LoadScene(Scenes.MainMenu);
    }

    private void OnRoomList(OSCMessageIn msg, IPEndPoint sender)
    {
        int roomCount = msg.ReadInt();
        rooms.Clear();
        for (int i = 0; i < roomCount; i++)
        {
            string name = msg.ReadString();
            int goal = msg.ReadInt();
            string host = msg.ReadString();
            int playerCount = msg.ReadInt();
            int state = msg.ReadInt(); // 0 = waiting, 1 = in-game (unused)
            var room = new RoomDataModel(name, host, goal, playerCount);
            rooms[name] = room;
        }
        view.ClearRoomList();
        view.PopulateRoomList(rooms.Values.ToList());
    }
    /// <summary>
    /// DONE
    /// Sends user to the Host room screen Subbed to S_CreateRoom. when we enable the Create windw we add a listener for On roo
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="sender"></param>
    private void OnRoomCreated(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Instance.CancelTimeout(Msg.CREATE_ROOM_TIMEOUT);
        hostRoomView.gameObject.SetActive(true);

        string roomName = msg.ReadString();
        int participantCount = msg.ReadInt();
        int maxPlayers = msg.ReadInt();
        string hostName = msg.ReadString();
        int pointGoal = msg.ReadInt();
        bool gameStarted = msg.ReadBool();

        
        RoomDataModel room = new RoomDataModel(roomName, hostName, pointGoal, participantCount);
        rooms[roomName] = room;

        view.CreateRoomEntry(room);

        currentRoom = room;
        view.Panel_HostRoom.gameObject.SetActive(true);
        view.Panel_CreateRoom.gameObject.SetActive(false);
        view.Panel_WaitingForHost.gameObject.SetActive(false);


    }
    /// <summary>
    /// Basicly The view is enabled, dissable btn presses and well jsut wait for updates.
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="sender"></param>
    private void OnRoomJoined(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Instance.CancelTimeout(Msg.JOIN_ROOM_TIMEOUT);
        waitingForHostView.gameObject.SetActive(true);

        string roomName = msg.ReadString();
        int participantCount = msg.ReadInt();
        int maxPlayers = msg.ReadInt();
        string hostName = msg.ReadString();
        int pointGoal = msg.ReadInt();
        bool gameStarted = msg.ReadBool();


        RoomDataModel room = new RoomDataModel(roomName, hostName, pointGoal, participantCount);
        rooms[roomName].participantCount++;
        view.UpdateRoomEntry(room); // probably should e its seperate call since if we recive a packet it goes there.
        waitingForHostView.UpdateDisplay();


    }

    /// <summary>
    /// Get room data, find it in Rooms and update its visuals. if the user is in its waiting part udate participants count.
    /// When calling this it should only do, is in game now? new participant count.
    /// if its a new room then room List should be called and arange the details.
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="sender"></param>
    private void OnRoomUpdate(OSCMessageIn msg, IPEndPoint sender)
    {
            string roomName = msg.ReadString();
            int participantCount = msg.ReadInt();
            int maxPlayers = msg.ReadInt();
            string hostName = msg.ReadString();
            int pointGoal = msg.ReadInt();
            bool gameStarted = msg.ReadBool();

            
            EventBus<UpdateRoomParticipants>.Publish(new UpdateRoomParticipants(participantCount));

    }
    #endregion
}