using AniDrag.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuView : MonoBehaviour
{
    [SerializeField] public TMP_InputField usernameInput;
    [SerializeField] public TMP_InputField serverIpInput;
    [SerializeField] public Button connectButton;
    [SerializeField] public Button resetIpAdres;

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
    }

    public string GetUsername() => usernameInput?.text.Trim();
    public string GetServerIp() => serverIpInput?.text.Trim();
    void ResetIP() => serverIpInput.text = "127.0.0.1";

    public void SetButtonsInteractable(bool interactable)
    {
        connectButton.interactable = interactable;
        usernameInput.interactable = interactable;
        serverIpInput.interactable = interactable;
    }

    private void OnDestroy()
    {
        connectButton.onClick.RemoveAllListeners();
        resetIpAdres.onClick.RemoveAllListeners();
    }
}