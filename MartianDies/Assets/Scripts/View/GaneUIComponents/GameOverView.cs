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
        if (panel == null)
            panel = gameObject;

        Hide();
    }

    private void OnEnable()
    {
        if (rematchButton != null)
            rematchButton.onClick.AddListener(HandleRematchClicked);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(HandleLeaveClicked);
    }

    private void OnDisable()
    {
        if (rematchButton != null)
            rematchButton.onClick.RemoveListener(HandleRematchClicked);

        if (leaveButton != null)
            leaveButton.onClick.RemoveListener(HandleLeaveClicked);
    }

    #endregion

    #region Public Methods

    public void Show(string message)
    {
        if (panel != null)
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