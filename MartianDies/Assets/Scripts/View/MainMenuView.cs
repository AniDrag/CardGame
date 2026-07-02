using AniDrag.EventBus;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuView : MonoBehaviour
{
    #region View References

    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField serverIpInput;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button resetIpAddressButton;
    [SerializeField] private Button maliciousTesterButton;

    #endregion

    #region State

    private string originalUsernameText;
    private string originalIpText;

    private Coroutine restoreUsernameCoroutine;
    private Coroutine restoreIpCoroutine;

    #endregion

    #region Event Bindings

    private EventBinding<IncorrectIP> incorrectIpBinding;
    private EventBinding<IncorrectUsername> incorrectUsernameBinding;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        FindMissingReferences();

        RegisterButtons();
        RegisterEvents();

        ResetIP();
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
        if (connectButton == null)
            connectButton = transform.Find("btn_Connect")?.GetComponent<Button>();

        if (resetIpAddressButton == null)
            resetIpAddressButton = transform.Find("BTN_DefaultIP")?.GetComponent<Button>();

        if (connectButton == null)
            Debug.LogError("Connect button not found!");

        if (resetIpAddressButton == null)
            Debug.LogError("Reset IP button not found!");
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectButtonClicked);

        if (resetIpAddressButton != null)
            resetIpAddressButton.onClick.AddListener(OnResetIpButtonClicked);

        if (maliciousTesterButton != null)
            maliciousTesterButton.onClick.AddListener(OnMaliciousTesterButtonClicked);
    }

    private void UnregisterButtons()
    {
        if (connectButton != null)
            connectButton.onClick.RemoveListener(OnConnectButtonClicked);

        if (resetIpAddressButton != null)
            resetIpAddressButton.onClick.RemoveListener(OnResetIpButtonClicked);

        if (maliciousTesterButton != null)
            maliciousTesterButton.onClick.RemoveListener(OnMaliciousTesterButtonClicked);
    }

    private void RegisterEvents()
    {
        incorrectIpBinding = new EventBinding<IncorrectIP>(OnIncorrectIp);
        incorrectUsernameBinding = new EventBinding<IncorrectUsername>(OnIncorrectUsername);

        EventBus<IncorrectIP>.Subscribe(incorrectIpBinding);
        EventBus<IncorrectUsername>.Subscribe(incorrectUsernameBinding);
    }

    private void UnregisterEvents()
    {
        if (incorrectIpBinding != null)
            EventBus<IncorrectIP>.Unsubscribe(incorrectIpBinding);

        if (incorrectUsernameBinding != null)
            EventBus<IncorrectUsername>.Unsubscribe(incorrectUsernameBinding);
    }

    #endregion

    #region UI Events

    private void OnConnectButtonClicked()
    {
        EventBus<Connect>.Publish(new Connect());
    }

    private void OnResetIpButtonClicked()
    {
        ResetIP();
    }
    private void OnMaliciousTesterButtonClicked()
    {
        EventBus<OpenMaliciousTester>.Publish(new OpenMaliciousTester());
    }

    #endregion

    #region Public Getters

    public string GetUsername()
    {
        return usernameInput != null ? usernameInput.text.Trim() : "";
    }

    public string GetServerIp()
    {
        return serverIpInput != null ? serverIpInput.text.Trim() : "";
    }

    #endregion

    #region Public UI Controls

    public void SetButtonsInteractable(bool interactable)
    {
        if (connectButton != null)
            connectButton.interactable = interactable;

        if (usernameInput != null)
            usernameInput.interactable = interactable;

        if (serverIpInput != null)
            serverIpInput.interactable = interactable;

        if (resetIpAddressButton != null)
            resetIpAddressButton.interactable = interactable;

        if (maliciousTesterButton != null)
            maliciousTesterButton.interactable = interactable;
    }

    #endregion

    #region Display

    private void ResetIP()
    {
        if (serverIpInput != null)
            serverIpInput.text = "127.0.0.1";
    }

    private void OnIncorrectUsername(IncorrectUsername e)
    {
        if (usernameInput == null)
            return;

        originalUsernameText = usernameInput.text;
        usernameInput.text = e.errorMsg;

        RestartUsernameRestore();
    }

    private void OnIncorrectIp(IncorrectIP e)
    {
        if (serverIpInput == null)
            return;

        originalIpText = serverIpInput.text;
        serverIpInput.text = e.errorMsg;

        RestartIpRestore();
    }

    #endregion

    #region Coroutines

    private void RestartUsernameRestore()
    {
        if (restoreUsernameCoroutine != null)
            StopCoroutine(restoreUsernameCoroutine);

        restoreUsernameCoroutine = StartCoroutine(RestoreUsernameAfterDelay());
    }

    private IEnumerator RestoreUsernameAfterDelay()
    {
        yield return new WaitForSeconds(2f);

        if (usernameInput != null)
            usernameInput.text = originalUsernameText;

        restoreUsernameCoroutine = null;
    }

    private void RestartIpRestore()
    {
        if (restoreIpCoroutine != null)
            StopCoroutine(restoreIpCoroutine);

        restoreIpCoroutine = StartCoroutine(RestoreIpAfterDelay());
    }

    private IEnumerator RestoreIpAfterDelay()
    {
        yield return new WaitForSeconds(2f);

        if (serverIpInput != null)
            serverIpInput.text = originalIpText;

        restoreIpCoroutine = null;
    }

    #endregion
}