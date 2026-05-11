using AniDrag.EventBus;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DiceView : MonoBehaviour
{
    private Button dieButton;
    private Image dieImage;
    [SerializeField]private DiceType dieType;

    [Header("Debug")]
    [SerializeField,Range(-1,4)] int testInt = -1;
    [SerializeField] bool selectibility = false;

    [SerializeField] private List<Sprite> possibleImages = new();
    

    public void Initialize(int typeIndex, bool isSelectible = false)
    {
        dieButton = this.transform.GetChild(0).GetComponent<Button>();
        dieImage = GetComponent<Image>();
        dieButton.onClick.AddListener(Debug_WasPressed);
        if (isSelectible)
        {
            dieButton.interactable = isSelectible;
            dieButton.onClick.AddListener(OnPress);
            
        }
        SelectImage(typeIndex);
    }

    void OnPress()
    {
        EventBus<SelectedDiceType>.Publish(new SelectedDiceType((int)dieType));
    }

    void SelectImage(int type)  // 'type' comes from server, 0..4
    {
        // Validate range (since server could send garbage)
        if (type < 0 || type >= System.Enum.GetValues(typeof(DiceType)).Length)
        {
            Client.Log("Debug", $"Invalid die type index from server: {type}");
            return;
        }

        // Cast the int directly to the enum – works because enum values match server ints
        dieType = (DiceType)type;

        // Use the same int as sprite index (assuming possibleImages order matches server ints)
        if (type >= possibleImages.Count)
        {
            Client.Log("Debug", $"Missing sprite for index {type}");
            return;
        }

        dieImage.sprite = possibleImages[type];
    }
    [AniDrag.Utility.Button]
    void DebugCheck_IMGCHANGE()
    {
        Initialize(testInt, selectibility);
    }
    void Debug_WasPressed()
    {
        Debug.Log($"I was pressed {(int)dieType}!");
    }
    private void OnDisable()
    {
        dieButton.onClick.RemoveAllListeners();
    }
}

public enum DiceType { 
    Human,      // idx: 0 -> is a pt
    Cow,        // idx: 1 -> is a pt
    Chicken,    // idx: 2 -> is a pt
    Tank,       // idx: 3 -> is a danger / enemy
    UFO         // idx: 4 -> is a defense / Attack power
}