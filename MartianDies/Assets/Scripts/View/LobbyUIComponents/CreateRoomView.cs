using AniDrag.EventBus;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//DONE
public class CreateRoomView : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_Text pointsCounter;
    [SerializeField] private Slider ptSlider;
    [SerializeField] private Button createRoom;

    public Action<bool> AddListener;
    private void OnEnable()
    {
        createRoom.onClick.AddListener(CreateRoom);
        ptSlider.onValueChanged.AddListener(UpdatePtText);
        AddListener?.Invoke(true);

    }

    private void OnDisable()
    {
        // Clean up listener to avoid memory leaks
        ptSlider.onValueChanged.RemoveListener(UpdatePtText);
        createRoom.onClick.RemoveListener(CreateRoom);
        AddListener?.Invoke(false);
    }

    // Slider value is float, not int
    private void UpdatePtText(float val) => pointsCounter.text = $"PT: {Mathf.RoundToInt(val)}";
    private void CreateRoom() => EventBus<CreateRoom>.Publish(new CreateRoom(nameInput.text, (int)ptSlider.value));


}