using OSCTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/*
 * MaliciousClientTester
 * 
 * Purpose:
 * This script is a debug/testing tool for the networking system.
 * It lets a developer manually send OSC messages to the server.
 * 
 * This is useful for testing:
 * - normal client messages
 * - wrong data types
 * - missing data
 * - invalid values
 * - spam/burst messages
 * - unknown OSC addresses
 * 
 * Important:
 * This script is not normal gameplay code.
 * It is made to test if the server is safe against bad or unexpected client input.
 * 
 * The server should never trust these messages.
 * The server should validate every message before using it.
 */
public class MaliciousClientTester : MonoBehaviour
{
    #region View References

    [Header("UI References")]
    [SerializeField] private TMP_Dropdown messageDropdown;
    [SerializeField] private TMP_InputField parameterInput;
    [SerializeField] private TMP_Text previewText;
    [SerializeField] private TMP_InputField serverIpInput;

    [Header("Buttons")]
    [SerializeField] private Button sendButton;
    [SerializeField] private Button burstButton;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button disconnectButton;


    [Header("Burst Settings")]
    [SerializeField] private TMP_InputField burstCountInput;
    [SerializeField] private TMP_InputField burstDelayInput;

    #endregion

    #region State
    private readonly List<MessagePreset> presets = new();
    private Coroutine burstCoroutine;

    /*
     * queuedMessageAfterConnect:
     * If the tester tries to send while disconnected, the message is stored here.
     * After the client connects, this message is sent automatically.
     * 
     * waitingForAutoConnect:
     * True when the tester is waiting for a connection before sending the queued message.
     */

    private OSCMessageOut queuedMessageAfterConnect;
    private bool waitingForAutoConnect;

    #endregion

    #region Unity Lifecycle

    /*
     * Start
     * 
     * What this does:
     * Sets up the tester UI.
     * Builds all test message presets.
     * Registers UI button events.
     * Registers Client connection events.
     * Sets default values for IP, burst count, and burst delay.
     */
    private void Start()
    {
        BuildPresets();
        PopulateDropdown();
        RegisterUI();
        RegisterClientEvents();

        if (serverIpInput != null && Client.Instance != null)
            serverIpInput.text = Client.Instance.ServerIP;

        if (burstCountInput != null)
            burstCountInput.text = "10";

        if (burstDelayInput != null)
            burstDelayInput.text = "0.05";

        ApplySelectedPreset();
        UpdatePreview();
    }

    private void OnDestroy()
    {
        UnregisterUI();
        UnregisterClientEvents();

        if (burstCoroutine != null)
            StopCoroutine(burstCoroutine);
    }

    #endregion

    #region Setup

