using AniDrag.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HostRoomView : MonoBehaviour
{
    [SerializeField] TMP_Text roomDetailsText;
    public Button startGame;
    public Button closeRoom;

    EventBinding<UpdateRoomParticipants> updateRoomParticipantsBinding;

    string _roomName;
    int _points;

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

        startGame.onClick.AddListener(EventBus_Publish_StartGame);
        closeRoom.onClick.AddListener(EventBus_Publish_CloseHostedRoom);

        updateRoomParticipantsBinding = new EventBinding<UpdateRoomParticipants>(UpdateParticipantsInRoom);
        EventBus<UpdateRoomParticipants>.Subscribe(updateRoomParticipantsBinding);
    }

    private void OnDisable()
    {
        if (startGame != null)
            startGame.onClick.RemoveListener(EventBus_Publish_StartGame);
        if (closeRoom != null)
            closeRoom.onClick.RemoveListener(EventBus_Publish_CloseHostedRoom);

        EventBus<UpdateRoomParticipants>.Unsubscribe(updateRoomParticipantsBinding);
    }

    /// <summary>
    /// When showing the room aka on enabeling it
    /// </summary>
    /// <param name="roomName"></param>
    /// <param name="participants"></param>
    /// <param name="pointGoal"></param>
    public void OnCreate(string roomName, int participants, int pointGoal)
    {
        _roomName = roomName;
        _points = participants;
        roomDetailsText.text = $"{_roomName}\n {participants} / 4\nPonit Goal: {_points}";
    }
    public void UpdateParticipantsInRoom(UpdateRoomParticipants e) => OnCreate(_roomName, e.participants, _points);

    void EventBus_Publish_StartGame() => EventBus<StartGame>.Publish(new StartGame());
    void EventBus_Publish_CloseHostedRoom() => EventBus<CloseHostedRoom>.Publish(new CloseHostedRoom());
}
