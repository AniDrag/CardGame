using AniDrag.EventBus;
using OSCTools;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
/*
 * MainMenuController
 * 
 * Purpose:
 * This script controls the main menu connection flow.
 * It connects the UI to the networking Client.
 * 
 * What it does:
 * - Reads username and server IP from MainMenuView.
 * - Validates the input before connecting.
 * - Starts the TCP connection.
 * - Sends the register message after the TCP connection succeeds.
 * - Waits for the server to confirm registration.
 * - Moves to the Lobby scene after successful registration.
 * - Shows username/IP errors through local EventBus events.
 * - Can open the malicious client tester scene for debugging.
 * 
 * Important:
 * This script does not keep the TCP connection itself.
 * The Client singleton handles the actual connection, sending, receiving, and timeouts.
 */

public class MainMenuController : MonoBehaviour
{
    #region View References

    /*
     * Reference to the main menu UI script.
     * 
     * MainMenuView is expected to handle:
     * - username input field
     * - server IP input field
     * - connect button
     * - error display
     * - button interactable state
     */
    [SerializeField] private MainMenuView view;

    #endregion

    #region Event Bindings

    /*
    * Local EventBus bindings.
    * 
    * connectBinding:
    * Listens for the Connect event, usually fired by the connect button.
    * 
    * openMaliciousTesterBinding:
    * Listens for the OpenMaliciousTester event, usually fired by a debug button.
    */
    private EventBinding<Connect> connectBinding;
    private EventBinding<OpenMaliciousTester> openMaliciousTesterBinding;

    #endregion

    #region Unity Lifecycle

    /*
     * Start
     * 
     * What this does:
     * Runs when the main menu scene starts.
     * 
     * Flow:
     * 1. Checks if the needed references exist.
     * 2. Registers local UI/EventBus events.
     * 3. Registers Client connection events.
     * 4. Logs that the main menu loaded.
     */
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
        connectBinding = new EventBinding<Connect>(ConnectClicked);
        openMaliciousTesterBinding = new EventBinding<OpenMaliciousTester>(OpenMaliciousTesterScene);

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
        Client.Instance.OnConnected += Connected;
        Client.Instance.OnConnectionFailed += ConnectionFailed;
    }

  
    private void UnregisterClientEvents()
    {
        if (Client.Instance == null)
            return;

        Client.Instance.OnConnected -= Connected;
        Client.Instance.OnConnectionFailed -= ConnectionFailed;
    }

    /*
     * What this does:
     * Registers temporary OSC messages needed during registration.
     *
     * These are only needed after the TCP connection succeeds.
     *
     * OSC received:
     *
     * Msg.S_REGISTERED
     * Payload:
     * [0] int id
     * [1] string username
     *
     * Msg.S_ERROR
     * Payload:
     * [0] string error
     */
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

    /*
     * Local EventBus event: Connect
     *
     * Data received:
     * No data inside the Connect event.
     *
     * What this does:
     * 1. Reads username and IP from the view.
     * 2. Validates the username.
     * 3. Validates the IP field.
     * 4. Disables buttons so the user cannot spam connect.
     * 5. Starts the TCP connection through Client.Instance.
     *
     * Important:
     * This does not send the register OSC message yet.
     * Register is sent after the Client confirms the TCP connection worked.
     */
    private void ConnectClicked(Connect e)
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

    /*
     * Client event.
     *
     * What this means:
     * The TCP connection to the server succeeded.
     *
     * What this does:
     * 1. Registers OSC listeners for registration result.
     * 2. Starts a timeout so the client does not wait forever.
     * 3. Sends the register OSC message.
     */
    private void Connected()
    {
        RegisterServerMessages();

        Client.Instance.StartTimeout(Msg.REGISTER_TIMEOUT_ID, 10f, RegisterTimeout);

        SendRegister();
    }

    /*
     * Client event.
     *
     * Data received:
     * reason = why the connection failed.
     *
     * What this does:
     * Re-enables main menu buttons and publishes an IncorrectIP event
     * so the UI can show a connection error.
     */
    private void ConnectionFailed(string reason)
    {
        Client.Log("Connection failed: " + reason);

        view.SetButtonsInteractable(true);

        EventBus<IncorrectIP>.Publish(new IncorrectIP("Connection failed."));
    }

    #endregion

    #region Received OSC Messages

    /*
     * OSC RECEIVE: Msg.S_REGISTERED
     *
     * Payload received:
     * [0] int id
     * [1] string username
     *
     * Example:
     * id = 2
     * username = "Nik"
     *
     * What this means:
     * The server accepted the register request.
     * The server assigned this client an id.
     *
     * What this does:
     * 1. Cancels the register timeout.
     * 2. Saves the server assigned client id.
     * 3. Saves the confirmed username.
     * 4. Removes registration-only OSC listeners.
     * 5. Loads the Lobby scene.
     */
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

    /*
     * OSC RECEIVE: Msg.S_ERROR
     *
     * Payload received:
     * [0] string error
     *
     * Example:
     * error = "Username already taken."
     *
     * What this means:
     * The server rejected the registration or sent a server error.
     *
     * What this does:
     * 1. Reads the error message.
     * 2. Cancels the register timeout.
     * 3. Disconnects from the server.
     * 4. Re-enables menu buttons.
     * 5. Publishes IncorrectUsername so the UI can show the error.
     */
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

    #region Sending OSC Messages

    /*
     * OSC SEND: Msg.C_REGISTER
     *
     * Payload sent:
     * [0] string username
     *
     * Example:
     * username = "Nik"
     *
     * What this tells the server:
     * This client wants to register with the given username.
     *
     * Important:
     * The client does basic validation first for user feedback,
     * but the server still needs to validate the username again.
     */
    private void SendRegister()
    {
        var msg = new OSCMessageOut(Msg.C_REGISTER)
            .AddString(view.GetUsername());

        Client.Instance.Send(msg);
    }

    #endregion

    #region Validation

    /*
     * What this does:
     * Checks the username before connecting.
     *
     * Rules:
     * - Username cannot be empty.
     * - Username cannot be longer than 12 characters.
     *
     * Returns:
     * true if username is locally valid.
     * false if username is invalid.
     *
     * If invalid:
     * Publishes IncorrectUsername so the view can show feedback.
     */
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

    /*
     * What this does:
     * Checks the IP text before connecting.
     *
     * Rules:
     * - IP field cannot be empty.
     *
     * Returns:
     * true if IP text is not empty.
     * false if IP text is empty.
     *
     * Note:
     * This does not fully validate if the IP format is correct.
     * The actual Client connection will fail if the IP cannot be reached.
     */
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

    /*
     * What this means:
     * TCP connection worked, but the server did not answer the register request in time.
     *
     * Expected server answers before this timeout:
     * - Msg.S_REGISTERED
     * - Msg.S_ERROR
     *
     * What this does:
     * Re-enables main menu buttons and disconnects the client.
     */
    private void RegisterTimeout()
    {
        Client.Log("Registration timeout. Disconnecting.");

        view.SetButtonsInteractable(true);

        Client.Instance.Disconnect("Registration timeout");
    }

    #endregion

    #region Debugging 

    private void OpenMaliciousTesterScene(OpenMaliciousTester e)
    {
        SceneManager.LoadSceneAsync(Scenes.MaliciousClient);
    }

    #endregion
}