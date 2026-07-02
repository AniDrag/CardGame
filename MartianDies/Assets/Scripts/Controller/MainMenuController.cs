using AniDrag.EventBus;
using OSCTools;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    #region View References

    [SerializeField] private MainMenuView view;

    #endregion

    #region Event Bindings

    private EventBinding<Connect> connectBinding;
    private EventBinding<OpenMaliciousTester> openMaliciousTesterBinding;
    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (!ValidateReferences())
            return;

        RegisterEvents();
        RegisterClientEvents();

        Client.Log("Loaded Main Menu");
    }

    private void OnDisable()
    {
        UnregisterEvents();
        UnregisterClientEvents();
        UnregisterServerMessages();
    }

    #endregion

    #region Setup

    private bool ValidateReferences()
    {
        if (Client.Instance == null)
        {
            Debug.LogError("Client instance not found!");
            return false;
        }

        if (view == null)
            view = FindFirstObjectByType<MainMenuView>();

        if (view == null)
        {
            Debug.LogError("MainMenuView not found!");
            return false;
        }

        return true;
    }

    #endregion

    #region Event Registration

    private void RegisterEvents()
    {
        connectBinding = new EventBinding<Connect>(OnConnectClicked);
        openMaliciousTesterBinding = new EventBinding<OpenMaliciousTester>(OnOpenMaliciousTester);

        EventBus<Connect>.Subscribe(connectBinding);
        EventBus<OpenMaliciousTester>.Subscribe(openMaliciousTesterBinding);
    }

    private void UnregisterEvents()
    {
        if (connectBinding != null)
            EventBus<Connect>.Unsubscribe(connectBinding);

        if (openMaliciousTesterBinding != null)
            EventBus<OpenMaliciousTester>.Unsubscribe(openMaliciousTesterBinding);
    }

    private void RegisterClientEvents()
    {
        Client.Instance.OnConnected += OnConnected;
        Client.Instance.OnConnectionFailed += OnConnectionFailed;
    }

    private void UnregisterClientEvents()
    {
        if (Client.Instance == null)
            return;

        Client.Instance.OnConnected -= OnConnected;
        Client.Instance.OnConnectionFailed -= OnConnectionFailed;
    }

    private void RegisterServerMessages()
    {
        Client.Instance.AddListener(Msg.S_REGISTERED, OnRegistered, OSCUtil.INT, OSCUtil.STRING);
        Client.Instance.AddListener(Msg.S_ERROR, OnServerError, OSCUtil.STRING);
    }

    private void UnregisterServerMessages()
    {
        if (Client.Instance == null)
            return;

        Client.Instance.RemoveListener(Msg.S_REGISTERED, OnRegistered);
        Client.Instance.RemoveListener(Msg.S_ERROR, OnServerError);
    }

    #endregion

    #region UI Events

    private void OnConnectClicked(Connect e)
    {
        string username = view.GetUsername();
        string ip = view.GetServerIp();

        if (!ValidateUsername(username))
            return;

        if (!ValidateIp(ip))
            return;

        view.SetButtonsInteractable(false);

        Client.Log($"Connecting to {ip}...");
        Client.Instance.Connect(ip, Msg.PORT);
    }

    #endregion

    #region Client Events

    private void OnConnected()
    {
        RegisterServerMessages();

        Client.Instance.StartTimeout(Msg.REGISTER_TIMEOUT_ID, 10f, OnRegisterTimeout);

        SendRegister();
    }

    private void OnConnectionFailed(string reason)
    {
        Client.Log("Connection failed: " + reason);
        view.SetButtonsInteractable(true);
        EventBus<IncorrectIP>.Publish(new IncorrectIP("Connection failed."));
    }

    #endregion

    #region Received Messages

    private void OnRegistered(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Instance.CancelTimeout(Msg.REGISTER_TIMEOUT_ID);

        int id = msg.ReadInt();
        string username = msg.ReadString();

        Client.Instance.ClientId = id;
        Client.Instance.Username = username;

        Client.Log($"Registration successful! Server assigned ID {id} to {username}");

        UnregisterServerMessages();

        SceneManager.LoadSceneAsync(Scenes.Lobby);
    }

    private void OnServerError(OSCMessageIn msg, IPEndPoint sender)
    {
        string error = msg.ReadString();

        Client.Log("Server error: " + error);

        Client.Instance.CancelTimeout(Msg.REGISTER_TIMEOUT_ID);
        Client.Instance.Disconnect("Registration rejected");

        view.SetButtonsInteractable(true);
        EventBus<IncorrectUsername>.Publish(new IncorrectUsername(error));
    }

    #endregion

    #region Sending Messages

    private void SendRegister()
    {
        var msg = new OSCMessageOut(Msg.C_REGISTER)
            .AddString(view.GetUsername());

        Client.Instance.Send(msg);
    }

    #endregion

    #region Validation

    private bool ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            Client.Log("Connection attempt failed: empty username.");
            EventBus<IncorrectUsername>.Publish(new IncorrectUsername("Empty username."));
            return false;
        }

        if (username.Length > 12)
        {
            Client.Log("Connection attempt failed: username too long.");
            EventBus<IncorrectUsername>.Publish(new IncorrectUsername("Max 12 chars."));
            return false;
        }

        return true;
    }

    private bool ValidateIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            Client.Log("Connection attempt failed: empty IP.");
            EventBus<IncorrectIP>.Publish(new IncorrectIP("Empty IP."));
            return false;
        }

        return true;
    }

    #endregion

    #region Timeout

    private void OnRegisterTimeout()
    {
        Client.Log("Registration timeout. Disconnecting.");

        view.SetButtonsInteractable(true);
        Client.Instance.Disconnect("Registration timeout");
    }
    #endregion
    #region Debugging 
    private void OnOpenMaliciousTester(OpenMaliciousTester e)
    {
        SceneManager.LoadSceneAsync(Scenes.MaliciousClient);
    }
    #endregion
}