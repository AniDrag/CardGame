using AniDrag.EventBus;



#region Lobby Events
public struct DisableButtons : IEvBusEvent
{
    public bool isEnabled;
    public DisableButtons(bool pIsEnabled) => isEnabled = pIsEnabled;
}
public struct RefreshRooms : IEvBusEvent { }
public struct UpdateRoomParticipants : IEvBusEvent
{
    public int participants;
    public UpdateRoomParticipants(int pNewCount) => participants = pNewCount;
}
public struct JoinRoom : IEvBusEvent
{
    public RoomEntryData data;
    public JoinRoom(RoomEntryData pData)
    {
        data = pData;
    }
}

#endregion

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
    public bool succes;
    public RoomEntryData data;
    public RoomCreated(bool pSucces, RoomEntryData pData)
    {
        succes = pSucces;
        data = pData;
    }
}
#endregion

#region HostRoomEvents
public struct StartGame : IEvBusEvent { }
public struct CloseHostedRoom : IEvBusEvent { }
#endregion

// Only for Hosting room or when waiting room


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
