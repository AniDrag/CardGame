using AniDrag.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomView : MonoBehaviour
{
    #region View References

    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_Text pointsCounter;
    [SerializeField] private Slider ptSlider;
    [SerializeField] private Button createRoom;
    [SerializeField] private Button closeButton;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        RegisterButtons();
        RegisterSlider();

        UpdatePointText(ptSlider.value);
    }

    private void OnDisable()
    {
        UnregisterButtons();
        UnregisterSlider();
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        if (createRoom != null)
            createRoom.onClick.AddListener(OnCreateRoomButtonClicked);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseView);
    }

    private void UnregisterButtons()
    {
        if (createRoom != null)
            createRoom.onClick.RemoveListener(OnCreateRoomButtonClicked);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseView);
    }

    private void RegisterSlider()
    {
        ptSlider.onValueChanged.AddListener(UpdatePointText);
    }

    private void UnregisterSlider()
    {
        ptSlider.onValueChanged.RemoveListener(UpdatePointText);
    }

    #endregion

    #region UI Events

    private void OnCreateRoomButtonClicked()
    {
        string roomName = nameInput.text;
        int pointGoal = Mathf.RoundToInt(ptSlider.value);

        EventBus<CreateRoom>.Publish(new CreateRoom(roomName, pointGoal));
    }

    private void UpdatePointText(float value)
    {
        pointsCounter.text = $"PT: {Mathf.RoundToInt(value)}";
    }
    private void CloseView()
    {
        EnableView(false);
    }

    #endregion

    #region Public Methods

    public void EnableView(bool enabled)
    {
        gameObject.SetActive(enabled);
    }

    #endregion
}