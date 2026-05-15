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

// Only for Hosting room or when waiting room

#region Game Events
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
public struct RoundResults : IEvBusEvent
{
    public string msg;
    public RoundResults(string pMsg) => msg = pMsg;
}

#endregion
