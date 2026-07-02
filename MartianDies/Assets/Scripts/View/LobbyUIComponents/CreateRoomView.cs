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
        createRoom.onClick.AddListener(OnCreateRoomButtonClicked);
    }

    private void UnregisterButtons()
    {
        createRoom.onClick.RemoveListener(OnCreateRoomButtonClicked);
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

    #endregion

    #region Public Methods

    public void EnableView(bool enabled)
    {
        gameObject.SetActive(enabled);
    }

    #endregion
}