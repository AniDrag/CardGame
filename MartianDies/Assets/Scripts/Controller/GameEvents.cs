using AniDrag.EventBus;

/*
 * Networking / UI Event Definitions
 * 
 * Documentation with Chat GPT 5.5 and cleaned up by me.
 * 
 * Purpose:
 * This file contains the small EventBus event structs used by the client side code.
 * These are not OSC messages by themselves.
 * They are local Unity events used inside the client to communicate between UI,
 * controllers, and view scripts without needing direct references everywhere.
 * 
 * Example:
 * A button can publish CreateRoom.
 * A lobby controller can listen for CreateRoom and then send the real OSC message to the server.
 * 
 * Important:
 * These structs only carry data inside the Unity client.
 * The server does not receive these directly unless another script converts them into OSC messages.
 */

#region General Events

public struct Connect : IEvBusEvent { }
public struct Disconnect : IEvBusEvent { }

#endregion

#region Main Menu Events

/// <summary>
/// Local EventBus event.
/// 
/// Meaning:
/// Opens the malicious tester/debug tester UI.
/// 
/// Data:
/// No extra data.
/// 
/// Used by:
/// A debug button in the main menu.
/// </summary>
public struct OpenMaliciousTester : IEvBusEvent { }

public struct IncorrectUsername : IEvBusEvent
{
    public string errorMsg;

    public IncorrectUsername(string pErrorMsg)
    {
        errorMsg = pErrorMsg;
    }
}

/// <summary>
/// Local EventBus event.
/// 
/// Meaning:
/// The server IP entered by the player is not valid.
/// 
/// Data received:
/// errorMsg = text explaining what is wrong with the IP.
public struct IncorrectIP : IEvBusEvent
{
    public string errorMsg;

    public IncorrectIP(string pErrorMsg)
    {
        errorMsg = pErrorMsg;
    }
}

#endregion

#region Lobby Events

/// <summary>
/// Local EventBus event.
/// 
/// Meaning:
/// Tells lobby buttons if they should be clickable or not.
/// 
/// Data received:
/// isEnabled = true means buttons can be used.
/// isEnabled = false means buttons are disabled.
/// 
/// Usually used when:
/// The client is waiting for a server response and should not send duplicate requests.
/// </summary>
public struct EnableButtons : IEvBusEvent
{
    public bool isEnabled;

    public EnableButtons(bool pIsEnabled)
    {
        isEnabled = pIsEnabled;
    }
}

public struct RefreshRooms : IEvBusEvent { }

public struct UpdateRoomParticipants : IEvBusEvent
{
    public int participants;

    public UpdateRoomParticipants(int pNewCount)
    {
        participants = pNewCount;
    }
}

/// <summary>
/// Local EventBus event.
/// 
/// Meaning:
/// The user wants to join a selected room.
/// 
/// Data received:
/// data = the room data model for the selected room.
/// 
/// Expected data inside RoomDataModel:
/// This depends on the RoomDataModel class,
/// but it usually contains things like room name, host, player count, and point goal.
/// 
/// Usually converted into:
/// A client join room OSC message.
/// </summary>
public struct JoinRoom : IEvBusEvent
{
    public RoomDataModel data;

    public JoinRoom(RoomDataModel pData)
    {
        data = pData;
    }
}

public struct LeaveRoom : IEvBusEvent { }

/// <summary>
/// Local EventBus event.
/// 
/// Meaning:
/// The user wants to create a new room.
/// 
/// Data received:
/// roomName = name of the room the user typed.
/// pointGoal = score needed to win the match.
/// 
/// Example:
/// roomName = "TestRoom"
/// pointGoal = 5000
/// 
/// Usually converted into:
/// A client create room OSC message.
/// </summary>
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

/// <summary>
/// Local EventBus event.
/// Usually used by:
/// Lobby UI after the server confirms room creation.
/// </summary>
public struct RoomCreated : IEvBusEvent
{
    public RoomDataModel data;

    public RoomCreated(RoomDataModel pData)
    {
        data = pData;
    }
}

public struct StartGame : IEvBusEvent { }

public struct CloseHostedRoom : IEvBusEvent { }

#endregion

#region Game Events

/// <summary>
/// Local EventBus event.
/// 
/// Meaning:
/// The player selected a dice type in the game UI.
/// 
/// Data received:
/// diceType = the dice type or dice value that was selected.
/// 
/// Example:
/// diceType = 1
/// 
/// Usually converted into:
/// Msg.C_SELECT_DICE with:
/// [0] int diceType
/// </summary>
public struct SelectedDiceType : IEvBusEvent
{
    public int diceType;

    public SelectedDiceType(int pDiceType)
    {
        diceType = pDiceType;
    }
}

/// <summary>
/// Local EventBus event.
/// 
/// Meaning:
/// Response saying if a dice selection was allowed.
/// 
/// Data received:
/// allowed = true if the selected dice is allowed.
/// allowed = false if the selected dice is not allowed.
/// 
/// Note:
/// This is a local event.
/// In the current flow, dice validation should come from the server.
/// </summary>
public struct SelectDiceReply : IEvBusEvent
{
    public bool allowed;

    public SelectDiceReply(bool pAllowed)
    {
        allowed = pAllowed;
    }
}

public struct GameAnnouncement : IEvBusEvent
{
    public string msg;

    public GameAnnouncement(string pMsg)
    {
        msg = pMsg;
    }
}

/// <summary>
/// Local EventBus event.
/// 
/// Meaning:
/// The player answered the roll again or stop prompt.
/// 
/// Data received:
/// doReRoll = true means the player wants to roll again.
/// doReRoll = false means the player wants to stop/bank.
/// 
/// Usually converted into:
/// Msg.C_STAKE_ANSWER with:
/// [0] bool doReRoll
/// </summary>
public struct StakeRoll : IEvBusEvent
{
    public bool doReRoll;

    public StakeRoll(bool pDoReRoll)
    {
        doReRoll = pDoReRoll;
    }
}

/// <summary>
/// Local EventBus event.
/// 
/// Meaning:
/// Round result text should be shown in the UI.
/// 
/// Data received:
/// msg = result text from the round.
/// 
/// Example:
/// msg = "Nik gained 500 points this turn."
/// </summary>
public struct RoundResults : IEvBusEvent
{
    /// <summary>
    /// Text that describes the result of the round or turn.
    /// </summary>
    public string msg;

    /// <summary>
    /// Creates the round results event.
    /// </summary>
    public RoundResults(string pMsg)
    {
        msg = pMsg;
    }
}

#endregion