using UnityEngine;
using UnityEngine.UI;
using AniDrag.UI.Animations;
using TMPro;

public class WaitingForHostView : MonoBehaviour
{
    public Button leaveRoom;
    [SerializeField] private TMP_Text WaitingText;
    [SerializeField] public TMP_Text roomDetails; // RoomName\n Point Goal: xxpt \n participants / 4;
    [SerializeField] private TextCoroutineAnimator animation;


    private string _roomName;
    private int _participants;
    private int _pointGoal;

    public void OnJoin(string roomName, int participants, int pointGoal)
    {
        _roomName = roomName;
        _participants = participants;
        _pointGoal = pointGoal;
        UpdateDisplay();
    }

    public void UpdateParticipantsInRoom(UpdateRoomParticipants e)
    {
        _participants = e.participants;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        roomDetails.text = $"{_roomName}\n{_participants} / 4\nPoint Goal: {_pointGoal}";
    }

    private void OnDisable()
    {
        animation.StopAnimation();
    }
}
