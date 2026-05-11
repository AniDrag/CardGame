using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomEntryView : MonoBehaviour
{
    [SerializeField] private TMP_Text roomDetails;
    [SerializeField] private TMP_Text participants;
    public Button joinBtn;
    private int roomID;

    public void Initialize(string roomName, int pointGoal, int participantCount, int id)
    {
        roomID = id;
        roomDetails.text = $"{roomName}    Goal: {pointGoal}pt";
        UpdateParticipants(participantCount);
    }

    public void UpdateParticipants(int count)
    {
        participants.text = $"{count} / 4";
    }
}
[Serializable]
public class RoomEntryData
{
    public int ID;
    public string roomName;
    public string host;
    public int pointGoal;
    public int currParticipants;
    public RoomEntryData(int pId, string pRoomName, string pHostName, int pPointGoal, int pCurrParticipants)
    {
        ID = pId;
        roomName = pRoomName;
        host = pHostName;
        pointGoal = pPointGoal;
        currParticipants = pCurrParticipants;
    }
}