    /*
     * BuildPresets
     * 
     * What this does:
     * Creates all predefined test messages.
     * 
     * Preset parameter format:
     * type:value
     * 
     * Supported types:
     * - int:123
     * - float:1.5
     * - bool:true
     * - string:hello
     * 
     * Multiple parameters are separated with |
     * Example:
     * string:Test Room | int:25
     * 
     * Message examples:
     * 
     * Msg.C_REGISTER
     * Payload sent:
     * [0] string username
     * 
     * Msg.C_CREATE_ROOM
     * Payload sent:
     * [0] string roomName
     * [1] int pointGoal
     * 
     * Msg.C_JOIN_ROOM
     * Payload sent:
     * [0] string roomName
     * 
     * Msg.C_SELECT_DICE
     * Payload sent:
     * [0] int diceType
     * 
     * Msg.C_STAKE_ANSWER
     * Payload sent:
     * [0] bool doReRoll
     * 
     * Some presets intentionally send wrong data.
     * Those are used to check if the server rejects invalid messages safely.
     */
    private void BuildPresets()
    {
        presets.Clear();

        // General
        presets.Add(new MessagePreset("C_DISCONNECT - normal", Msg.C_DISCONNECT, "", "Disconnect normally."));

        // Main Menu
        presets.Add(new MessagePreset("C_REGISTER - normal", Msg.C_REGISTER, "string:Tester", "Register with a normal username."));
        presets.Add(new MessagePreset("C_REGISTER - empty username", Msg.C_REGISTER, "string:", "Try registering with empty username."));
        presets.Add(new MessagePreset("C_REGISTER - very long username", Msg.C_REGISTER, "string:ThisUsernameIsWayTooLongForTheServer", "Try username length validation."));
        presets.Add(new MessagePreset("C_REGISTER - wrong type", Msg.C_REGISTER, "int:123", "Send int where server expects string."));

        // Lobby
        presets.Add(new MessagePreset("C_LIST_ROOMS - normal", Msg.C_LIST_ROOMS, "", "Ask for room list."));

        presets.Add(new MessagePreset("C_CREATE_ROOM - normal", Msg.C_CREATE_ROOM, "string:Test Room | int:25", "Create normal room."));
        presets.Add(new MessagePreset("C_CREATE_ROOM - empty name", Msg.C_CREATE_ROOM, "string: | int:25", "Try empty room name."));
        presets.Add(new MessagePreset("C_CREATE_ROOM - huge name", Msg.C_CREATE_ROOM, "string:ThisRoomNameIsDefinitelyWayTooLongForTheLimit | int:25", "Try room name length validation."));
        presets.Add(new MessagePreset("C_CREATE_ROOM - negative goal", Msg.C_CREATE_ROOM, "string:BadGoal | int:-10", "Try invalid point goal."));
        presets.Add(new MessagePreset("C_CREATE_ROOM - huge goal", Msg.C_CREATE_ROOM, "string:BigGoal | int:999999", "Try excessive point goal."));
        presets.Add(new MessagePreset("C_CREATE_ROOM - wrong types", Msg.C_CREATE_ROOM, "int:123 | string:not_a_number", "Wrong argument order and types."));

        presets.Add(new MessagePreset("C_JOIN_ROOM - normal", Msg.C_JOIN_ROOM, "string:Test Room", "Join existing room."));
        presets.Add(new MessagePreset("C_JOIN_ROOM - missing room", Msg.C_JOIN_ROOM, "string:RoomThatDoesNotExist", "Join nonexistent room."));
        presets.Add(new MessagePreset("C_JOIN_ROOM - wrong type", Msg.C_JOIN_ROOM, "int:5", "Send int where server expects room name."));

        presets.Add(new MessagePreset("C_LEAVE_ROOM - normal", Msg.C_LEAVE_ROOM, "", "Leave current room."));
        presets.Add(new MessagePreset("C_CLOSE_ROOM - normal", Msg.C_CLOSE_ROOM, "", "Close current hosted room."));
        presets.Add(new MessagePreset("C_START_GAME - normal", Msg.C_START_GAME, "", "Start current room."));
        presets.Add(new MessagePreset("C_START_GAME - spam candidate", Msg.C_START_GAME, "", "Try repeated start game calls."));

        // Game loading
        presets.Add(new MessagePreset("C_GAME_SCENE_READY - normal", Msg.C_GAME_SCENE_READY, "", "Tell server game scene is ready."));
        presets.Add(new MessagePreset("C_GAME_SCENE_READY - spam candidate", Msg.C_GAME_SCENE_READY, "", "Try repeated scene ready calls."));

        // Game
        presets.Add(new MessagePreset("C_SELECT_DICE - Human", Msg.C_SELECT_DICE, "int:0", "Select Human."));
        presets.Add(new MessagePreset("C_SELECT_DICE - Cow", Msg.C_SELECT_DICE, "int:1", "Select Cow."));
        presets.Add(new MessagePreset("C_SELECT_DICE - Chicken", Msg.C_SELECT_DICE, "int:2", "Select Chicken."));
        presets.Add(new MessagePreset("C_SELECT_DICE - Tank invalid", Msg.C_SELECT_DICE, "int:3", "Try selecting Tank. Should be rejected."));
        presets.Add(new MessagePreset("C_SELECT_DICE - UFO", Msg.C_SELECT_DICE, "int:4", "Select UFO."));
        presets.Add(new MessagePreset("C_SELECT_DICE - negative", Msg.C_SELECT_DICE, "int:-1", "Invalid dice type."));
        presets.Add(new MessagePreset("C_SELECT_DICE - huge", Msg.C_SELECT_DICE, "int:999", "Invalid dice type."));
        presets.Add(new MessagePreset("C_SELECT_DICE - wrong type", Msg.C_SELECT_DICE, "string:not_an_int", "Send string where server expects int."));

        presets.Add(new MessagePreset("C_STAKE_ANSWER - continue", Msg.C_STAKE_ANSWER, "bool:true", "Choose double stake / continue rolling."));
        presets.Add(new MessagePreset("C_STAKE_ANSWER - bank", Msg.C_STAKE_ANSWER, "bool:false", "Choose bank points."));
        presets.Add(new MessagePreset("C_STAKE_ANSWER - wrong type", Msg.C_STAKE_ANSWER, "string:true", "Send string where server expects bool."));

        // Unknown/custom
        presets.Add(new MessagePreset("UNKNOWN ADDRESS", "/definitely_not_a_real_message", "int:123 | string:hello", "Send unknown OSC address."));
        presets.Add(new MessagePreset("CUSTOM - first parameter is address", "", "/c_select_dice | int:999", "Custom mode. First token must be OSC address.", true));
    }

  
    private void PopulateDropdown()
    {
        if (messageDropdown == null)
            return;

        messageDropdown.ClearOptions();

        List<string> labels = presets.Select(preset => preset.DisplayName).ToList();

        messageDropdown.AddOptions(labels);
    }

