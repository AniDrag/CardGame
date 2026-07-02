using AniDrag.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomEntryView : MonoBehaviour
{
    #region View References

    [SerializeField] private TMP_Text roomDetails;
    [SerializeField] private TMP_Text participants;
    [SerializeField] private Button joinRoomButton;

    #endregion

    #region State

    private RoomDataModel data;

    #endregion

    #region Event Bindings

    private EventBinding<EnableButtons> enableButtonsBinding;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        RegisterButtons();
        RegisterEvents();
    }

    private void OnDisable()
    {
        UnregisterButtons();
    }

    private void OnDestroy()
    {
        UnregisterEvents();
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        joinRoomButton.onClick.AddListener(OnJoinButtonClicked);
    }

    private void UnregisterButtons()
    {
        joinRoomButton.onClick.RemoveListener(OnJoinButtonClicked);
    }

    private void RegisterEvents()
    {
        if (enableButtonsBinding != null)
            return;

        enableButtonsBinding = new EventBinding<EnableButtons>(SetInteractable);
        EventBus<EnableButtons>.Subscribe(enableButtonsBinding);
    }

    private void UnregisterEvents()
    {
        if (enableButtonsBinding == null)
            return;

        EventBus<EnableButtons>.Unsubscribe(enableButtonsBinding);
        enableButtonsBinding = null;
    }

    #endregion

    #region UI Events

    private void OnJoinButtonClicked()
    {
        if (data == null)
            return;

        EventBus<JoinRoom>.Publish(new JoinRoom(data));
    }

    #endregion

    #region Display

    public void Initialize(RoomDataModel roomData)
    {
        data = roomData;
        UpdateDisplay();
    }

    public void UpdateData(RoomDataModel roomData)
    {
        data = roomData;
        UpdateDisplay();
    }

    public void UpdateParticipants(int count)
    {
        if (data != null)
            data.participantCount = count;

        if (participants != null)
            participants.text = $"{count} / 4";
    }

    private void UpdateDisplay()
    {
        if (data == null)
            return;

        if (roomDetails != null)
            roomDetails.text = $"{data.roomName}    Goal: {data.pointGoal}pt";

        UpdateParticipants(data.participantCount);
    }

    #endregion

    #region Button State

    private void SetInteractable(EnableButtons e)
    {
        if (joinRoomButton != null)
            joinRoomButton.interactable = e.isEnabled;
    }

    #endregion
}