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

    private RoomDataModel data;
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
        data = e.data;
        UpdateDisplay();
    }
    public void SetRoomData(RoomDataModel roomData)
    {
        data = roomData;
    }
    public void UpdateParticipantsInRoom(UpdateRoomParticipants e)
    {
        data.participantCount = e.participants;
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        roomDetails.text = $"{data.roomName}\n{data.participantCount} / 4\nPoint Goal: {data.pointGoal}";
    }

    private void OnDisable()
    {
        animation.StopAnimation();
        leaveRoom.onClick.RemoveListener(() => EventBus<LeaveRoom>.Publish(new LeaveRoom()));
        EventBus<JoinRoom>.Unsubscribe(joinRoomBinding);
        EventBus<UpdateRoomParticipants>.Unsubscribe(updateRoomParticipantsBinding);
    }
}