    #endregion

    #region UI Registration
    private void RegisterUI()
    {
        if (messageDropdown != null)
            messageDropdown.onValueChanged.AddListener(OnDropdownChanged);

        if (parameterInput != null)
            parameterInput.onValueChanged.AddListener(OnParameterInputChanged);

        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendClicked);

        if (burstButton != null)
            burstButton.onClick.AddListener(OnBurstClicked);

        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectClicked);

        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(OnDisconnectClicked);
    }

    private void UnregisterUI()
    {
        if (messageDropdown != null)
            messageDropdown.onValueChanged.RemoveListener(OnDropdownChanged);

        if (parameterInput != null)
            parameterInput.onValueChanged.RemoveListener(OnParameterInputChanged);

        if (sendButton != null)
            sendButton.onClick.RemoveListener(OnSendClicked);

        if (burstButton != null)
            burstButton.onClick.RemoveListener(OnBurstClicked);

        if (connectButton != null)
            connectButton.onClick.RemoveListener(OnConnectClicked);

        if (disconnectButton != null)
            disconnectButton.onClick.RemoveListener(OnDisconnectClicked);
    }

    #endregion

    #region Client Event Registration

    /*
     * RegisterClientEvents
     * 
     * What this does:
     * Subscribes to the main Client connection events.
     * 
     * Used for:
     * - sending a queued message after auto connect
     * - clearing queued data if connection fails
     * - clearing queued data if disconnected
     */
    private void RegisterClientEvents()
    {
        if (Client.Instance == null)
            return;

        Client.Instance.OnConnected += OnClientConnected;
        Client.Instance.OnConnectionFailed += OnConnectionFailed;
        Client.Instance.OnDisconnected += OnClientDisconnected;
    }
    private void UnregisterClientEvents()
    {
        if (Client.Instance == null)
            return;

        Client.Instance.OnConnected -= OnClientConnected;
        Client.Instance.OnConnectionFailed -= OnConnectionFailed;
        Client.Instance.OnDisconnected -= OnClientDisconnected;
    }

    #endregion

    #region UI Events

    /*
     * OnDropdownChanged
     * 
     * What this receives:
     * index = selected dropdown item index.
     * 
     * What this does:
     * Applies the selected preset parameters to the input field.
     * Updates the preview text.
     */
    private void OnDropdownChanged(int index)
    {
        ApplySelectedPreset();
        UpdatePreview();
    }

    /*
     * OnParameterInputChanged
     * 
     * What this receives:
     * value = the current parameter input field text.
     * 
     * What this does:
     * Updates the preview text so the tester can see the final message.
     */
    private void OnParameterInputChanged(string value)
    {
        UpdatePreview();
    }
    private void OnSendClicked()
    {
        SendSelectedMessage();
    }

    /*
     * OnBurstClicked
     * 
     * What this does:
     * Starts or stops burst mode.
     * 
     * Burst mode sends the selected message many times.
     * This is useful for testing spam, repeated actions, or server stability.
     */
    private void OnBurstClicked()
    {
        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstCoroutine = null;

            if (burstButton != null)
                burstButton.GetComponentInChildren<TMP_Text>()?.SetText("Burst");

            Client.Log("MaliciousTester", "Burst stopped.");
            return;
        }

        if (!HasClient())
            return;

        if (!Client.Instance.IsConnected)
        {
            Client.Log("MaliciousTester", "Connect first or send one message to auto-connect before starting a burst.");
            ConnectRawClient();
            return;
        }

        burstCoroutine = StartCoroutine(SendBurst());
    }

    
    private void OnConnectClicked()
    {
        ConnectRawClient();
    }

    private void OnDisconnectClicked()
    {
        if (Client.Instance != null)
            Client.Instance.Disconnect("Malicious tester disconnect");
    }

    #endregion

    #region Client Events

    /*
     * OnClientConnected
     * 
     * What this does:
     * Called when Client connects successfully.
     * 
     * If there is a queued message from QueueMessageAndConnect,
     * this sends it now that the connection is ready.
     */
    private void OnClientConnected()
    {
        Client.Log("MaliciousTester", "Raw TCP connection ready.");

        if (!waitingForAutoConnect || queuedMessageAfterConnect == null)
            return;

        OSCMessageOut msg = queuedMessageAfterConnect;
        queuedMessageAfterConnect = null;
        waitingForAutoConnect = false;

        Client.Instance.Send(msg);
        Client.Log("MaliciousTester", "Sent after auto-connect: " + msg);
    }

    /*
     * OnConnectionFailed
     * 
     * What this receives:
     * reason = why the Client failed to connect.
     * 
     * What this does:
     * Clears the queued auto-connect message.
     */
    private void OnConnectionFailed(string reason)
    {
        waitingForAutoConnect = false;
        queuedMessageAfterConnect = null;
        Client.Log("MaliciousTester", "Connection failed: " + reason);
    }

    /*
     * OnClientDisconnected
     * 
     * What this receives:
     * reason = why the Client disconnected.
     * 
     * What this does:
     * Clears the queued auto-connect message.
     */
    private void OnClientDisconnected(string reason)
    {
        waitingForAutoConnect = false;
        queuedMessageAfterConnect = null;
        Client.Log("MaliciousTester", "Disconnected: " + reason);
    }

    #endregion

    #region Preset Logic
    private MessagePreset SelectedPreset()
    {
        if (presets.Count == 0)
            return null;

        int index = messageDropdown != null ? messageDropdown.value : 0;
        index = Mathf.Clamp(index, 0, presets.Count - 1);

        return presets[index];
    }
    private void ApplySelectedPreset()
    {
        MessagePreset preset = SelectedPreset();

        if (preset == null || parameterInput == null)
            return;

        parameterInput.text = preset.ExampleParameters;
    }

    #endregion

    #region Sending

    /*
     * SendSelectedMessage
     * 
     * What this does:
     * Builds the selected OSC message and sends it.
     * 
     * Flow:
     * 1. Check that Client.Instance exists.
     * 2. Build an OSCMessageOut from the selected preset and parameter input.
     * 3. If not connected, queue the message and connect first.
     * 4. If connected, send immediately.
     */
    private void SendSelectedMessage()
    {
        if (!HasClient())
            return;

        if (!TryBuildMessage(out OSCMessageOut msg, out string error))
        {
            Client.Log("MaliciousTester", "Failed to build message: " + error);
            UpdatePreview(error);
            return;
        }

        if (!Client.Instance.IsConnected)
        {
            QueueMessageAndConnect(msg);
            return;
        }

        Client.Instance.Send(msg);

        Client.Log("MaliciousTester", "Sent: " + msg);
    }

    /*
     * SendBurst
     * 
     * What this does:
     * Sends the selected message multiple times with a delay.
     * 
     * Data read from UI:
     * burstCountInput = how many messages to send.
     * burstDelayInput = delay between messages.
     * 
     * Limits:
     * count is clamped between 1 and 250.
     * delay is clamped between 0.01 and 2 seconds.
     */
    private IEnumerator SendBurst()
    {
        int count = ReadIntInput(burstCountInput, 10);
        float delay = ReadFloatInput(burstDelayInput, 0.05f);

        count = Mathf.Clamp(count, 1, 250);
        delay = Mathf.Clamp(delay, 0.01f, 2f);

        Client.Log("MaliciousTester", $"Starting burst. Count={count}, Delay={delay}");

        for (int i = 0; i < count; i++)
        {
            if (!CanSend())
                break;

            if (TryBuildMessage(out OSCMessageOut msg, out string error))
            {
                Client.Instance.Send(msg);
                Client.Log("MaliciousTester", $"Burst {i + 1}/{count}: {msg}");
            }
            else
            {
                Client.Log("MaliciousTester", "Burst build failed: " + error);
                break;
            }

            yield return new WaitForSeconds(delay);
        }

        burstCoroutine = null;

        Client.Log("MaliciousTester", "Burst finished.");
    }

    /*
     * CanSend
     * 
     * What this does:
     * Checks if the Client exists and is connected.
     * 
     * Returns:
     * true if a message can be sent right now.
     * false if sending should stop.
     */
    private bool CanSend()
    {
        if (!HasClient())
            return false;

        if (!Client.Instance.IsConnected)
        {
            Client.Log("MaliciousTester", "Client is not connected yet. Auto-connecting to server.");
            return false;
        }

        return true;
    }

    /*
     * HasClient
     * 
     * What this does:
     * Checks if the main Client singleton exists.
     * 
     * Returns:
     * true if Client.Instance exists.
     * false if it is missing.
     */
    private bool HasClient()
    {
        if (Client.Instance == null)
        {
            Client.Log("MaliciousTester", "Cannot send. Client.Instance missing.");
            return false;
        }

        return true;
    }

    /*
     * QueueMessageAndConnect
     * 
     * What this does:
     * Stores a message and connects to the server.
     * 
     * After connection succeeds:
     * OnClientConnected sends the stored message.
     */
    private void QueueMessageAndConnect(OSCMessageOut msg)
    {
        queuedMessageAfterConnect = msg;
        waitingForAutoConnect = true;

        Client.Log("MaliciousTester", "Queued message and connecting raw client: " + msg);
        ConnectRawClient();
    }

    /*
     * ConnectRawClient
     * 
     * What this does:
     * Connects the main Client to the server IP from the tester input.
     * 
     * Port:
     * Uses Msg.PORT.
     */
    private void ConnectRawClient()
    {
        if (!HasClient())
            return;

        if (Client.Instance.IsConnected)
        {
            Client.Log("MaliciousTester", "Already connected.");
            return;
        }

        string ip = ReadServerIp();
        Client.Log("MaliciousTester", $"Connecting raw tester to {ip}:{Msg.PORT}");
        Client.Instance.Connect(ip, Msg.PORT);
    }

    /*
     * ReadServerIp
     * 
     * What this does:
     * Reads the server IP in this order:
     * 1. serverIpInput text
     * 2. Client.Instance.ServerIP
     * 3. fallback 127.0.0.1
     */
    private string ReadServerIp()
    {
        if (serverIpInput != null && !string.IsNullOrWhiteSpace(serverIpInput.text))
            return serverIpInput.text.Trim();

        if (Client.Instance != null && !string.IsNullOrWhiteSpace(Client.Instance.ServerIP))
            return Client.Instance.ServerIP;

        return "127.0.0.1";
    }

    #endregion

    #region Message Building

    /*
     * TryBuildMessage
     * 
     * What this does:
     * Converts the selected preset and parameter text into an OSCMessageOut.
     * 
     * Output:
     * msg = the finished OSC message if successful.
     * error = reason why it failed.
     * 
     * Returns:
     * true if the message was built successfully.
     * false if the message could not be built.
     */
    private bool TryBuildMessage(out OSCMessageOut msg, out string error)
    {
        msg = null;
        error = null;

        MessagePreset preset = SelectedPreset();

        if (preset == null)
        {
            error = "No preset selected.";
            return false;
        }

        string rawParams = parameterInput != null ? parameterInput.text : "";

        if (!TryResolveAddressAndParams(preset, rawParams, out string address, out string parameterText, out error))
            return false;

        if (string.IsNullOrWhiteSpace(address) || !address.StartsWith("/"))
        {
            error = "OSC address must start with '/'.";
            return false;
        }

        msg = new OSCMessageOut(address);

        if (!TryAddParameters(msg, parameterText, out error))
            return false;

        return true;
    }

    /*
     * TryResolveAddressAndParams
     * 
     * What this does:
     * Decides which OSC address should be used.
     * 
     * Normal preset:
     * Uses preset.Address.
     * 
     * Custom preset:
     * The first token in the parameter input becomes the OSC address.
     * 
     * Custom example:
     * /c_select_dice | int:999
     * 
     * Result:
     * address = /c_select_dice
     * parameterText = int:999
     */
    private bool TryResolveAddressAndParams(MessagePreset preset, string rawParams, out string address, out string parameterText, out string error)
    {
        address = preset.Address;
        parameterText = rawParams;
        error = null;

        if (!preset.IsCustom)
            return true;

        string[] tokens = SplitParameterTokens(rawParams);

        if (tokens.Length == 0)
        {
            error = "Custom mode requires first token to be an OSC address.";
            return false;
        }

        address = tokens[0].Trim();

        if (!address.StartsWith("/"))
        {
            error = "First custom token must be an OSC address, for example /c_select_dice.";
            return false;
        }

        parameterText = string.Join(" | ", tokens.Skip(1));

        return true;
    }

    /*
     * TryAddParameters
     * 
     * What this does:
     * Splits the parameter text into separate tokens and adds each one to the OSC message.
     * 
     * Example input:
     * string:Test Room | int:25
     * 
     * Tokens:
     * string:Test Room
     * int:25
     */
    private bool TryAddParameters(OSCMessageOut msg, string parameterText, out string error)
    {
        error = null;

        string[] tokens = SplitParameterTokens(parameterText);

        foreach (string rawToken in tokens)
        {
            string token = rawToken.Trim();

            if (string.IsNullOrWhiteSpace(token))
                continue;

            if (!TryAddParameter(msg, token, out error))
                return false;
        }

        return true;
    }

    /*
     * TryAddParameter
     * 
     * What this does:
     * Adds one parameter to the OSCMessageOut.
     * 
     * Expected format:
     * type:value
     * 
     * Supported types:
     * int
     * float
     * bool
     * string
     * 
     * If no type is written, it tries auto typing in TryAddAutoTypedParameter.
     */
    private bool TryAddParameter(OSCMessageOut msg, string token, out string error)
    {
        error = null;

        int separatorIndex = token.IndexOf(':');

        if (separatorIndex <= 0)
        {
            return TryAddAutoTypedParameter(msg, token, out error);
        }

        string type = token.Substring(0, separatorIndex).Trim().ToLower();
        string value = token.Substring(separatorIndex + 1);

        switch (type)
        {
            case "int":
                if (!int.TryParse(value, out int intValue))
                {
                    error = $"Invalid int value: {value}";
                    return false;
                }

                msg.AddInt(intValue);
                return true;

            case "float":
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                {
                    error = $"Invalid float value: {value}";
                    return false;
                }

                msg.AddFloat(floatValue);
                return true;

            case "bool":
                if (!bool.TryParse(value, out bool boolValue))
                {
                    error = $"Invalid bool value: {value}";
                    return false;
                }

                msg.AddBool(boolValue);
                return true;

            case "string":
                msg.AddString(value);
                return true;

            default:
                error = $"Unknown parameter type: {type}. Use int, float, bool, or string.";
                return false;
        }
    }

    /*
     * TryAddAutoTypedParameter
     * 
     * What this does:
     * Adds a parameter when the tester did not write type:value.
     * 
     * Auto type order:
     * 1. Try int
     * 2. Try float
     * 3. Try bool
     * 4. If none match, use string
     * 
     * Example:
     * 123 becomes int.
     * 1.5 becomes float.
     * true becomes bool.
     * hello becomes string.
     */
    private bool TryAddAutoTypedParameter(OSCMessageOut msg, string token, out string error)
    {
        error = null;

        if (int.TryParse(token, out int intValue))
        {
            msg.AddInt(intValue);
            return true;
        }

        if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
        {
            msg.AddFloat(floatValue);
            return true;
        }

        if (bool.TryParse(token, out bool boolValue))
        {
            msg.AddBool(boolValue);
            return true;
        }

        msg.AddString(token);
        return true;
    }

    /*
     * SplitParameterTokens
     * 
     * What this does:
     * Splits parameter text by the | character.
     * Removes empty tokens.
     * Trims spaces around each token.
     * 
     * Example:
     * "string:Room | int:25"
     * becomes:
     * ["string:Room", "int:25"]
     */
    private string[] SplitParameterTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return text
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();
    }

    #endregion

    #region Preview

    /*
     * UpdatePreview
     * 
     * What this does:
     * Updates the preview UI text so the tester can see:
     * - selected preset
     * - final OSC address
     * - parameters
     * - purpose of the preset
     * 
     * If an error exists, it shows the error instead.
     */
    private void UpdatePreview(string error = null)
    {
        if (previewText == null)
            return;

        MessagePreset preset = SelectedPreset();

        if (preset == null)
        {
            previewText.text = "No preset selected.";
            return;
        }

        string rawParams = parameterInput != null ? parameterInput.text : "";

        TryResolveAddressAndParams(preset, rawParams, out string address, out string parameterText, out string resolveError);

        if (!string.IsNullOrEmpty(error))
        {
            previewText.text = "ERROR:\n" + error;
            return;
        }

        if (!string.IsNullOrEmpty(resolveError))
        {
            previewText.text = "ERROR:\n" + resolveError;
            return;
        }

        string[] tokens = SplitParameterTokens(parameterText);

        previewText.text =
            $"Preset: {preset.DisplayName}\n" +
            $"Address: {address}\n" +
            $"Params: {(tokens.Length == 0 ? "-" : string.Join(", ", tokens))}\n" +
            $"Purpose: {preset.Description}";
    }

    #endregion

    #region Input Helpers

    private int ReadIntInput(TMP_InputField input, int fallback)
    {
        if (input == null)
            return fallback;

        return int.TryParse(input.text, out int value) ? value : fallback;
    }

    /*
     * ReadFloatInput
     * 
     * What this does:
     * Reads a float from a TMP input field.
     * 
     * Uses CultureInfo.InvariantCulture so decimal points work consistently.
     * 
     * Returns:
     * parsed value if valid.
     * fallback value if input is missing or invalid.
     */
    private float ReadFloatInput(TMP_InputField input, float fallback)
    {
        if (input == null)
            return fallback;

        return float.TryParse(input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    #endregion

    #region Helper Types

    /*
     * MessagePreset
     * 
     * Purpose:
     * Stores one test message option for the dropdown.
     * 
     * DisplayName:
     * Text shown in the dropdown.
     * 
     * Address:
     * OSC address that will be sent.
     * Example: Msg.C_REGISTER or "/c_select_dice"
     * 
     * ExampleParameters:
     * Default text placed in the parameter input field.
     * 
     * Description:
     * Explains what this test message is for.
     * 
     * IsCustom:
     * If true, the first token in the parameter input is used as the OSC address.
     */
    private class MessagePreset
    {
        public string DisplayName;
        public string Address;
        public string ExampleParameters;
        public string Description;
        public bool IsCustom;

        public MessagePreset(
            string displayName,
            string address,
            string exampleParameters,
            string description,
            bool isCustom = false)
        {
            DisplayName = displayName;
            Address = address;
            ExampleParameters = exampleParameters;
            Description = description;
            IsCustom = isCustom;
        }
    }

    #endregion
}