using OSCTools;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private MainMenuView view;
    private const string REGISTER_TIMEOUT_ID = "register";

    private void Start()
    {
        if (Client.Instance == null)
        {
            Debug.LogError("Client instance not found!");
            return;
        }

        view.OnConnectClicked += HandleConnect;

        // Subscribe to the connected event
        Client.Instance.OnConnected += OnClientConnected;

        Client.Log("Loaded Main Menu");
    }

    private void HandleConnect()
    {
        string username = view.GetUsername();
        string ip = view.GetServerIp();

        if (string.IsNullOrEmpty(username))
        {
            Client.Log("Connection attempt failed: empty username.");
            return;
        }
        if (string.IsNullOrEmpty(ip))
        {
            Client.Log("Connection attempt failed: empty IP.");
            return;
        }

        Client.Log($"Connecting to {ip}...");
        Client.Instance.Connect(ip, 55000);
    }

    private void OnClientConnected()
    {
        // Now we are connected – add listeners and send registration
        string username = view.GetUsername();

        Client.Instance.AddListener("/registered", OnRegistered, OSCUtil.INT, OSCUtil.STRING);
        Client.Log("Debug", "Registered /registered listener");
        Client.Instance.AddListener("/*", (msg, sender) => {
            Client.Log("Debug", $"Wildcard caught: {msg.header} with tags {msg.typeTag}");
        });

        Client.Instance.StartTimeout(REGISTER_TIMEOUT_ID, 10f, () =>
        {
            view.SetButtonsInteractable(true);
            Client.Instance.RemoveListener("/registered", OnRegistered); 
            Client.Instance.Disconnect();
        });

        OSCMessageOut regMsg = new OSCMessageOut("/register");
        regMsg.AddString(username);
        Client.Instance.Send(regMsg);
    }

    private void OnRegistered(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Debug", "OnRegistered ENTERED");

        Client.Instance.CancelTimeout(REGISTER_TIMEOUT_ID);
        int id = msg.ReadInt();
        Client.Instance.Username = msg.ReadString();
        Client.Log($"Registration successful! Server assigned ID {id} to {name}");
        Client.Instance.RemoveListener("/registered", OnRegistered);
        SceneManager.LoadScene("1_Sc_Lobby");
    }

    private void OnDisable()
    {
        if (view != null)
            view.OnConnectClicked -= HandleConnect;
        if (Client.Instance != null)
            Client.Instance.OnConnected -= OnClientConnected;
    }
}