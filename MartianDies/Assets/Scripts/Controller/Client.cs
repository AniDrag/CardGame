using AniDrag.Utility;
using CreeperDice_Net_Proj.Model;
using NetworkConnections;
using OSCTools;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Client : MonoBehaviour
{
    #region Singleton

    public static Client Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("Connection")]
    public string ServerIP = "127.0.0.1";
    public int ServerPort = 55000;
    public string Username;
    public string CurrentRoom;

    #endregion

    #region Heartbeat

    [Header("Heartbeat")]
    [SerializeField] private float pingIntervalSeconds = 1f;
    [SerializeField] private float serverTimeoutSeconds = 4f;

    private float nextPingTime;
    private float lastServerContactTime;

    #endregion

    #region Public State

    public int ClientId { get; set; } = -1;
    public bool IsConnected { get; private set; }

    #endregion

    #region Events

    public static event Action<string> OnConsoleLog;

    public event Action OnConnected;
    public event Action<string> OnDisconnected;
    public event Action<string> OnConnectionFailed;

    #endregion

    #region Private State

    private TcpNetworkConnection connection;
    private OSCDispatcher dispatcher;

    private bool isConnecting = false;
    private bool isQuitting = false;

    private readonly ConcurrentQueue<byte[]> incomingPackets = new();
    private readonly Dictionary<string, Coroutine> pendingTimeouts = new();
    private readonly List<OscListenerRegistration> listenerRegistrations = new();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!SetupSingleton())
            return;

        Application.quitting += OnApplicationQuitting;

        RegisterBuiltInListeners();
    }

    private void Update()
    {
        ReadNetworkPackets();
        ProcessQueuedPackets();
        DetectConnectionLost();
        UpdateHeartbeat();
    }

    private void OnDestroy()
    {
        Application.quitting -= OnApplicationQuitting;

        if (Instance != this)
            return;

        Disconnect("Client destroyed");

        Instance = null;
    }

    #endregion

    #region Setup

    private bool SetupSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return false;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        return true;
    }

    private void RegisterBuiltInListeners()
    {
        AddListener(Msg.S_SHUTDOWN, OnShutdown, OSCUtil.STRING);
        AddListener(Msg.S_SERVER_MESSAGE, OnServerMessage, OSCUtil.STRING);
        AddListener(Msg.S_DISCONNECT, OnShutdown, OSCUtil.STRING);
    }

    private void OnApplicationQuitting()
    {
        isQuitting = true;
        Disconnect("Application quitting");
    }

    #endregion

    #region Connection Lifecycle

    public async void Connect(string ip, int port)
    {
        if (isConnecting)
        {
            Log("System", "Connection already in progress...");
            return;
        }

        if (IsConnected)
        {
            Log("System", "Already connected.");
            return;
        }

        CleanupConnection();

        ServerIP = ip;
        ServerPort = port;
        isConnecting = true;

        try
        {
            connection = new TcpNetworkConnection(ip, port, asynchronous: true, fast: true);

            bool connected = await WaitForConnection(5f);

            if (!connected)
                throw new Exception("Connection failed or timed out.");

            dispatcher = new OSCDispatcher();
            dispatcher.ShowIncomingMessages = true;

            RegisterAllListenersToDispatcher();

            IsConnected = true;
            isConnecting = false;

            StartHeartbeat();

            Log("System", $"Connected to {ip}:{port}");

            OnConnected?.Invoke();
        }
        catch (Exception e)
        {
            Log("System", $"Connection failed: {e.Message}");

            StopHeartbeat();
            CleanupConnection();

            IsConnected = false;
            isConnecting = false;

            OnConnectionFailed?.Invoke(e.Message);
        }
    }

    public void Disconnect(string reason = "User requested")
    {
        bool shouldReturnToMainMenu = !isQuitting;

        if (!IsConnected && !isConnecting)
        {
            StopHeartbeat();
            CleanupConnection();
            ResetClientState();

            if (shouldReturnToMainMenu)
                ReturnToMainMenuIfNeeded();

            return;
        }

        StopHeartbeat();

        if (IsConnected && connection != null && connection.Status == ConnectionStatus.Connected)
            TrySendDisconnectMessage();

        CleanupConnection();
        CancelAllTimeouts();
        ResetClientState();

        Log("System", $"Disconnected: {reason}");

        if (shouldReturnToMainMenu)
        {
            OnDisconnected?.Invoke(reason);
            ReturnToMainMenuIfNeeded();
        }
    }

    private async Task<bool> WaitForConnection(float timeoutSeconds)
    {
        float startTime = Time.time;

        while (connection != null &&
               connection.Status == ConnectionStatus.Connecting &&
               Time.time - startTime < timeoutSeconds)
        {
            await Task.Delay(10);
        }

        return connection != null && connection.Status == ConnectionStatus.Connected;
    }

    private void CleanupConnection()
    {
        if (connection != null)
        {
            connection.Close();
            connection = null;
        }

        dispatcher = null;

        while (incomingPackets.TryDequeue(out _))
        {
            // Clear queued packets.
        }
    }

    private void ResetClientState()
    {
        IsConnected = false;
        isConnecting = false;
        CurrentRoom = null;
        ClientId = -1;
    }

    private void DetectConnectionLost()
    {
        if (!IsConnected || connection == null)
            return;

        if (connection.Status == ConnectionStatus.Connected)
            return;

        Log("System", "Connection lost.");
        Disconnect("Connection lost");
    }

    #endregion

    #region Packet Updating

    private void ReadNetworkPackets()
    {
        if (!IsConnected || connection == null)
            return;

        try
        {
            while (connection.Available() > 0)
            {
                byte[] packet = connection.GetPacket();

                if (packet != null)
                    incomingPackets.Enqueue(packet);
            }
        }
        catch (Exception e)
        {
            Log("System", "Connection error: " + e.Message);
            Disconnect("Connection error");
        }
    }

    private void ProcessQueuedPackets()
    {
        while (incomingPackets.TryDequeue(out byte[] packet))
        {
            lastServerContactTime = Time.realtimeSinceStartup;
            ProcessPacket(packet);
        }
    }

    private void ProcessPacket(byte[] data)
    {
        if (data == null)
            return;

        try
        {
            if (OSCObject.IsBundle(data))
            {
                ProcessBundle(data);
                return;
            }

            ProcessMessage(data);
        }
        catch (Exception e)
        {
            Log("Error", $"Exception in ProcessPacket: {e.Message}\n{e.StackTrace}");
        }
    }

    private void ProcessBundle(byte[] data)
    {
        OSCBundleIn bundle = new OSCBundleIn(data, null);

        if (!bundle.corrupt)
            Log("Server", bundle.ToString());
        else
            Log("Debug", "Corrupt bundle skipped.");
    }

    private void ProcessMessage(byte[] data)
    {
        OSCMessageIn msg = new OSCMessageIn(data);

        if (msg.corrupt)
        {
            Log("Debug", "Corrupt message skipped.");
            return;
        }

        Log("Server", msg.ToString());

        dispatcher?.HandlePacket(data, null);
    }

    #endregion

    #region Sending Messages

    public void Send(OSCMessageOut msg)
    {
        if (!IsConnected || connection == null)
        {
            Log("System", "Cannot send: not connected.");
            return;
        }

        try
        {
            byte[] packet = msg.GetBytes();

            connection.Send(packet);

            Log("Client", msg.ToString());
        }
        catch (Exception e)
        {
            Log("System", "Send failed: " + e.Message);
            Disconnect("Send failed");
        }
    }

    private void TrySendDisconnectMessage()
    {
        try
        {
            if (connection == null)
                return;

            var msg = new OSCMessageOut(Msg.C_DISCONNECT);

            byte[] packet = msg.GetBytes();

            connection.Send(packet);

            Log("Client", msg.ToString());
        }
        catch (Exception e)
        {
            Log("System", "Could not send disconnect message: " + e.Message);
        }
    }

    #endregion

    #region Message Registration

    public void AddListener(string address, Action<OSCMessageIn, IPEndPoint> handler, params string[] args)
    {
        if (HasListener(address, handler))
            return;

        listenerRegistrations.Add(new OscListenerRegistration(address, handler, args));

        if (dispatcher != null)
            dispatcher.AddListener(address, handler, args);
    }

    public void RemoveListener(string address, Action<OSCMessageIn, IPEndPoint> handler)
    {
        listenerRegistrations.RemoveAll(listener =>
            listener.Address == address &&
            listener.Handler == handler);

        dispatcher?.RemoveListener(address, handler);
    }

    private void RegisterAllListenersToDispatcher()
    {
        foreach (OscListenerRegistration listener in listenerRegistrations)
            dispatcher.AddListener(listener.Address, listener.Handler, listener.Args);
    }

    private bool HasListener(string address, Action<OSCMessageIn, IPEndPoint> handler)
    {
        foreach (OscListenerRegistration listener in listenerRegistrations)
        {
            if (listener.Address == address && listener.Handler == handler)
                return true;
        }

        return false;
    }

    #endregion

    #region Received Messages

    private void OnShutdown(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();

        Log("System", $"Server shutdown: {reason}");

        Disconnect(reason);
    }

    private void OnServerMessage(OSCMessageIn msg, IPEndPoint sender)
    {
        string message = msg.ReadString();

        Log("Server Broadcast", message);
    }

    #endregion

    #region Timeout Controls

    public void StartTimeout(string operationId, float duration, Action onTimeout)
    {
        if (pendingTimeouts.ContainsKey(operationId))
            CancelTimeout(operationId);

        pendingTimeouts[operationId] = StartCoroutine(TimeoutCoroutine(operationId, duration, onTimeout));
    }

    public void CancelTimeout(string operationId)
    {
        if (!pendingTimeouts.TryGetValue(operationId, out Coroutine coroutine))
            return;

        StopCoroutine(coroutine);
        pendingTimeouts.Remove(operationId);
    }

    private void CancelAllTimeouts()
    {
        foreach (Coroutine coroutine in pendingTimeouts.Values)
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
        }

        pendingTimeouts.Clear();
    }

    private IEnumerator TimeoutCoroutine(string operationId, float duration, Action onTimeout)
    {
        yield return new WaitForSeconds(duration);

        if (!pendingTimeouts.ContainsKey(operationId))
            yield break;

        pendingTimeouts.Remove(operationId);

        Log("System", $"Timeout: {operationId}");

        onTimeout?.Invoke();
    }

    #endregion

    #region Heartbeat

    private void StartHeartbeat()
    {
        lastServerContactTime = Time.realtimeSinceStartup;
        nextPingTime = Time.realtimeSinceStartup + pingIntervalSeconds;

        AddListener(Msg.S_PONG, OnPong);

        Log("Heartbeat", "Heartbeat started.");
    }

    private void StopHeartbeat()
    {
        RemoveListener(Msg.S_PONG, OnPong);
    }

    private void UpdateHeartbeat()
    {
        if (!IsConnected || connection == null)
            return;

        float now = Time.realtimeSinceStartup;

        if (now - lastServerContactTime >= serverTimeoutSeconds)
        {
            Log("Heartbeat", "Server timeout. Returning to main menu.");
            Disconnect("Server timeout");
            return;
        }

        if (now < nextPingTime)
            return;

        nextPingTime = now + pingIntervalSeconds;

        TrySendHeartbeatPing();
    }

    private void TrySendHeartbeatPing()
    {
        var msg = new OSCMessageOut(Msg.C_PING);

        Send(msg);
    }

    private void OnPong(OSCMessageIn msg, IPEndPoint sender)
    {
        lastServerContactTime = Time.realtimeSinceStartup;
    }

    #endregion

    #region Logging

    private static string GetTimestamp()
    {
        return DateTime.Now.ToString("HH:mm");
    }

    public static void Log(string sender, string message)
    {
        string formatted = $"[{GetTimestamp()}] {sender} | {message}";

        Debug.Log(formatted);

        OnConsoleLog?.Invoke(formatted);
    }

    public static void Log(string message)
    {
        Log("System", message);
    }

    #endregion

    #region Debug Testing

    [Button]
    public void Debug_SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        string[] tokens = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
            return;

        string address = tokens[0];

        if (!address.StartsWith("/"))
        {
            Debug.LogWarning($"Address '{address}' must start with '/'");
            return;
        }

        var msg = new OSCMessageOut(address);

        for (int i = 1; i < tokens.Length; i++)
            AddDebugParameter(msg, tokens[i]);

        Send(msg);
    }

    private void AddDebugParameter(OSCMessageOut msg, string token)
    {
        if (!token.StartsWith("/"))
            return;

        string trimmed = token.Substring(1);
        int underscoreIndex = trimmed.IndexOf('_');

        if (underscoreIndex == -1)
            return;

        string type = trimmed.Substring(0, underscoreIndex).ToLower();
        string value = trimmed.Substring(underscoreIndex + 1);

        switch (type)
        {
            case "bool":
                if (bool.TryParse(value, out bool boolValue))
                    msg.AddBool(boolValue);
                break;

            case "int":
                if (int.TryParse(value, out int intValue))
                    msg.AddInt(intValue);
                break;

            case "float":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                    msg.AddFloat(floatValue);
                break;

            case "string":
                msg.AddString(value);
                break;

            default:
                Debug.LogWarning($"Unknown debug parameter type: {type}");
                break;
        }
    }

    #endregion

    #region Helpers

    private void ReturnToMainMenuIfNeeded()
    {
        if (isQuitting)
            return;

        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.name == Scenes.MainMenu)
            return;

        SceneManager.LoadSceneAsync(Scenes.MainMenu);
    }

    private class OscListenerRegistration
    {
        public string Address;
        public Action<OSCMessageIn, IPEndPoint> Handler;
        public string[] Args;

        public OscListenerRegistration(string address, Action<OSCMessageIn, IPEndPoint> handler, string[] args)
        {
            Address = address;
            Handler = handler;
            Args = args;
        }
    }

    #endregion
}