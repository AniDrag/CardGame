using AniDrag.EventBus;
using OSCTools;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEditor.PackageManager;
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
        Client.Instance.Send(new OSCMessageOut(Msg.C_LIST_ROOMS));
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
        client.AddListener(Msg.S_CREATED_ROOM, OnRoomCreated);
        client.AddListener(Msg.S_JOINED, OnRoomJoined);
        client.AddListener(Msg.S_CLOSED_ROOM, OnRoomClosed);

    }
    
    private void BindingsSetup()
    {
        createRoomBinding = new EventBinding<CreateRoom>(CreateRoom);
        joinRoomBinding = new EventBinding<JoinRoom>(JoinRoom);
        startGameBinding = new EventBinding<StartGame>(StartGame);
        closeHostedRoomBinding = new EventBinding<CloseHostedRoom>(CloseRoom);
        leaveRoomBinding = new EventBinding<LeaveRoom>(LeaveRoom);
        refreshRoomsBinding = new EventBinding<RefreshRooms>(RefreshRoomList);
        disconnectBinding = new EventBinding<Disconnect>(Disconect);

        EventBus<CreateRoom>.Subscribe(createRoomBinding);
        EventBus<JoinRoom>.Subscribe(joinRoomBinding);
        EventBus<StartGame>.Subscribe(startGameBinding);
        EventBus<CloseHostedRoom>.Subscribe(closeHostedRoomBinding);
        EventBus<LeaveRoom>.Subscribe(leaveRoomBinding);
        EventBus<RefreshRooms>.Subscribe(refreshRoomsBinding);
        EventBus<Disconnect>.Subscribe(disconnectBinding);
    }


    private void OnDestroy()
    {
        if (Client.Instance != null)
        {
            Client.Instance.RemoveListener(Msg.S_ROOM_LIST, OnRoomList);
            Client.Instance.RemoveListener(Msg.S_ROOM_UPDATE, OnRoomUpdate);
            Client.Instance.RemoveListener(Msg.S_GAME_STARTED, OnGameStarted);
            Client.Instance.RemoveListener(Msg.S_ERROR, OnError);
            Client.Instance.RemoveListener(Msg.S_CREATED_ROOM, OnRoomCreated);
            Client.Instance.RemoveListener(Msg.S_JOINED, OnRoomJoined);
            Client.Instance.OnDisconnected -= OnDisconnected;
        }
        EventBus<CreateRoom>.Unsubscribe(createRoomBinding);
        EventBus<JoinRoom>.Unsubscribe(joinRoomBinding);
        EventBus<StartGame>.Unsubscribe(startGameBinding);
        EventBus<CloseHostedRoom>.Unsubscribe(closeHostedRoomBinding);
        EventBus<LeaveRoom>.Unsubscribe(leaveRoomBinding);
        EventBus<RefreshRooms>.Unsubscribe(refreshRoomsBinding);
        EventBus<Disconnect>.Unsubscribe(disconnectBinding);
    }

    #region Send OSC
    private void RefreshRoomList(RefreshRooms e) => Client.Instance.Send(new OSCMessageOut(Msg.C_LIST_ROOMS));

    /// <summary>
    /// DONE
    /// Requests to create room, timeout will reset stuff
    /// </summary>
    /// <param name="e">[roomName, pointGoal]</param>
    private void CreateRoom(CreateRoom e)
    {
        Debug.LogWarning("Created Room] Triggered");
        if (pendingRoomCreation) return;

        Debug.LogWarning("Created Room] No Pending state");
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

        Debug.LogWarning("Created Room] PASSED DETAILS");

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

        var msg = new OSCMessageOut(Msg.C_CREATE_ROOM)
            .AddString(roomName)
            .AddInt(pointGoal);
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

    private void Disconect(Disconnect e) => Client.Instance.Disconnect();
    private void StartGame()
    {
        Debug.Log("LobbyController.StartGame() called – sending C_START_GAME");
        Client.Instance.Send(new OSCMessageOut(Msg.C_START_GAME));
    }
    private void CloseRoom()
    {
        Debug.Log("LobbyController.CloseRoom() called – sending C_CLOSE_ROOM");
        Client.Instance.Send(new OSCMessageOut(Msg.C_CLOSE_ROOM));
    }
    private void LeaveRoom()
    {
        Debug.Log("LobbyController.CloseRoom() called – sending C_LEAVE_ROOM");
        Client.Instance.Send(new OSCMessageOut(Msg.C_LEAVE_ROOM));
    }
    #endregion

    #region OSC Receivers
    private void OnGameStarted(OSCMessageIn msg, IPEndPoint sender)
    {
        //if (pendingRoomName == null) return;
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
        Client.Log("Disconnected Loadig to Menu: " + reason);
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
        Debug.LogWarning("[On room Created] Triggered");
        Client.Instance.CancelTimeout(Msg.CREATE_ROOM_TIMEOUT);

        string roomName = msg.ReadString();
        int participantCount = msg.ReadInt();
        string hostName = msg.ReadString();
        int pointGoal = msg.ReadInt();
        bool gameStarted = msg.ReadBool();   

        var room = new RoomDataModel(roomName, hostName, pointGoal, participantCount, gameStarted);
        rooms[roomName] = room;
        view.CreateRoomEntry(room);

        // Only the host sees the HostRoomView
        if (hostName == Client.Instance.Username)
        {
            currentRoom = room;
            view.Panel_HostRoom.SetActive(true);
            view.Panel_CreateRoom.SetActive(false);
            view.Panel_WaitingForHost.SetActive(false);
            Client.Instance.CurrentRoom = roomName; 
        }


    }
    /// <summary>
    /// Basicly The view is enabled, dissable btn presses and well jsut wait for updates.
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="sender"></param>
    private void OnRoomJoined(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Instance.CancelTimeout(Msg.JOIN_ROOM_TIMEOUT);

        string roomName = msg.ReadString();
        int participantCount = msg.ReadInt();   // current participants
        string hostName = msg.ReadString();
        int pointGoal = msg.ReadInt();
        bool gameStarted = msg.ReadBool();

        // Create or update the room data
        if (!rooms.TryGetValue(roomName, out var room))
        {
            room = new RoomDataModel(roomName, hostName, pointGoal, participantCount, gameStarted);
            rooms[roomName] = room;
            view.CreateRoomEntry(room);
        }
        else
        {
            room.participantCount = participantCount;
            room.host = hostName;
            view.UpdateRoomEntry(room);
        }

        // Set current room and show waiting view (for non-host)
        if (hostName != Client.Instance.Username)   // not the host
        {
            Client.Instance.CurrentRoom = roomName;
            currentRoom = room;

            view.Panel_WaitingForHost.SetActive(true);
            view.Panel_HostRoom.SetActive(false);
            view.Panel_CreateRoom.SetActive(false);

            waitingForHostView.SetRoomData(room);
            waitingForHostView.UpdateDisplay();
        }

        pendingRoomName = null;
        EventBus<EnableButtons>.Publish(new EnableButtons(true));
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
        string hostName = msg.ReadString();
        int pointGoal = msg.ReadInt();
        bool gameStarted = msg.ReadBool();

        if (rooms.TryGetValue(roomName, out var room))
        {
            room.participantCount = participantCount;
            room.host = hostName;
            view.UpdateRoomEntry(room);
        }
        else
        {
            room = new RoomDataModel(roomName, hostName, pointGoal, participantCount, gameStarted);
            rooms[roomName] = room;
            view.CreateRoomEntry(room);
        }

        // If we are inside this room, update participant count in the active panel
        if (Client.Instance.CurrentRoom == roomName)
        {
            EventBus<UpdateRoomParticipants>.Publish(new UpdateRoomParticipants(participantCount));
        }
    }

    private void OnRoomClosed(OSCMessageIn msg, IPEndPoint sender)
    {
        string roomName = msg.ReadString();
        if (rooms.ContainsKey(roomName))
        {
            rooms.Remove(roomName);
            view.ClearRoomList();
            view.PopulateRoomList(rooms.Values.ToList());
        }

        if (Client.Instance.CurrentRoom == roomName)
        {
            Client.Log($"Room '{roomName} was closed. Returning to lobby.");
            Client.Instance.CurrentRoom = null;
            // Reset UI to lobby view
            view.Panel_HostRoom.SetActive(false);
            view.Panel_WaitingForHost.SetActive(false);
            view.Panel_CreateRoom.SetActive(false);
            EventBus<EnableButtons>.Publish(new EnableButtons(true));
            Client.Instance.Send(new OSCMessageOut(Msg.C_LIST_ROOMS));
        }
    }
    #endregion
}