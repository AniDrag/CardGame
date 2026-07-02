using AniDrag.EventBus;
using AniDrag.UI.Animations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaitingForHostView : MonoBehaviour
{
    #region View References

    [SerializeField] private Button leaveRoom;
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] public TMP_Text roomDetails;
    [SerializeField] private TextCoroutineAnimator animation;

    #endregion

    #region State

    private RoomDataModel data;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        RegisterButtons();

        if (animation != null)
            animation.StartAnimation();
    }

    private void OnDisable()
    {
        UnregisterButtons();

        if (animation != null)
            animation.StopAnimation();
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        if (leaveRoom != null)
            leaveRoom.onClick.AddListener(OnLeaveRoomButtonClicked);
    }

    private void UnregisterButtons()
    {
        if (leaveRoom != null)
            leaveRoom.onClick.RemoveListener(OnLeaveRoomButtonClicked);
    }

    #endregion

    #region UI Events

    private void OnLeaveRoomButtonClicked()
    {
        EventBus<LeaveRoom>.Publish(new LeaveRoom());
    }

    #endregion

    #region Display

    public void SetRoomData(RoomDataModel roomData)
    {
        data = roomData;
        UpdateDisplay();
    }

    public void UpdateParticipants(int participants)
    {
        if (data == null)
            return;

        data.participantCount = participants;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (data == null || roomDetails == null)
            return;

        roomDetails.text =
            $"{data.roomName}\n" +
            $"{data.participantCount} / 4\n" +
            $"Point Goal: {data.pointGoal}";
    }

    #endregion
}