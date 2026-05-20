using AniDrag.EventBus;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HostRoomView : MonoBehaviour
{
    [SerializeField] private TMP_Text roomDetailsText;
    [SerializeField] private Button startGame;
    [SerializeField] private Button closeRoom;

    EventBinding<UpdateRoomParticipants> updateRoomParticipantsBinding;
    EventBinding<RoomCreated> roomCreatedBinding;

    string _roomName;
    int _points;
    int participants;

    private void OnEnable()
    {
        if (startGame == null)
        {
            Client.Log($"Null Reference: Button [ startGame ] not found on: {this.gameObject.name}");
            return;
        }
        if (closeRoom == null)
        {
            Client.Log($"Null Reference: Button [ closeRoom ] not found on: {this.gameObject.name}");
            return;
        }
        startGame.onClick.AddListener(() => EventBus<StartGame>.Publish(new StartGame()));
        closeRoom.onClick.AddListener(() => EventBus<CloseHostedRoom>.Publish(new CloseHostedRoom()));
        updateRoomParticipantsBinding = new EventBinding<UpdateRoomParticipants>(UpdateParticipantsInRoom);
        EventBus<UpdateRoomParticipants>.Subscribe(updateRoomParticipantsBinding);
        roomCreatedBinding = new EventBinding<RoomCreated>(OnCreate);
        EventBus<RoomCreated>.Subscribe(roomCreatedBinding);

        
    }


    private void OnDisable()
    {
        if (startGame != null)
            startGame.onClick.RemoveListener(EventBus_Publish_StartGame);
        if (closeRoom != null)
            closeRoom.onClick.RemoveListener(EventBus_Publish_CloseHostedRoom);

        EventBus<UpdateRoomParticipants>.Unsubscribe(updateRoomParticipantsBinding);
        
    }

    public void OnCreate(RoomCreated e)
    {
        _roomName = e.data.roomName;
        _points = e.data.pointGoal;
        participants = 1;

        roomDetailsText.text = $"{_roomName}\n {participants} / 4\nPonit Goal: {_points}";
    }
    public void UpdateParticipantsInRoom(UpdateRoomParticipants e)
    {
        participants = e.participants;
        roomDetailsText.text = $"{_roomName}\n {participants} / 4\nPonit Goal: {_points}";
    }

    void EventBus_Publish_StartGame() => EventBus<StartGame>.Publish(new StartGame());
    void EventBus_Publish_CloseHostedRoom() => EventBus<CloseHostedRoom>.Publish(new CloseHostedRoom());
}
