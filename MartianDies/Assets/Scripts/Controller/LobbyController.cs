using AniDrag.EventBus;
using AniDrag.Utility;
using AniDrag.Utility.Inspector;
using OSCTools;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * LobbyController
 *
 * Purpose:
 * This script controls the client-side lobby scene.
 *
 * What it does:
 * - Shows the current player name.
 * - Requests and displays the room list.
 * - Sends room actions to the server.
 * - Handles room updates received from the server.
 * - Switches between lobby view, host room view, and waiting for host view.
 * - Loads the game scene when the server starts the match.
 *
 * Naming rule used in this script:
 * - On prefix = receiving OSC message from the server.
 * - Send prefix = sending OSC message to the server.
 * - No On prefix = normal local function, EventBus function, helper, client event, or timeout.
 *
 * Unity callback exception:
 * Start and OnDestroy keep their Unity names because Unity calls them automatically.
 *
 * Important:
 * This client does not decide if a room action is really allowed.
 * The client sends requests, and the server validates them.
 */

public class LobbyController : MonoBehaviour
{
    #region View References

    [SerializeField] private LobbyView view;
    [SerializeField] private HostRoomView hostRoomView;
    [SerializeField] private CreateRoomView createRoomView;
    [SerializeField] private WaitingForHostView waitingForHostView;

    #endregion

    #region State

    /*
     * pendingRoomCreation:
     * True while the client is waiting for the server to answer a create room request.
     *
     * pendingRoomName:
     * Stores the room name involved in a pending create or join request.
     *
     * currentRoom:
     * The room this client is currently inside.
     *
     * rooms:
     * Local cache of rooms received from the server.
     * Key = room name.
     * Value = room data.
     */

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

    /*
     * Unity callback.
     *
     * What this does:
     * Runs when the Lobby scene starts.
     *
     * Flow:
     * 1. Validate all required references.
     * 2. Set the visible player name.
     * 3. Register server OSC messages.
     * 4. Register local UI/EventBus events.
     * 5. Listen for client disconnects.
     * 6. Ask the server for the current room list.
     */
    private void Start()
    {
        if (!ValidateReferences())
            return;

        view.SetPlayerName(Client.Instance.Username);

        RegisterServerMessages();
        RegisterUIEvents();

        Client.Instance.OnDisconnected += Disconnected;

        SendRefreshRooms();
    }
    private void OnDestroy()
    {
        UnregisterServerMessages();
        UnregisterUIEvents();

        if (Client.Instance != null)
            Client.Instance.OnDisconnected -= Disconnected;
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

    /*
     * What this does:
     * Registers all OSC messages this lobby needs to receive from the server.
     *
     * OSC received:
     *
     * Msg.S_ROOM_LIST
     * Payload:
     * [0] int roomCount
     * Then repeated roomCount times:
     *     string roomName
     *     int pointGoal
     *     string hostName
     *     int playerCount
     *     int state
     *
     * Msg.S_ROOM_UPDATE
     * Payload:
     * [0] string roomName
     * [1] int participantCount
     * [2] string hostName
     * [3] int pointGoal
     * [4] bool gameStarted
     *
     * Msg.S_GAME_STARTED
     * Payload:
     * No data read here.
     *
     * Msg.S_ERROR
     * Payload:
     * [0] string error
     *
     * Msg.S_CREATED_ROOM
     * Payload:
     * [0] string roomName
     * [1] int participantCount
     * [2] string hostName
     * [3] int pointGoal
     * [4] bool gameStarted
     *
     * Msg.S_JOINED
     * Payload:
     * [0] string roomName
     * [1] int participantCount
     * [2] string hostName
     * [3] int pointGoal
     * [4] bool gameStarted
     *
     * Msg.S_CLOSED_ROOM
     * Payload:
     * [0] string roomName
     *
     * Msg.S_RETURN_TO_LOBBY
     * Payload:
     * [0] string reason
     */
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

    #region Sending OSC Messages

    /*
     * OSC SEND: Msg.C_LIST_ROOMS
     *
     * Payload sent:
     * No data.
     *
     * What this tells the server:
     * This client wants the latest list of available rooms.
     */
    private void SendRefreshRooms()
    {
        Client.Instance.Send(new OSCMessageOut(Msg.C_LIST_ROOMS));
    }

    /*
     * OSC SEND: Msg.C_CREATE_ROOM
     *
     * Local event data received:
     * e.roomName = requested room name.
     * e.pointGoal = score needed to win.
     *
     * Payload sent:
     * [0] string roomName
     * [1] int pointGoal
     *
     * Example:
     * roomName = "Test Room"
     * pointGoal = 25
     *
     * What this does:
     * Validates basic input locally, disables buttons, starts a timeout,
     * then sends the create room request to the server.
     *
     * Important:
     * The server still validates the room name and point goal again.
     */
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

        Client.Instance.StartTimeout(Msg.CREATE_ROOM_TIMEOUT, 8f, CreateRoomTimeout);

        var msg = new OSCMessageOut(Msg.C_CREATE_ROOM)
            .AddString(roomName)
            .AddInt(pointGoal);

        Client.Instance.Send(msg);
    }

