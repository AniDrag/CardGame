using AniDrag.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomView : MonoBehaviour
{
    public TMP_InputField nameInput;
    public Slider ptSlider;
    [SerializeField] private TMP_Text pointsCounter;
    public Button createRoom;

    private void OnEnable()
    {
        createRoom.onClick.AddListener(CreateRoom);
        ptSlider.onValueChanged.AddListener(UpdatePtText);
    }

    private void OnDisable()
    {
        // Clean up listener to avoid memory leaks
        ptSlider.onValueChanged.RemoveListener(UpdatePtText);
        createRoom.onClick.RemoveListener(CreateRoom);
    }

    // Slider value is float, not int
    private void UpdatePtText(float val)
    {
        pointsCounter.text = $"PT: {Mathf.RoundToInt(val)}";
    }

    void CreateRoom()
    {
        EventBus<CreateRoom>.Publish(new CreateRoom(nameInput.text, (int)ptSlider.value));
    }
}