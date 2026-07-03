using AniDrag.EventBus;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DiceView : MonoBehaviour
{
    #region View References

    [SerializeField] private Button diceButton;
    [SerializeField] private Image diceImage;

    #endregion

    #region Dice Data

    [SerializeField] private DiceType diceType;
    [SerializeField] private List<Sprite> possibleImages = new();

    public int TypeIndex => (int)diceType;

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        UnregisterButtons();
    }

    #endregion

    #region Setup

    public void Initialize(int typeIndex, bool isSelectable = false)
    {
        FindReferences();

        diceType = ConvertIndexToDiceType(typeIndex);

        SelectImage(typeIndex);
        RegisterButtons();
        SetSelectable(isSelectable);
    }

    private void FindReferences()
    {
        if (diceButton == null)
            diceButton = GetComponentInChildren<Button>(true);

        if (diceImage == null)
        {
            // Prefer the button image, because in your prefab the visible white square is probably the button.
            if (diceButton != null)
                diceImage = diceButton.GetComponent<Image>();

            // Fallback to root image.
            if (diceImage == null)
                diceImage = GetComponent<Image>();
        }

        if (diceButton == null)
            Client.Log("[DiceView] Missing Button on " + gameObject.name);

        if (diceImage == null)
            Client.Log("[DiceView] Missing Image on " + gameObject.name);
    }

    #endregion

    #region Button Registration

    private void RegisterButtons()
    {
        if (diceButton == null)
            return;

        diceButton.onClick.RemoveListener(OnPressed);
        diceButton.onClick.AddListener(OnPressed);
    }

    private void UnregisterButtons()
    {
        if (diceButton == null)
            return;

        diceButton.onClick.RemoveListener(OnPressed);
    }

    #endregion

    #region Public Controls

    public void SetSelectable(bool selectable)
    {
        if (diceButton == null)
            return;

        bool canSelect = selectable && diceType != DiceType.Tank && diceType != DiceType.Error;
        diceButton.interactable = canSelect;
    }

    #endregion

    #region UI Events

    private void OnPressed()
    {
        EventBus<SelectedDiceType>.Publish(new SelectedDiceType((int)diceType));
    }

    #endregion

    #region Visuals

    private void SelectImage(int typeIndex)
    {
        if (diceImage == null)
            return;

        if (possibleImages == null || possibleImages.Count == 0)
        {
            Client.Log("[DiceView] possibleImages list is empty on prefab: " + gameObject.name);
            return;
        }

        if (typeIndex < 0 || typeIndex >= possibleImages.Count)
        {
            Client.Log($"[DiceView] Missing sprite for dice index {typeIndex}. Sprite count: {possibleImages.Count}");
            return;
        }

        diceImage.sprite = possibleImages[typeIndex];
        diceImage.preserveAspect = true;
    }

    #endregion

    #region Helpers

    private DiceType ConvertIndexToDiceType(int typeIndex)
    {
        switch (typeIndex)
        {
            case 0: return DiceType.Human;
            case 1: return DiceType.Cow;
            case 2: return DiceType.Chicken;
            case 3: return DiceType.Tank;
            case 4: return DiceType.UFO;
            default: return DiceType.Error;
        }
    }

    #endregion
}

public enum DiceType
{
    Human = 0,
    Cow = 1,
    Chicken = 2,
    Tank = 3,
    UFO = 4,
    Error = 5
}