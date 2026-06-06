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
