using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClientMainMenuManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_InputField serverIpInputField;
    [SerializeField] private Button connectButton;
    [SerializeField] private TMP_Text serverMSG;

    private void Start()
    {
        if (nameInputField == null)
            nameInputField = GameObject.Find("NameField_inputField")?.GetComponent<TMP_InputField>();
        if (serverIpInputField == null)
            serverIpInputField = GameObject.Find("ServerIPField_inputField")?.GetComponent<TMP_InputField>();
        if (connectButton == null)
            connectButton = GameObject.Find("Connect_btn")?.GetComponent<Button>();
        if (serverMSG == null)
            serverMSG = GameObject.Find("ServerMSG_text")?.GetComponent<TMP_Text>();

        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectButtonClicked);

        if (DummyClient.Instance != null)
        {
            DummyClient.Instance.MainMenuConnectonFaliure += OnConnectionFailed;
            DummyClient.Instance.MainMenuUpdateStatus += ServerMSG;
        }
    }

    void OnConnectButtonClicked()
    {
        string playerName = nameInputField.text;
        string serverIp = serverIpInputField.text;

        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogWarning("Player name is required!");
            StartCoroutine(ShowTempWarning(" Player name is required!", nameInputField));
            return;
        }
        if (string.IsNullOrEmpty(serverIp))
        {
            Debug.LogWarning("Server IP is required!");
            StartCoroutine(ShowTempWarning(" Server IP is required!", serverIpInputField));
            return;
        }

        Debug.Log($"Attempting to connect to server at {serverIp} with player name {playerName}");
        DummyClient.Instance.ConnectToServer(playerName, serverIp);
    }

    void OnConnectionFailed()
    {
        Debug.LogWarning("Server IP is invalid!");
        StartCoroutine(ShowTempWarning(" Server IP is invalid!", serverIpInputField));
    }

    IEnumerator ShowTempWarning(string message, TMP_InputField inputfield)
    {
        string originalText = inputfield.text;
        inputfield.text = message;
        yield return new WaitForSeconds(2f);
        inputfield.text = originalText;
    }

    public void ServerMSG(string message)
    {
        if (serverMSG != null)
        {
            serverMSG.text = message;
            StartCoroutine(ClearMessageAfterDelay(serverMSG, 3f));
        }
    }

    IEnumerator ClearMessageAfterDelay(TMP_Text textField, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (textField != null)
            textField.text = string.Empty;
    }

    private void OnDestroy()
    {
        if (DummyClient.Instance != null)
            DummyClient.Instance.MainMenuUpdateStatus -= ServerMSG;
    }
}