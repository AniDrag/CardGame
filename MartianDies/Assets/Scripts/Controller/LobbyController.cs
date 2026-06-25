using AniDrag.EventBus;
using AniDrag.Utility;
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
    private void StartGame(StartGame e)
    {
        Debug.Log("LobbyController.StartGame() called – sending C_START_GAME");
        Client.Instance.Send(new OSCMessageOut(Msg.C_START_GAME));
    }
    private void CloseRoom(CloseHostedRoom e)
    {
        Debug.Log("LobbyController.CloseRoom() called – sending C_CLOSE_ROOM");
        Client.Instance.Send(new OSCMessageOut(Msg.C_CLOSE_ROOM));
    }
    private void LeaveRoom(LeaveRoom e)
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
        pendingRoomCreation = false;

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
        pendingRoomCreation = false;
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
            pendingRoomCreation = false;
            Client.Instance.Send(new OSCMessageOut(Msg.C_LIST_ROOMS));
        }
    }
    #endregion

    #region Debug Testing 
    [Button]
    void Debug_StartGame(int addInt = -1, float addFloat = -1, string addString ="", bool addBool = false, bool sentBool =false)
    {
        var msg = new OSCMessageOut(Msg.C_START_GAME);
        if(addString != string.Empty)
            msg.AddString(addString);
        if(addInt != -1)
            msg.AddInt(addInt);
        if(addFloat != -1)
            msg.AddFloat(addFloat);
        if(addBool)
            msg.AddBool(sentBool);
        
        Client.Instance.Send(msg);
    }
    [Button]
    void Debug_LeaveRoom(int addInt = -1, float addFloat = -1, string addString = "", bool addBool = false, bool sentBool = false)
    {
        var msg = new OSCMessageOut(Msg.C_LEAVE_ROOM);
        if (addString != string.Empty)
            msg.AddString(addString);
        if (addInt != -1)
            msg.AddInt(addInt);
        if (addFloat != -1)
            msg.AddFloat(addFloat);
        if (addBool)
            msg.AddBool(sentBool);

        Client.Instance.Send(msg);
    }
    [Button]
    void Debug_CloseRoom(int addInt = -1, float addFloat = -1, string addString = "", bool addBool = false, bool sentBool = false)
    {
        var msg = new OSCMessageOut(Msg.C_CLOSE_ROOM);
        if (addString != string.Empty)
            msg.AddString(addString);
        if (addInt != -1)
            msg.AddInt(addInt);
        if (addFloat != -1)
            msg.AddFloat(addFloat);
        if (addBool)
            msg.AddBool(sentBool);

        Client.Instance.Send(msg);
    }
    #endregion 
}

