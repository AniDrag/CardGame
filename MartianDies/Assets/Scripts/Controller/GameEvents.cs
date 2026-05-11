using AniDrag.EventBus;

/// <summary>
/// For all UI buttons to recive info that they should be dissabled
/// </summary>
public struct DisableButtons : IEvBusEvent
{
    public bool isEnabled;
    public DisableButtons(bool pIsEnabled) => isEnabled = pIsEnabled;
}
public struct CreateRoom : IEvBusEvent 
{
    public string roomName;
    public int pointGoal;
    public CreateRoom(string pName,int pPointGoal)
    {
        roomName = pName;
        pointGoal = pPointGoal;
    }

}

public struct RefreshRooms : IEvBusEvent { }
public struct StartGame : IEvBusEvent { }
public struct CloseHostedRoom : IEvBusEvent { }
public struct LeaveRoom : IEvBusEvent { }

// Only for Hosting room or when waiting room
public struct UpdateRoomParticipants : IEvBusEvent
{
    public int participants;
    public UpdateRoomParticipants(int pNewCount) => participants = pNewCount;
}

public struct SelectedDiceType : IEvBusEvent
{
    public int diceType;
    public SelectedDiceType(int pDiceType) => diceType = pDiceType;
}

public struct StakeRoll : IEvBusEvent
{
    public bool doReRoll;
    public StakeRoll(bool pDoReRoll) => doReRoll = pDoReRoll;
}
