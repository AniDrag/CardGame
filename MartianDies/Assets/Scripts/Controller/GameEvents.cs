using AniDrag.EventBus;

#region General Events

public struct Connect : IEvBusEvent { }

public struct Disconnect : IEvBusEvent { }

#endregion

#region Main Menu Events

public struct OpenMaliciousTester : IEvBusEvent { }

public struct IncorrectUsername : IEvBusEvent
{
    public string errorMsg;

    public IncorrectUsername(string pErrorMsg)
    {
        errorMsg = pErrorMsg;
    }
}

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

public struct JoinRoom : IEvBusEvent
{
    public RoomDataModel data;

    public JoinRoom(RoomDataModel pData)
    {
        data = pData;
    }
}

public struct LeaveRoom : IEvBusEvent { }

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

    public RoomCreated(RoomDataModel pData)
    {
        data = pData;
    }
}

public struct StartGame : IEvBusEvent { }

public struct CloseHostedRoom : IEvBusEvent { }

#endregion

#region Game Events

public struct SelectedDiceType : IEvBusEvent
{
    public int diceType;

    public SelectedDiceType(int pDiceType)
    {
        diceType = pDiceType;
    }
}

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

public struct StakeRoll : IEvBusEvent
{
    public bool doReRoll;

    public StakeRoll(bool pDoReRoll)
    {
        doReRoll = pDoReRoll;
    }
}

public struct RoundResults : IEvBusEvent
{
    public string msg;

    public RoundResults(string pMsg)
    {
        msg = pMsg;
    }
}

#endregion