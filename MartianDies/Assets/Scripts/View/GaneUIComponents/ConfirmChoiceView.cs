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
        RegisterButtons();
        Hide();
    }

    private void OnDestroy()
    {
        UnregisterButtons();
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        if (yesButton != null)
            yesButton.onClick.AddListener(OnYesClicked);

        if (noButton != null)
            noButton.onClick.AddListener(OnNoClicked);
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

    public void Show()
    {
        if (panel == null)
        {
            Client.Log("RollAgainView", "Panel reference missing. Using this gameObject.");
            panel = transform.Find("Panel_ConfirmDieChoice").gameObject;
        }

        panel.SetActive(true);

        Client.Log("ConfirmChoiceView", "Panel activated.");
    }

    public void Hide()
    {
        if (panel == null)
        {
            Client.Log("RollAgainView", "Panel reference missing. Using this gameObject.");
            panel = transform.Find("Panel_ConfirmDieChoice").gameObject;
        }
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