    /*
     * OSC SEND: Msg.C_JOIN_ROOM
     *
     * Local event data received:
     * e.data = selected room data.
     * e.data.roomName = room the player wants to join.
     *
     * Payload sent:
     * [0] string roomName
     *
     * What this does:
     * Starts a timeout and asks the server to join the selected room.
     */
    private void SendJoinRoom(JoinRoom e)
    {
        if (e.data == null || string.IsNullOrEmpty(e.data.roomName))
            return;

        pendingRoomName = e.data.roomName;

        EventBus<EnableButtons>.Publish(new EnableButtons(false));

        Client.Instance.StartTimeout(Msg.JOIN_ROOM_TIMEOUT, 5f, JoinRoomTimeout);

        var msg = new OSCMessageOut(Msg.C_JOIN_ROOM)
            .AddString(e.data.roomName);

        Client.Instance.Send(msg);
    }

    /*
     * Local EventBus event: Disconnect
     *
     * This does not build a lobby OSC message directly.
     * It calls Client.Instance.Disconnect(), and the Client handles disconnect logic.
     */
    private void SendDisconnect(Disconnect e)
    {
        Client.Instance.Disconnect();
    }

    /*
     * OSC SEND: Msg.C_START_GAME
     *
     * Payload sent:
     * No data.
     *
     * What this tells the server:
     * The host wants to start the game.
     *
     * Important:
     * The server should check if this client is really the host.
     */
    private void SendStartGame(StartGame e)
    {
        Debug.Log("LobbyController.SendStartGame() called");
        Client.Instance.Send(new OSCMessageOut(Msg.C_START_GAME));
    }

    /*
     * OSC SEND: Msg.C_CLOSE_ROOM
     *
     * Payload sent:
     * No data.
     *
     * What this tells the server:
     * The host wants to close their current room.
     */
    private void SendCloseHostedRoom(CloseHostedRoom e)
    {
        Debug.Log("LobbyController.SendCloseHostedRoom() called");
        Client.Instance.Send(new OSCMessageOut(Msg.C_CLOSE_ROOM));
    }

    /*
     * OSC SEND: Msg.C_LEAVE_ROOM
     *
     * Payload sent:
     * No data.
     *
     * What this tells the server:
     * This client wants to leave the current room.
     *
     * Note:
     * The server currently does not send a private "left room" confirmation.
     * Because of that, the client locally returns to the lobby after sending.
     */
    private void SendLeaveRoom(LeaveRoom e)
    {
        Debug.Log("LobbyController.SendLeaveRoom() called");

        Client.Instance.Send(new OSCMessageOut(Msg.C_LEAVE_ROOM));

        LeaveCurrentRoomLocally();

        SendRefreshRooms();
    }

    #endregion

    #region Received OSC Messages

    /*
     * OSC RECEIVE: Msg.S_ROOM_LIST
     *
     * Payload received:
     * [0] int roomCount
     * Then repeated roomCount times:
     *     string name
     *     int goal
     *     string host
     *     int playerCount
     *     int state
     *
     * Example:
     * roomCount = 2
     *
     * Room 1:
     * name = "Room A"
     * goal = 25
     * host = "Nik"
     * playerCount = 1
     * state = 0
     *
     * Room 2:
     * name = "Room B"
     * goal = 50
     * host = "Alex"
     * playerCount = 2
     * state = 1
     *
     * state:
     * 0 = lobby/open room
     * 1 = in game
     *
     * What this does:
     * Rebuilds the local room cache and room list UI.
     * Rooms already in game are skipped.
     */
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

