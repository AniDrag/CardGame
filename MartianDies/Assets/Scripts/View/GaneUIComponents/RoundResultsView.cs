using AniDrag.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundResultsView : MonoBehaviour
{
    #region View References

    [SerializeField] private TMP_Text resultsText;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject panel;

    #endregion

    #region Event Bindings

    private EventBinding<RoundResults> roundResultsBinding;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        RegisterButtons();
        RegisterEvents();

        Hide();
    }

    private void OnDestroy()
    {
        UnregisterButtons();
        UnregisterEvents();
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    private void UnregisterButtons()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
    }

    private void RegisterEvents()
    {
        roundResultsBinding = new EventBinding<RoundResults>(OnRoundResults);
        EventBus<RoundResults>.Subscribe(roundResultsBinding);
    }

    private void UnregisterEvents()
    {
        if (roundResultsBinding != null)
            EventBus<RoundResults>.Unsubscribe(roundResultsBinding);
    }

    #endregion

    #region Received Events

    private void OnRoundResults(RoundResults e)
    {
        ShowResults(e.msg);
    }

    #endregion

    #region Display

    private void ShowResults(string message)
    {
        if (resultsText != null)
            resultsText.text = message;

        if (panel != null)
            panel.SetActive(true);
        else
            gameObject.SetActive(true);

        CancelInvoke(nameof(Hide));
        Invoke(nameof(Hide), 5f);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    #endregion
}