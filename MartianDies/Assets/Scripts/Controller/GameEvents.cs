using AniDrag.EventBus;


public struct Connect : IEvBusEvent { }
public struct Disconnect : IEvBusEvent { }

#region Main Menu Events
public struct IncorrectUsername : IEvBusEvent 
{
    public string errorMsg;
    public IncorrectUsername(string pErrorMsg) => errorMsg = pErrorMsg;
}
public struct IncorrectIP : IEvBusEvent 
{
    public string errorMsg;
    public IncorrectIP(string pErrorMsg) => errorMsg = pErrorMsg;
}
#endregion

#region Lobby Events
public struct EnableButtons : IEvBusEvent
{
    public bool isEnabled;
    public EnableButtons(bool pIsEnabled) => isEnabled = pIsEnabled;
}
public struct RefreshRooms : IEvBusEvent { }
public struct UpdateRoomParticipants : IEvBusEvent
{
    public int participants;
    public UpdateRoomParticipants(int pNewCount) => participants = pNewCount;
}
public struct JoinRoom : IEvBusEvent
{
    public RoomDataModel data;
    public JoinRoom(RoomDataModel pData)
    {
        data = pData;
    }
}



#region Waiting for host Events
public struct LeaveRoom : IEvBusEvent { }

#endregion

#region Create Room Events
public struct CreateRoom : IEvBusEvent
{
    public string roomName;
    public int pointGoal;
    public CreateRoom(string pName, int pPointGoal)
    {
        roomName = pName;
        pointGoal = pPointGoal;
    }

}
public struct RoomCreated : IEvBusEvent
{
    public RoomDataModel data;
    public RoomCreated(bool pSucces, RoomDataModel pData)
    {
        data = pData;
    }
}
#endregion

#region HostRoomEvents
public struct StartGame : IEvBusEvent { }
public struct CloseHostedRoom : IEvBusEvent { }
#endregion
#endregion

#region Game Events

/// <summary>
/// Send dice type -> store selection type and coutn.
/// Wait for recive :
///     Success remove dice from rolled view add to designated spot.
///     Faliure: msg Invalid dice.
///     
/// </summary>


public struct SelectedDiceType : IEvBusEvent
{
    public int diceType;
    public SelectedDiceType(int pDiceType) => diceType = pDiceType;
}
public struct SelectDiceReplie : IEvBusEvent
{
    public bool allowed;
    public SelectDiceReplie(bool pAllowed) => allowed = pAllowed;
}

public struct GameAnnouncment: IEvBusEvent
{
    public string msg;
    public GameAnnouncment(string pMsg) => msg = pMsg; 
}

public struct StakeRoll : IEvBusEvent
{
    public bool doReRoll;
    public StakeRoll(bool pDoReRoll) => doReRoll = pDoReRoll;
}

public struct RoundResults : IEvBusEvent
{
    public string msg;
    public RoundResults(string pMsg) => msg = pMsg;
}

#endregion

