using OSCTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private OSCMessageOut queuedMessageAfterConnect;
    private bool waitingForAutoConnect;

    #endregion

    #region Unity Lifecycle

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

    private void OnDropdownChanged(int index)
    {
        ApplySelectedPreset();
        UpdatePreview();
    }

    private void OnParameterInputChanged(string value)
    {
        UpdatePreview();
    }

    private void OnSendClicked()
    {
        SendSelectedMessage();
    }

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

    private void OnConnectionFailed(string reason)
    {
        waitingForAutoConnect = false;
        queuedMessageAfterConnect = null;
        Client.Log("MaliciousTester", "Connection failed: " + reason);
    }

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

    private bool HasClient()
    {
        if (Client.Instance == null)
        {
            Client.Log("MaliciousTester", "Cannot send. Client.Instance missing.");
            return false;
        }

        return true;
    }

    private void QueueMessageAndConnect(OSCMessageOut msg)
    {
        queuedMessageAfterConnect = msg;
        waitingForAutoConnect = true;

        Client.Log("MaliciousTester", "Queued message and connecting raw client: " + msg);
        ConnectRawClient();
    }

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

    private bool TryResolveAddressAndParams(
        MessagePreset preset,
        string rawParams,
        out string address,
        out string parameterText,
        out string error)
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