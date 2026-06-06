using System;
using TMPro;
using AniDrag.EventBus;
using UnityEngine;
using UnityEngine.UI;

public class RoomEntryView : MonoBehaviour
{
    [SerializeField] private TMP_Text roomDetails;
    [SerializeField] private TMP_Text participants;
    [SerializeField] private Button joinRoomButton;
    private RoomDataModel data;

    private EventBinding<EnableButtons> disableButtonsBinding;

    // Use a named method instead of a lambda – so RemoveListener works
    private void OnJoinButtonClicked()
    {
        EventBus<JoinRoom>.Publish(new JoinRoom(data));
    }

    private void OnEnable()
    {
        // Subscribe to event bus only once
        if (disableButtonsBinding == null)
        {
            disableButtonsBinding = new EventBinding<EnableButtons>(DisableButtons);
            EventBus<EnableButtons>.Subscribe(disableButtonsBinding);
        }

        // Add button listener (using named method)
        joinRoomButton.onClick.AddListener(OnJoinButtonClicked);
    }

    private void OnDisable()
    {
        // Remove button listener (works because it's the same method)
        joinRoomButton.onClick.RemoveListener(OnJoinButtonClicked);
    }

    private void OnDestroy()
    {
        // Unsubscribe from event bus
        if (disableButtonsBinding != null)
        {
            EventBus<EnableButtons>.Unsubscribe(disableButtonsBinding);
            disableButtonsBinding = null;
        }
    }

    private void DisableButtons(EnableButtons e)
    {
        if (joinRoomButton != null)
            joinRoomButton.interactable = e.isEnabled;
    }

    public void Initialize(RoomDataModel pData)
    {
        data = pData;
        
        roomDetails.text = $"{data.roomName}    Goal: {data.pointGoal}pt";
        UpdateParticipants(data.participantCount);
    }

    public void UpdateParticipants(int count)
    {
        participants.text = $"{count} / 4";
    }
}