/*
Q & A session – Event Bus Definitions

Q1: Why use a struct-based event system (AniDrag.EventBus) instead of UnityEvents or delegates?
A1: Structs are lightweight and avoid heap allocation. The EventBus pattern allows decoupled 
    communication between components – any script can publish an event, and any other script can 
    subscribe without direct references. This improves modularity and testability.

Q2: Why separate events into regions (Main Menu, Lobby, Game)?
A2: Regions organise events by their functional area, making the codebase easier to navigate. 
    It also clarifies the lifecycle of events – for example, Lobby events are only relevant when 
    in the lobby scene.

Q3: Why use parameterised constructors for events like IncorrectUsername?
A3: Events often need to carry data (error messages, room data, participant counts). Providing 
    a constructor ensures that the data is set at creation time, making the event immutable and 
    thread-safe (if needed).

Q4: Why use structs with public fields instead of properties?
A4: Structs with public fields are simpler and more performant for data containers. Since events 
    are typically short-lived and passed by value, this design reduces overhead. However, it's a 
    design choice; properties could also be used.

Q5: What is the purpose of the Connect and Disconnect events?
A5: These are empty event structs (no data) that signal when the client connects or disconnects. 
    Other systems can subscribe to these to react (e.g., UI updates, enabling/disabling buttons) 
    without polling Client.IsConnected.

Q6: Why have both CreateRoom and RoomCreated events?
A6: CreateRoom is a command event (sent by the UI when the user clicks "Create Room"). RoomCreated 
    is a response event (published when the server confirms room creation). This separates intent 
    from result, allowing async workflows.

Q7: Why does RoomCreated carry a bool success and RoomDataModel?
A7: The bool indicates whether the operation succeeded. The RoomDataModel provides details 
    (room name, participants, etc.) on success. This allows the UI to update or show errors 
    based on the outcome.

Q8: Why are there separate events for EnableButtons, RefreshRooms, UpdateRoomParticipants?
A8: These are UI update events. EnableButtons toggles interactivity (e.g., during loading). 
    RefreshRooms triggers a list refresh in the lobby. UpdateRoomParticipants updates the 
    participant count display. Separating them allows fine?grained UI updates without 
    over?refreshing.

Q9: Why is the JoinRoom event carrying RoomDataModel instead of just a room ID?
A9: The event is likely published when a room is selected and its data is already available. 
    Passing the full model avoids a subsequent data fetch and simplifies the subscriber 
    (e.g., a panel that shows room details).

Q10: Why have both LeaveRoom and CloseHostedRoom?
A10: LeaveRoom is for a participant leaving a room (non?host). CloseHostedRoom is for the host 
     to close the room entirely, potentially ending the lobby session. They serve different roles.

Q11: What is the purpose of StartGame?
A11: This event is published when the host starts the game. Subscribers (e.g., scene loader, 
     game manager) can react by transitioning to the game scene or initialising game state.

Q12: Why are game events like SelectedDiceType and SelectDiceReplie structured as request/reply?
A12: In a game, a player selects a dice type, and the server (or game logic) must validate and 
     respond. The request event (SelectedDiceType) and reply event (SelectDiceReplie) decouple 
     the UI from the logic, allowing asynchronous validation and state updates.

Q13: Why does GameAnnouncement carry a string message?
A13: This is a generic event for broadcasting game messages (e.g., "Roll the dice!", "Invalid move"). 
     It allows the UI to display notifications without knowing the specific game state.

Q14: What does StakeRoll represent and why include doReRoll?
A14: StakeRoll is likely an event when a player stakes a roll (maybe a double down or re?roll?). 
     The doReRoll bool indicates whether the player wants to re?roll (perhaps after a failed attempt). 
     This allows the game logic to handle the action accordingly.

Q15: Why use RoundResults with just a string message?
A15: At the end of a round, the game may need to display a summary (e.g., winner, score). A simple 
     string message is flexible – it can contain formatted text. More structured data could be 
     added later if needed.

Q16: Why are all events defined as structs and not classes?
A16: Structs are value types, which means they are passed by copy. In the context of event 
     publishing, this avoids reference sharing and accidental mutation. They also typically have 
     lower memory overhead and are more cache?friendly for frequent event dispatching.

Q17: How does this event system support the "waiting for host" state?
A17: Events like LeaveRoom, CloseHostedRoom, and StartGame control transitions in the host?client 
     model. The UI can listen to these events to show/hide panels (e.g., waiting for host to start).

Q18: Why are there no events for joining/leaving individual players?
A18: The existing UpdateRoomParticipants event updates the participant count, but if individual 
     player data is needed, additional events like PlayerJoined, PlayerLeft could be added. 
     The current design is minimal but can be extended.

Q19: What about error handling – why no generic Error event?
A19: Errors are currently handled via specific events (IncorrectUsername, IncorrectIP) which carry 
     an error message. This is more explicit and avoids a catch?all error event that might be 
     ambiguous. Other errors could be added similarly.

Q20: How does this event bus integrate with the Client class?
A20: The Client publishes events like Connect, Disconnect, RoomCreated, etc. when it receives OSC 
     messages from the server. Other scripts (UI, game managers) subscribe to these events to react. 
     This completely decouples the network layer from presentation/logic.
*/