    /*
     * OSC RECEIVE: Msg.S_CREATED_ROOM
     *
     * Payload received:
     * [0] string roomName
     * [1] int participantCount
     * [2] string hostName
     * [3] int pointGoal
     * [4] bool gameStarted
     *
     * What this means:
     * The server accepted the create room request.
     *
     * What this does:
     * Cancels the create room timeout, stores the room,
     * switches the UI to host room view, and disables room action buttons.
     */
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
        Client.Instance.CurrentPointGoal = pointGoal;
        RoomDataModel room = new RoomDataModel(roomName, hostName, pointGoal, participantCount, gameStarted);

        RegisterRoom(room);

        Client.Instance.CurrentRoom = roomName;
        currentRoom = room;

        view.ShowHostRoom();
        hostRoomView.SetRoomData(room);

        EventBus<EnableButtons>.Publish(new EnableButtons(false));
    }

    /*
     * OSC RECEIVE: Msg.S_JOINED
     *
     * Payload received:
     * [0] string roomName
     * [1] int participantCount
     * [2] string hostName
     * [3] int pointGoal
     * [4] bool gameStarted
     *
     * What this means:
     * The server accepted the join room request.
     *
     * What this does:
     * Cancels the join room timeout, stores the room,
     * then shows host UI or waiting-for-host UI depending on who owns the room.
     */
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
        Client.Instance.CurrentPointGoal = pointGoal;
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

    /*
     * OSC RECEIVE: Msg.S_ROOM_UPDATE
     *
     * Payload received:
     * [0] string roomName
     * [1] int participantCount
     * [2] string hostName
     * [3] int pointGoal
     * [4] bool gameStarted
     *
     * What this does:
     * Updates the room cache and UI when a room changes.
     *
     * If the room started a game:
     * It is removed from the public room list.
     *
     * If this client is inside the updated room:
     * The current room UI is refreshed.
     */
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

    /*
     * OSC RECEIVE: Msg.S_CLOSED_ROOM
     *
     * Payload received:
     * [0] string roomName
     *
     * What this means:
     * A room was closed by the host or server.
     *
     * What this does:
     * Removes the room from the list.
     * If this client was inside that room, it returns locally to the lobby.
     */
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

    /*
     * OSC RECEIVE: Msg.S_GAME_STARTED
     *
     * Payload received:
     * No data is read here.
     *
     * What this means:
     * The server started the game for this room.
     *
     * What this does:
     * Loads the Game scene.
     */
    private void OnGameStarted(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Game started. Loading game scene.");
        SceneManager.LoadScene(Scenes.Game);
    }

    /*
     * OSC RECEIVE: Msg.S_RETURN_TO_LOBBY
     *
     * Payload received:
     * [0] string reason
     *
     * Example:
     * reason = "Game cancelled."
     *
     * What this does:
     * Leaves the current room locally and refreshes the room list.
     */
    private void OnReturnToLobby(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();

        Client.Log("Lobby", "Return to lobby: " + reason);

        LeaveCurrentRoomLocally();
        SendRefreshRooms();
    }

    /*
     * OSC RECEIVE: Msg.S_ERROR
     *
     * Payload received:
     * [0] string error
     *
     * Example:
     * error = "Room does not exist."
     *
     * What this does:
     * Shows/logs a server error and resets pending room requests.
     */
    private void OnError(OSCMessageIn msg, IPEndPoint sender)
    {
        string error = msg.ReadString();

        Client.Log("Server error: " + error);

        ResetPendingRequests();

        EventBus<EnableButtons>.Publish(new EnableButtons(true));

        if (createRoomView != null)
            createRoomView.EnableView(false);
    }

    /*
     * Client event.
     *
     * Data received:
     * reason = why the Client disconnected.
     *
     * This is not an OSC receive function, so it does not use the On prefix.
     *
     * What this does:
     * Resets pending lobby requests and loads the Main Menu scene.
     */
    private void Disconnected(string reason)
    {
        Client.Log("Disconnected. Loading main menu: " + reason);

        ResetPendingRequests();

        SceneManager.LoadScene(Scenes.MainMenu);
    }

    #endregion

    #region Room Cache And UI Sync

    /*
     * What this does:
     * Adds or updates a room in the local room cache and room list UI.
     */
    private void RegisterRoom(RoomDataModel room)
    {
        rooms[room.roomName] = room;
        view.UpdateRoomEntry(room);
    }

    /*
     * What this does:
     * Removes a room from the local room cache and room list UI.
     */
    private void RemoveRoomFromList(string roomName)
    {
        if (!rooms.ContainsKey(roomName))
            return;

        rooms.Remove(roomName);
        view.RemoveRoomEntry(roomName);
    }

    /*
     * What this does:
     * Clears this client's current room state without waiting for another server message.
     *
     * Used when:
     * - The client sends leave room.
     * - The room is closed.
     * - The server tells the client to return to lobby.
     */
    private void LeaveCurrentRoomLocally()
    {
        Client.Instance.CurrentRoom = null;
        currentRoom = null;
        pendingRoomName = null;
        pendingRoomCreation = false;

        view.ShowLobby();

        EventBus<EnableButtons>.Publish(new EnableButtons(true));
    }

    /*
     * What this does:
     * Clears pending create/join state and cancels room request timeouts.
     */
    private void ResetPendingRequests()
    {
        pendingRoomCreation = false;
        pendingRoomName = null;

        Client.Instance.CancelTimeout(Msg.CREATE_ROOM_TIMEOUT);
        Client.Instance.CancelTimeout(Msg.JOIN_ROOM_TIMEOUT);
    }

    #endregion

    #region Timeouts

    /*
     * Timeout callback.
     *
     * What this means:
     * The client sent Msg.C_CREATE_ROOM but did not receive
     * Msg.S_CREATED_ROOM or Msg.S_ERROR in time.
     *
     * What this does:
     * Clears the create room pending state and re-enables buttons.
     */
    private void CreateRoomTimeout()
    {
        Client.Log("Create room timeout");

        pendingRoomCreation = false;
        pendingRoomName = null;

        EventBus<EnableButtons>.Publish(new EnableButtons(true));

        if (createRoomView != null)
            createRoomView.EnableView(true);
    }

    /*
     * Timeout callback.
     *
     * What this means:
     * The client sent Msg.C_JOIN_ROOM but did not receive
     * Msg.S_JOINED or Msg.S_ERROR in time.
     *
     * What this does:
     * Clears the join room pending state and re-enables buttons.
     */
    private void JoinRoomTimeout()
    {
        Client.Log("Join room timeout");

        pendingRoomName = null;

        EventBus<EnableButtons>.Publish(new EnableButtons(true));
    }

    #endregion

    #region Debug Testing

    /*
     * Debug inspector button.
     *
     * What this does:
     * Sends Msg.C_START_GAME manually for testing.
     */
    [DebugButton]
    private void DebugStartGame(string addString = "")
    {
        SendTestingMessage(Msg.C_START_GAME, addString);
    }

    /*
     * Debug inspector button.
     *
     * What this does:
     * Sends Msg.C_LEAVE_ROOM manually for testing.
     */
    [DebugButton]
    private void DebugLeaveRoom(string addString = "")
    {
        SendTestingMessage(Msg.C_LEAVE_ROOM, addString);
    }

    /*
     * Debug inspector button.
     *
     * What this does:
     * Sends Msg.C_CLOSE_ROOM manually for testing.
     */
    [DebugButton]
    private void DebugCloseRoom(string addString = "")
    {
        SendTestingMessage(Msg.C_CLOSE_ROOM, addString);
    }

    /*
     * OSC SEND: debug message
     *
     * Payload sent:
     * [0] string message, only if message is not empty.
     *
     * What this does:
     * Sends a manual testing OSC message.
     * This is only for debugging and should not be used as normal gameplay flow.
     */
    private void SendTestingMessage(string title, string message)
    {
        var msg = new OSCMessageOut(title);

        if (!string.IsNullOrEmpty(message))
            msg.AddString(message);

        Client.Instance.Send(msg);
    }

    #endregion
}