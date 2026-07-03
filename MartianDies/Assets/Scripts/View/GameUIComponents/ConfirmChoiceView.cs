using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmChoiceView : MonoBehaviour
{
    #region View References

    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private GameObject panel;

    #endregion

    #region State

    private System.Action<bool> onChoice;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        FindMissingReferences();
        RegisterButtons();
        Hide();
    }

    private void OnDestroy()
    {
        UnregisterButtons();
    }

    #endregion

    #region Setup

    private void FindMissingReferences()
    {
        if (panel == null)
        {
            panel = ViewAutoFind.FindGameObject(transform, "Panel_ConfirmDieChoice", "Panel_ConfirmDiceChoice", "Panel_ConfirmChoice");

            if (panel == null && name.Contains("Panel_"))
                panel = gameObject;
        }

        Transform searchRoot = panel != null ? panel.transform : transform;

        if (questionText == null)
            questionText = ViewAutoFind.FindFirstComponent<TMP_Text>(searchRoot);

        if (yesButton == null)
        {
            yesButton = ViewAutoFind.FindComponentByNames<Button>(searchRoot, "btn_Yes", "BTN_Yes", "Yes", "Button_Yes");

            if (yesButton == null)
                yesButton = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "yes");
        }

        if (noButton == null)
        {
            noButton = ViewAutoFind.FindComponentByNames<Button>(searchRoot, "btn_No", "BTN_No", "No", "Button_No");

            if (noButton == null)
                noButton = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "no");
        }
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(OnYesClicked);
            yesButton.onClick.AddListener(OnYesClicked);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(OnNoClicked);
            noButton.onClick.AddListener(OnNoClicked);
        }
    }

    private void UnregisterButtons()
    {
        if (yesButton != null)
            yesButton.onClick.RemoveListener(OnYesClicked);

        if (noButton != null)
            noButton.onClick.RemoveListener(OnNoClicked);
    }

    #endregion

    #region Public Controls

    public void Show(string question = null, System.Action<bool> callback = null)
    {
        FindMissingReferences();

        onChoice = callback;

        if (questionText != null && !string.IsNullOrEmpty(question))
            questionText.text = question;

        if (panel == null)
        {
            Client.Log("ConfirmChoiceView", "Panel reference missing. Cannot show confirm choice.");
            return;
        }

        panel.SetActive(true);
        Client.Log("ConfirmChoiceView", "Panel activated.");
    }

    public void Hide()
    {
        FindMissingReferences();

        if (panel != null)
            panel.SetActive(false);
    }

    #endregion

    #region UI Events

    private void OnYesClicked()
    {
        onChoice?.Invoke(true);
        Hide();
    }

    private void OnNoClicked()
    {
        onChoice?.Invoke(false);
        Hide();
    }

    #endregion
}
