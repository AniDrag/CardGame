using AniDrag.EventBus;
using AniDrag.UI.Animations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
// DONE
public class WaitingForHostView : MonoBehaviour
{
    [SerializeField] private Button leaveRoom;
    [SerializeField] private TMP_Text WaitingText;
    [SerializeField] public TMP_Text roomDetails;
    [SerializeField] private TextCoroutineAnimator animation;

    EventBinding<UpdateRoomParticipants> updateRoomParticipantsBinding;
    EventBinding<JoinRoom> joinRoomBinding;


    private string _roomName;
    private int _participants;
    private int _pointGoal;
    private void OnEnable()
    {
        animation.StartAnimation();
        leaveRoom.onClick.AddListener(() => EventBus<LeaveRoom>.Publish(new LeaveRoom()));

        updateRoomParticipantsBinding = new EventBinding<UpdateRoomParticipants>(UpdateParticipantsInRoom);
        EventBus<UpdateRoomParticipants>.Subscribe(updateRoomParticipantsBinding);

        joinRoomBinding = new EventBinding<JoinRoom>(OnJoin);
        EventBus<JoinRoom>.Subscribe(joinRoomBinding);

    }
    public void OnJoin(JoinRoom e)
    {
        _roomName = e.roomName;
        _participants = e.participants;
        _pointGoal = e.pointGoal;
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
        leaveRoom.onClick.RemoveListener(() => EventBus<LeaveRoom>.Publish(new LeaveRoom()));
        EventBus<JoinRoom>.Unsubscribe(joinRoomBinding);
        EventBus<UpdateRoomParticipants>.Unsubscribe(updateRoomParticipantsBinding);
    }
}
