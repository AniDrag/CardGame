using UnityEngine;

// this is only for the actual data to be passed around. all we need 
public class RoomDataModel
{
    public string roomName;
    public string host;
    public int pointGoal;
    public int participantCount;
    public bool isInGame;
    public RoomDataModel( string pRoomName, string pHostName, int pPointGoal, int pCurrParticipants, bool pIsInGame = false)
    {
        roomName = pRoomName;
        host = pHostName;
        pointGoal = pPointGoal;
        participantCount = pCurrParticipants;
        isInGame = pIsInGame;
    }
}
