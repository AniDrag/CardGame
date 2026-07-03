using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverView : MonoBehaviour
{
    #region View References

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text rematchStatusText;

    [SerializeField] private Button rematchButton;
    [SerializeField] private Button leaveButton;

    #endregion

    #region Events

    public event Action OnRematchClicked;
    public event Action OnLeaveClicked;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        FindMissingReferences();
        Hide();
    }

    private void OnEnable()
    {
        FindMissingReferences();

        if (rematchButton != null)
        {
            rematchButton.onClick.RemoveListener(HandleRematchClicked);
            rematchButton.onClick.AddListener(HandleRematchClicked);
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(HandleLeaveClicked);
            leaveButton.onClick.AddListener(HandleLeaveClicked);
        }
    }

    private void OnDisable()
    {
        if (rematchButton != null)
            rematchButton.onClick.RemoveListener(HandleRematchClicked);

        if (leaveButton != null)
            leaveButton.onClick.RemoveListener(HandleLeaveClicked);
    }

    #endregion

    #region Setup

    private void FindMissingReferences()
    {
        if (panel == null)
        {
            panel = ViewAutoFind.FindGameObject(transform, "Panel_Rematch", "Panel_GameOver", "Panel_EndGame");

            if (panel == null && name.Contains("Panel_"))
                panel = gameObject;
        }

        Transform searchRoot = panel != null ? panel.transform : transform;

        TMP_Text[] texts = searchRoot.GetComponentsInChildren<TMP_Text>(true);

        if (titleText == null && texts.Length > 0)
            titleText = texts[0];

        if (rematchStatusText == null && texts.Length > 1)
            rematchStatusText = texts[1];

        if (rematchButton == null)
        {
            rematchButton = ViewAutoFind.FindComponentByNames<Button>(searchRoot,
                "btn_Rematch", "BTN_Rematch", "Rematch", "Button_Rematch");

            if (rematchButton == null)
                rematchButton = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "rematch");
        }

        if (leaveButton == null)
        {
            leaveButton = ViewAutoFind.FindComponentByNames<Button>(searchRoot,
                "btn_Leave", "BTN_Leave", "Leave", "Button_Leave", "btn_Lobby", "Lobby");

            if (leaveButton == null)
                leaveButton = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "leave");

            if (leaveButton == null)
                leaveButton = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "lobby");
        }
    }

    #endregion

    #region Public Methods

    public void Show(string message)
    {
        FindMissingReferences();

        if (panel == null)
        {
            Client.Log("GameOverView", "Panel reference missing. Cannot show game-over view.");
            return;
        }

        panel.SetActive(true);

        if (titleText != null)
            titleText.text = message;

        if (rematchStatusText != null)
            rematchStatusText.text = "Rematch: 0/?";

        if (rematchButton != null)
            rematchButton.interactable = true;

        if (leaveButton != null)
            leaveButton.interactable = true;
    }

    public void Hide()
    {
        FindMissingReferences();

        if (panel != null)
            panel.SetActive(false);
    }

    public void SetRematchStatus(int readyCount, int neededCount)
    {
        if (rematchStatusText != null)
            rematchStatusText.text = $"Rematch: {readyCount}/{neededCount}";
    }

    public void LockRematchButton()
    {
        if (rematchButton != null)
            rematchButton.interactable = false;
    }

    #endregion

    #region UI Events

    private void HandleRematchClicked()
    {
        LockRematchButton();
        OnRematchClicked?.Invoke();
    }

    private void HandleLeaveClicked()
    {
        OnLeaveClicked?.Invoke();
    }

    #endregion
}
