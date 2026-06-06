using AniDrag.EventBus;
using OSCTools;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private MainMenuView view;

    EventBinding<Connect> connectBinding;


    private void Start()
    {
        if (Client.Instance == null)
        {
            Debug.LogError("Client instance not found!");
            return;
        }

        // Subscribe to event bus with correct signature
        connectBinding = new EventBinding<Connect>(ConnectClicked);
        EventBus<Connect>.Subscribe(connectBinding);

        // Subscribe to client connection event
        Client.Instance.OnConnected += OnConnect;

        Client.Log("Loaded Main Menu");
    }

    private void ConnectClicked(Connect e)
    {
        string username = view.GetUsername();
        string ip = view.GetServerIp();

        if (string.IsNullOrEmpty(username))
        {
            Client.Log("Connection attempt failed: empty username.");
            EventBus<IncorrectUsername>.Publish(new IncorrectUsername("Empty username."));
            return;
        }
        else if (username.Length > 13)
        {
            Client.Log("Connection attempt failed: username too long.");
            EventBus<IncorrectUsername>.Publish(new IncorrectUsername("Username too long."));
            return;
        }
        if (string.IsNullOrEmpty(ip))
        {
            Client.Log("Connection attempt failed: empty IP.");
            EventBus<IncorrectIP>.Publish(new IncorrectIP("Empty IP."));
            return;
        }

        Client.Log($"Connecting to {ip}...");
        Client.Instance.Connect(ip, Msg.PORT);
    }

    private void OnConnect()
    {
        string username = view.GetUsername();

        // Add listener for registration reply
        Client.Instance.AddListener(Msg.S_REGISTERED, OnRegistered, OSCUtil.INT, OSCUtil.STRING);
        Client.Log("Debug", "Registered listener for S_REGISTERED");

        // Start timeout for registration
        Client.Instance.StartTimeout(Msg.REGISTER_TIMEOUT_ID, 10f, () =>
        {
            Client.Log("Registration timeout – disconnecting");
            view.SetButtonsInteractable(true);
            Client.Instance.Disconnect();
        });

        // Send registration
        OSCMessageOut regMsg = new OSCMessageOut(Msg.C_REGISTER);
        regMsg.AddString(username);
        Client.Instance.Send(regMsg);
    }

    private void OnRegistered(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Debug", "OnRegistered ENTERED");

        Client.Instance.CancelTimeout(Msg.REGISTER_TIMEOUT_ID);
        int id = msg.ReadInt();
        Client.Instance.Username = msg.ReadString();
        Client.Log($"Registration successful! Server assigned ID {id} to {name}");
        Client.Instance.RemoveListener("/registered", OnRegistered);
        SceneManager.LoadSceneAsync(Scenes.Lobby);
    }

    private void OnDisable()
    {
        EventBus<Connect>.Unsubscribe(connectBinding);
        if (Client.Instance != null)
            Client.Instance.OnConnected -= OnConnect;
    }
}