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
        // Add listener (don't invoke)
        ptSlider.onValueChanged.AddListener(UpdatePtText);
    }

    private void OnDisable()
    {
        // Clean up listener to avoid memory leaks
        ptSlider.onValueChanged.RemoveListener(UpdatePtText);
    }

    // Slider value is float, not int
    private void UpdatePtText(float val)
    {
        pointsCounter.text = $"PT: {Mathf.RoundToInt(val)}";
    }
}