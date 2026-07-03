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
        FindMissingReferences();
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

    #region Setup

    private void FindMissingReferences()
    {
        if (panel == null)
        {
            panel = ViewAutoFind.FindGameObject(transform, "Panel_RoundResults", "Panel_RoundResult", "Panel_Results");

            if (panel == null && name.Contains("Panel_"))
                panel = gameObject;
        }

        Transform searchRoot = panel != null ? panel.transform : transform;

        if (resultsText == null)
            resultsText = ViewAutoFind.FindFirstComponent<TMP_Text>(searchRoot);

        if (closeButton == null)
        {
            closeButton = ViewAutoFind.FindComponentByNames<Button>(searchRoot, "btn_Close", "BTN_Close", "Close", "Button_Close");

            if (closeButton == null)
                closeButton = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "close");
        }
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }
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
        Show(e.msg);
    }

    #endregion

    #region Display

    private void Show(string message)
    {
        FindMissingReferences();

        if (resultsText != null)
            resultsText.text = message;

        if (panel == null)
        {
            Client.Log("RoundResultsView", "Panel reference missing. Cannot show round results.");
            return;
        }

        panel.SetActive(true);

        CancelInvoke(nameof(Hide));
        Invoke(nameof(Hide), 5f);
    }

    public void Hide()
    {
        FindMissingReferences();

        if (panel != null)
            panel.SetActive(false);
    }

    #endregion
}
