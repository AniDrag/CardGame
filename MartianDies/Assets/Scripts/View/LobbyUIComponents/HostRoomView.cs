using AniDrag.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HostRoomView : MonoBehaviour
{
    #region View References

    [SerializeField] private TMP_Text roomDetailsText;
    [SerializeField] private Button startGame;
    [SerializeField] private Button closeRoom;

    #endregion

    #region State

    private RoomDataModel roomData;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        RegisterButtons();
    }

    private void OnDisable()
    {
        UnregisterButtons();
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        if (startGame != null)
            startGame.onClick.AddListener(OnStartGameButtonClicked);

        if (closeRoom != null)
            closeRoom.onClick.AddListener(OnCloseRoomButtonClicked);
    }

    private void UnregisterButtons()
    {
        if (startGame != null)
            startGame.onClick.RemoveListener(OnStartGameButtonClicked);

        if (closeRoom != null)
            closeRoom.onClick.RemoveListener(OnCloseRoomButtonClicked);
    }

    #endregion

    #region UI Events

    private void OnStartGameButtonClicked()
    {
        Debug.Log("Start Game button clicked");
        EventBus<StartGame>.Publish(new StartGame());
    }

    private void OnCloseRoomButtonClicked()
    {
        Debug.Log("Close Room button clicked");
        EventBus<CloseHostedRoom>.Publish(new CloseHostedRoom());
    }

    #endregion

    #region Display

    public void SetRoomData(RoomDataModel data)
    {
        roomData = data;
        UpdateDisplay();
    }

    public void UpdateParticipants(int participants)
    {
        if (roomData == null)
            return;

        roomData.participantCount = participants;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (roomData == null || roomDetailsText == null)
            return;

        roomDetailsText.text =
            $"{roomData.roomName}\n" +
            $"{roomData.participantCount} / 4\n" +
            $"Point Goal: {roomData.pointGoal}";
    }

    #endregion
}