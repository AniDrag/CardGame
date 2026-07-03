using AniDrag.EventBus;
using AniDrag.Utility;
using OSCTools;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyController : MonoBehaviour
{
    #region View References

    [SerializeField] private LobbyView view;
    [SerializeField] private HostRoomView hostRoomView;
    [SerializeField] private CreateRoomView createRoomView;
    [SerializeField] private WaitingForHostView waitingForHostView;

    #endregion

    #region State

    private bool pendingRoomCreation = false;
    private string pendingRoomName = null;

    private RoomDataModel currentRoom;
    private readonly Dictionary<string, RoomDataModel> rooms = new();

    #endregion

    #region Event Bindings

    private EventBinding<CreateRoom> createRoomBinding;
    private EventBinding<JoinRoom> joinRoomBinding;
    private EventBinding<StartGame> startGameBinding;
    private EventBinding<CloseHostedRoom> closeHostedRoomBinding;
    private EventBinding<LeaveRoom> leaveRoomBinding;
    private EventBinding<RefreshRooms> refreshRoomsBinding;
    private EventBinding<Disconnect> disconnectBinding;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (!ValidateReferences())
            return;

        view.SetPlayerName(Client.Instance.Username);

        RegisterServerMessages();
        RegisterUIEvents();

        Client.Instance.OnDisconnected += OnDisconnected;

        SendRefreshRooms();
    }

    private void OnDestroy()
    {
        UnregisterServerMessages();
        UnregisterUIEvents();

        if (Client.Instance != null)
            Client.Instance.OnDisconnected -= OnDisconnected;
    }

    #endregion

    #region Setup

    private bool ValidateReferences()
    {
        if (Client.Instance == null)
        {
            Debug.LogError("Client instance missing!");
            return false;
        }

        if (view == null)
            view = GetComponent<LobbyView>() ?? GetComponentInChildren<LobbyView>(true) ?? FindFirstObjectByType<LobbyView>();

        if (hostRoomView == null)
            hostRoomView = GetComponent<HostRoomView>() ?? GetComponentInChildren<HostRoomView>(true) ?? FindFirstObjectByType<HostRoomView>();

        if (createRoomView == null)
            createRoomView = GetComponent<CreateRoomView>() ?? GetComponentInChildren<CreateRoomView>(true) ?? FindFirstObjectByType<CreateRoomView>();

        if (waitingForHostView == null)
            waitingForHostView = GetComponent<WaitingForHostView>() ?? GetComponentInChildren<WaitingForHostView>(true) ?? FindFirstObjectByType<WaitingForHostView>();

        bool valid = true;

        if (view == null)
        {
            Debug.LogError("LobbyView missing!");
            valid = false;
        }

        if (hostRoomView == null)
        {
            Debug.LogError("HostRoomView missing!");
            valid = false;
        }

        if (createRoomView == null)
        {
            Debug.LogError("CreateRoomView missing!");
            valid = false;
        }

        if (waitingForHostView == null)
        {
            Debug.LogError("WaitingForHostView missing!");
            valid = false;
        }

        return valid;
    }

    #endregion

    #region Message Registration

    private void RegisterServerMessages()
    {
        Client client = Client.Instance;

        client.AddListener(Msg.S_ROOM_LIST, OnRoomList);
        client.AddListener(Msg.S_ROOM_UPDATE, OnRoomUpdate);
        client.AddListener(Msg.S_GAME_STARTED, OnGameStarted);
        client.AddListener(Msg.S_ERROR, OnError, OSCUtil.STRING);
        client.AddListener(Msg.S_CREATED_ROOM, OnRoomCreated);
        client.AddListener(Msg.S_JOINED, OnRoomJoined);
        client.AddListener(Msg.S_CLOSED_ROOM, OnRoomClosed);
        client.AddListener(Msg.S_RETURN_TO_LOBBY, OnReturnToLobby, OSCUtil.STRING);
    }

    private void UnregisterServerMessages()
    {
        if (Client.Instance == null)
            return;

        Client client = Client.Instance;

        client.RemoveListener(Msg.S_ROOM_LIST, OnRoomList);
        client.RemoveListener(Msg.S_ROOM_UPDATE, OnRoomUpdate);
        client.RemoveListener(Msg.S_GAME_STARTED, OnGameStarted);
        client.RemoveListener(Msg.S_ERROR, OnError);
        client.RemoveListener(Msg.S_CREATED_ROOM, OnRoomCreated);
        client.RemoveListener(Msg.S_JOINED, OnRoomJoined);
        client.RemoveListener(Msg.S_CLOSED_ROOM, OnRoomClosed);
        client.RemoveListener(Msg.S_RETURN_TO_LOBBY, OnReturnToLobby);
    }

    #endregion

    #region UI Event Registration

    private void RegisterUIEvents()
    {
        createRoomBinding = new EventBinding<CreateRoom>(SendCreateRoom);
        joinRoomBinding = new EventBinding<JoinRoom>(SendJoinRoom);
        startGameBinding = new EventBinding<StartGame>(SendStartGame);
        closeHostedRoomBinding = new EventBinding<CloseHostedRoom>(SendCloseHostedRoom);
        leaveRoomBinding = new EventBinding<LeaveRoom>(SendLeaveRoom);
        refreshRoomsBinding = new EventBinding<RefreshRooms>(_ => SendRefreshRooms());
        disconnectBinding = new EventBinding<Disconnect>(SendDisconnect);

        EventBus<CreateRoom>.Subscribe(createRoomBinding);
        EventBus<JoinRoom>.Subscribe(joinRoomBinding);
        EventBus<StartGame>.Subscribe(startGameBinding);
        EventBus<CloseHostedRoom>.Subscribe(closeHostedRoomBinding);
        EventBus<LeaveRoom>.Subscribe(leaveRoomBinding);
        EventBus<RefreshRooms>.Subscribe(refreshRoomsBinding);
        EventBus<Disconnect>.Subscribe(disconnectBinding);
    }

    private void UnregisterUIEvents()
    {
        if (createRoomBinding != null)
            EventBus<CreateRoom>.Unsubscribe(createRoomBinding);

        if (joinRoomBinding != null)
            EventBus<JoinRoom>.Unsubscribe(joinRoomBinding);

        if (startGameBinding != null)
            EventBus<StartGame>.Unsubscribe(startGameBinding);

        if (closeHostedRoomBinding != null)
            EventBus<CloseHostedRoom>.Unsubscribe(closeHostedRoomBinding);

        if (leaveRoomBinding != null)
            EventBus<LeaveRoom>.Unsubscribe(leaveRoomBinding);

        if (refreshRoomsBinding != null)
            EventBus<RefreshRooms>.Unsubscribe(refreshRoomsBinding);

        if (disconnectBinding != null)
            EventBus<Disconnect>.Unsubscribe(disconnectBinding);
    }

    #endregion

    #region Sending Messages

    private void SendRefreshRooms()
    {
        Client.Instance.Send(new OSCMessageOut(Msg.C_LIST_ROOMS));
    }

    private void SendCreateRoom(CreateRoom e)
    {
        if (pendingRoomCreation)
            return;

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

        Client.Instance.StartTimeout(Msg.CREATE_ROOM_TIMEOUT, 8f, OnCreateRoomTimeout);

        var msg = new OSCMessageOut(Msg.C_CREATE_ROOM)
            .AddString(roomName)
            .AddInt(pointGoal);

        Client.Instance.Send(msg);
    }

    private void SendJoinRoom(JoinRoom e)
    {
        if (e.data == null || string.IsNullOrEmpty(e.data.roomName))
            return;

        pendingRoomName = e.data.roomName;

        EventBus<EnableButtons>.Publish(new EnableButtons(false));

        Client.Instance.StartTimeout(Msg.JOIN_ROOM_TIMEOUT, 5f, OnJoinRoomTimeout);

        var msg = new OSCMessageOut(Msg.C_JOIN_ROOM)
            .AddString(e.data.roomName);

        Client.Instance.Send(msg);
    }

    private void SendDisconnect(Disconnect e)
    {
        Client.Instance.Disconnect();
    }

    private void SendStartGame(StartGame e)
    {
        Debug.Log("LobbyController.SendStartGame() called");
        Client.Instance.Send(new OSCMessageOut(Msg.C_START_GAME));
    }

    private void SendCloseHostedRoom(CloseHostedRoom e)
    {
        Debug.Log("LobbyController.SendCloseHostedRoom() called");
        Client.Instance.Send(new OSCMessageOut(Msg.C_CLOSE_ROOM));
    }

    private void SendLeaveRoom(LeaveRoom e)
    {
        Debug.Log("LobbyController.SendLeaveRoom() called");

        Client.Instance.Send(new OSCMessageOut(Msg.C_LEAVE_ROOM));

        // Server currently does not send a private "left room" confirmation.
        // So we locally return to lobby after sending the request.
        LeaveCurrentRoomLocally();

        SendRefreshRooms();
    }

    #endregion

    #region Received Messages

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
            int state = msg.ReadInt();

            bool isInGame = state == 1;

            if (isInGame)
                continue;

            RoomDataModel room = new RoomDataModel(name, host, goal, playerCount, isInGame);
            rooms[name] = room;
        }

        view.ClearRoomList();
        view.PopulateRoomList(rooms.Values.ToList());
    }

    private void OnRoomCreated(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Instance.CancelTimeout(Msg.CREATE_ROOM_TIMEOUT);

        pendingRoomCreation = false;
        pendingRoomName = null;

        string roomName = msg.ReadString();
        int participantCount = msg.ReadInt();
        string hostName = msg.ReadString();
        int pointGoal = msg.ReadInt();
        bool gameStarted = msg.ReadBool();

        RoomDataModel room = new RoomDataModel(roomName, hostName, pointGoal, participantCount, gameStarted);

        RegisterRoom(room);

        Client.Instance.CurrentRoom = roomName;
        currentRoom = room;

        view.ShowHostRoom();
        hostRoomView.SetRoomData(room);

        EventBus<EnableButtons>.Publish(new EnableButtons(false));
    }

    private void OnRoomJoined(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Instance.CancelTimeout(Msg.JOIN_ROOM_TIMEOUT);

        pendingRoomCreation = false;
        pendingRoomName = null;

        string roomName = msg.ReadString();
        int participantCount = msg.ReadInt();
        string hostName = msg.ReadString();
        int pointGoal = msg.ReadInt();
        bool gameStarted = msg.ReadBool();

        RoomDataModel room = new RoomDataModel(roomName, hostName, pointGoal, participantCount, gameStarted);

        RegisterRoom(room);

        Client.Instance.CurrentRoom = roomName;
        currentRoom = room;

        if (hostName == Client.Instance.Username)
        {
            view.ShowHostRoom();
            hostRoomView.SetRoomData(room);
        }
        else
        {
            view.ShowWaitingForHost();
            waitingForHostView.SetRoomData(room);
        }

        EventBus<EnableButtons>.Publish(new EnableButtons(false));
    }

    private void OnRoomUpdate(OSCMessageIn msg, IPEndPoint sender)
    {
        string roomName = msg.ReadString();
        int participantCount = msg.ReadInt();
        string hostName = msg.ReadString();
        int pointGoal = msg.ReadInt();
        bool gameStarted = msg.ReadBool();

        RoomDataModel room = new RoomDataModel(roomName, hostName, pointGoal, participantCount, gameStarted);

        if (gameStarted)
        {
            RemoveRoomFromList(roomName);
        }
        else
        {
            RegisterRoom(room);
        }

        if (Client.Instance.CurrentRoom == roomName)
        {
            currentRoom = room;

            EventBus<UpdateRoomParticipants>.Publish(new UpdateRoomParticipants(participantCount));

            if (hostName == Client.Instance.Username)
            {
                view.ShowHostRoom();
                hostRoomView.SetRoomData(room);
            }
            else
            {
                view.ShowWaitingForHost();
                waitingForHostView.SetRoomData(room);
            }
        }
    }

    private void OnRoomClosed(OSCMessageIn msg, IPEndPoint sender)
    {
        string roomName = msg.ReadString();

        RemoveRoomFromList(roomName);

        if (Client.Instance.CurrentRoom == roomName)
        {
            Client.Log($"Room '{roomName}' was closed. Returning to lobby.");

            LeaveCurrentRoomLocally();

            SendRefreshRooms();
        }
    }

    private void OnGameStarted(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Game started. Loading game scene.");
        SceneManager.LoadScene(Scenes.Game);
    }

    private void OnReturnToLobby(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();

        Client.Log("Lobby", "Return to lobby: " + reason);

        LeaveCurrentRoomLocally();
        SendRefreshRooms();
    }

    private void OnError(OSCMessageIn msg, IPEndPoint sender)
    {
        string error = msg.ReadString();

        Client.Log("Server error: " + error);

        ResetPendingRequests();

        EventBus<EnableButtons>.Publish(new EnableButtons(true));

        if (createRoomView != null)
            createRoomView.EnableView(false);
    }

    private void OnDisconnected(string reason)
    {
        Client.Log("Disconnected. Loading main menu: " + reason);

        ResetPendingRequests();

        SceneManager.LoadScene(Scenes.MainMenu);
    }

    #endregion

    #region Room Cache And UI Sync

    private void RegisterRoom(RoomDataModel room)
    {
        rooms[room.roomName] = room;
        view.UpdateRoomEntry(room);
    }

    private void RemoveRoomFromList(string roomName)
    {
        if (!rooms.ContainsKey(roomName))
            return;

        rooms.Remove(roomName);
        view.RemoveRoomEntry(roomName);
    }

    private void LeaveCurrentRoomLocally()
    {
        Client.Instance.CurrentRoom = null;
        currentRoom = null;
        pendingRoomName = null;
        pendingRoomCreation = false;

        view.ShowLobby();

        EventBus<EnableButtons>.Publish(new EnableButtons(true));
    }

    private void ResetPendingRequests()
    {
        pendingRoomCreation = false;
        pendingRoomName = null;

        Client.Instance.CancelTimeout(Msg.CREATE_ROOM_TIMEOUT);
        Client.Instance.CancelTimeout(Msg.JOIN_ROOM_TIMEOUT);
    }

    #endregion

    #region Timeouts

    private void OnCreateRoomTimeout()
    {
        Client.Log("Create room timeout");

        pendingRoomCreation = false;
        pendingRoomName = null;

        EventBus<EnableButtons>.Publish(new EnableButtons(true));

        if (createRoomView != null)
            createRoomView.EnableView(true);
    }

    private void OnJoinRoomTimeout()
    {
        Client.Log("Join room timeout");

        pendingRoomName = null;

        EventBus<EnableButtons>.Publish(new EnableButtons(true));
    }

    #endregion

    #region Debug Testing

    [Button]
    private void Debug_StartGame(string addString = "")
    {
        SendTestingMessage(Msg.C_START_GAME, addString);
    }

    [Button]
    private void Debug_LeaveRoom(string addString = "")
    {
        SendTestingMessage(Msg.C_LEAVE_ROOM, addString);
    }

    [Button]
    private void Debug_CloseRoom(string addString = "")
    {
        SendTestingMessage(Msg.C_CLOSE_ROOM, addString);
    }

    private void SendTestingMessage(string title, string message)
    {
        var msg = new OSCMessageOut(title);

        if (!string.IsNullOrEmpty(message))
            msg.AddString(message);

        Client.Instance.Send(msg);
    }

    #endregion
}