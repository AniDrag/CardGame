using AniDrag.EventBus;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuView : MonoBehaviour
{
    [SerializeField] public TMP_InputField usernameInput;
    [SerializeField] public TMP_InputField serverIpInput;
    [SerializeField] public Button connectButton;
    [SerializeField] public Button resetIpAdres;

    EventBinding<IncorrectIP> incorectIpBinding;
    EventBinding<IncorrectUsername> emptyUsernameBinding;

    private string originalUsernameText;
    private string originalIpUsedText;

    void Start()
    {
        if (connectButton == null)
            connectButton = transform.Find("btn_Connect")?.GetComponent<Button>();
        if (connectButton == null)
            Debug.LogError("Connect button not found!");
        else
            connectButton.onClick.AddListener(() => EventBus<Connect>.Publish(new Connect()));

        if (resetIpAdres == null)
            resetIpAdres = transform.Find("BTN_DefaultIP")?.GetComponent<Button>();
        if (resetIpAdres == null)
            Debug.LogError("Reset IP button not found!");
        else
            resetIpAdres.onClick.AddListener(ResetIP);

        if (serverIpInput != null)
            serverIpInput.text = "127.0.0.1";

        incorectIpBinding = new EventBinding<IncorrectIP>(IncorectIp);
        emptyUsernameBinding = new EventBinding<IncorrectUsername>(IncorectUsername);
        EventBus<IncorrectIP>.Subscribe(incorectIpBinding);
        EventBus<IncorrectUsername>.Subscribe(emptyUsernameBinding);
    }

    public string GetUsername() => usernameInput != null ? usernameInput.text.Trim() : "";
    public string GetServerIp() => serverIpInput != null ? serverIpInput.text.Trim() : "";

    void ResetIP()
    {
        if (serverIpInput != null)
            serverIpInput.text = "127.0.0.1";
    }

    public void SetButtonsInteractable(bool interactable)
    {
        if (connectButton != null)
            connectButton.interactable = interactable;
        if (usernameInput != null)
            usernameInput.interactable = interactable;
        if (serverIpInput != null)              
            serverIpInput.interactable = interactable;
        if (resetIpAdres != null)                
            resetIpAdres.interactable = interactable;
    }

    private void OnDestroy()
    {
        if (connectButton != null)
            connectButton.onClick.RemoveAllListeners();
        if (resetIpAdres != null)
            resetIpAdres.onClick.RemoveAllListeners();
        EventBus<IncorrectIP>.Unsubscribe(incorectIpBinding);
        EventBus<IncorrectUsername>.Unsubscribe(emptyUsernameBinding);
    }

    private void IncorectUsername(IncorrectUsername e)
    {
        if (usernameInput == null) return;
        originalUsernameText = usernameInput.text;
        usernameInput.text = e.errorMsg;
        DelayedUsernameRestore();
    }

    private void DelayedUsernameRestore()
    {
        StopCoroutine(RestoreUsernameAfterDelay());
        StartCoroutine(RestoreUsernameAfterDelay());
    }

    private IEnumerator RestoreUsernameAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        if (usernameInput != null)
            usernameInput.text = originalUsernameText;
    }

    public void IncorectIp(IncorrectIP e)  
    {
        if (serverIpInput == null) return;
        originalIpUsedText = serverIpInput.text;
        serverIpInput.text = e.errorMsg;   
        DelayedIPAdressRestore();
    }

    private void DelayedIPAdressRestore()
    {
        StopCoroutine(RestoreIPAdressAfterDelay());
        StartCoroutine(RestoreIPAdressAfterDelay());
    }

    private IEnumerator RestoreIPAdressAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        if (serverIpInput != null) 
            serverIpInput.text = originalIpUsedText;
    }
}