/*
Q & A session – LobbyController

Q1: What is the primary responsibility of LobbyController?
A1: It manages the lobby UI state, sends OSC commands to the server (create/join/leave rooms, start game), 
    and processes incoming OSC messages to update the UI accordingly. It acts as a bridge between the 
    network layer (Client) and the presentation layer (views).

Q2: Why does LobbyController reference multiple view components (LobbyView, HostRoomView, etc.)?
A2: Each view represents a different UI panel (room list, host controls, waiting screen). By referencing 
    them directly, the controller can toggle visibility and update data without the views needing to know 
    about the network or game logic. This follows the MVC/MVP pattern where the controller handles logic.

Q3: What is the purpose of pendingRoomCreation and pendingRoomName?
A3: pendingRoomCreation prevents multiple simultaneous room creation requests. pendingRoomName stores the 
    name of the room being created or joined, used to handle timeouts and avoid race conditions when 
    responses arrive.

Q4: Why are there separate EventBindings for each UI action (CreateRoom, JoinRoom, etc.)?
A4: These bindings allow other parts of the code (e.g., UI buttons) to trigger lobby actions via the 
    EventBus. This decouples the UI from the controller – the UI publishes events, and the controller 
    subscribes to them, making the code more maintainable and testable.

Q5: Why does LobbyController subscribe to OSC listeners in SubscribeOSC() and also to EventBus?
A5: OSC listeners handle incoming server messages (e.g., room list, updates). EventBus subscriptions 
    handle local UI events (button clicks). This separation keeps network handling separate from UI 
    interaction, improving clarity.

Q6: Why use timeouts (Client.Instance.StartTimeout) for create/join room operations?
A6: Network operations may hang if the server doesn't respond. Timeouts ensure that the UI isn't stuck 
    indefinitely – they reset pending states and re-enable buttons so the user can retry.

Q7: Why does OnRoomCreated only show HostRoomView if the host is the current user?
A7: The server sends the room data to all clients after creation. Only the creator should see the host 
    controls; others will later see the waiting view when they join. The check hostName == Client.Instance.Username 
    correctly differentiates the host.

Q8: What does OnRoomJoined do, and why does it update both the room list and the waiting view?
A8: OnRoomJoined is triggered when a non-host joins a room. It updates the room’s participant count in 
    the lobby list, sets the current room, and shows the WaitingForHostView for the joining player. 
    It also cancels the join timeout and re-enables buttons.

Q9: Why is Client.Instance.CurrentRoom set in OnRoomCreated and OnRoomJoined?
A9: This tracks which room the player is currently in. It is used later in OnRoomUpdate and OnRoomClosed 
    to update the appropriate UI panel and manage state transitions.

Q10: What is the role of OnRoomUpdate, and why does it both update existing rooms and create new ones?
A10: The server can broadcast room updates (new participant, host change). OnRoomUpdate handles both 
     scenarios: if the room exists, update its data; otherwise, add it to the dictionary and list. 
     This keeps the lobby list in sync without requiring a full refresh.

Q11: How does OnRoomClosed handle room closure for the current player?
A11: It removes the room from the local dictionary, refreshes the room list UI, and if the closed room 
     is the one the player is currently in, it resets the UI to the lobby state (hides panels, re-enables 
     buttons) and requests a fresh room list.

Q12: Why does OnGameStarted load the Game scene immediately?
A12: When the server signals that the game has started, all clients must transition to the game scene. 
     Using SceneManager.LoadScene is a straightforward way to change scenes. The event ensures the 
     transition happens for all players simultaneously.

Q13: What is the purpose of the three debug methods (Debug_StartGame, Debug_LeaveRoom, Debug_CloseRoom)?
A13: They are inspector?accessible buttons (via [Button]) for manual testing. They send the corresponding 
     OSC messages with optional extra parameters, allowing developers to test server responses without 
     going through the UI flow. They are marked as debug tools.

Q14: Why are optional parameters (addInt, addFloat, etc.) included in the debug methods?
A14: These mimic the original Debug_CloseRoom pattern, allowing testers to append extra arguments to the 
     OSC message. However, they are currently not used meaningfully – they could be for future extensions 
     or to simulate specific server requests.

Q15: Why is OnRoomCreated not setting pendingRoomCreation = false if something fails?
A15: Actually, it does set it to false after cancelling the timeout. The only failure scenario would be 
     a corrupt message or missing data, but the method always resets the flag. The timeout also resets it 
     if the response never arrives.

Q16: Why does OnRoomCreated call view.CreateRoomEntry(room) even though the room might already exist?
A16: It adds the room to the lobby list. In the case of creation, the room is new and should appear. 
     If the room already existed (unlikely), the method would duplicate it, but the server typically 
     sends a list refresh or update instead. The code could be made safer, but it's acceptable for this 
     purpose.

Q17: Why does the controller validate references in ValidateReferences() at Start?
A17: To ensure all required view components are assigned. If any are missing, the controller logs an 
     error and returns early, preventing null reference exceptions later. This also finds references 
     in the scene if not manually assigned.

Q18: How does the controller handle disconnection gracefully?
A18: It subscribes to Client.Instance.OnDisconnected, which triggers loading the main menu and cancelling 
     any pending timeouts. This ensures the user is returned to the login screen on network loss.

Q19: Why is EventBus used instead of direct method calls from UI?
A19: EventBus provides a publish/subscribe pattern that reduces coupling. The UI buttons can simply 
     publish events without knowing which controller or method handles them. This makes it easier to 
     swap or extend functionality.

Q20: What improvements could be made to this controller?
A20: Several: 
     - Use a state machine to manage UI states more robustly.
     - Handle cases where the room list is updated while the user is in a room.
     - Add more error handling for malformed OSC messages.
     - Avoid hardcoding panel references; use a view manager instead.
*/