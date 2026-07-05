using AniDrag.Utility;
using AniDrag.Utility.Inspector;
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
/// <summary>
/// Persistent Unity networking client. It owns the socket, routes server OSC messages,
/// and gives scene scripts a clean listener API.
/// </summary>
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
    [SerializeField] private float pingIntervalSeconds = 5f;
    [SerializeField] private float serverTimeoutSeconds = 10f;

    private float nextPingTime;
    private float lastServerContactTime;

    #endregion

    #region Public State

    public int ClientId { get; set; } = -1;
    public bool IsConnected { get; private set; }
    public int CurrentPointGoal { get; set; } = 25;
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


    // Thread-safe packet queue. Network packets are stored here first,
    // then handled in Update() on the Unity main thread.
    private readonly ConcurrentQueue<byte[]> incomingPackets = new();

    // Tracks temporary operation timeouts, for example waiting for a server response.
    // Key = custom operation id, Value = running timeout coroutine.
    private readonly Dictionary<string, Coroutine> pendingTimeouts = new();

    // Stores all requested listeners even before the dispatcher exists.
    // This lets scene scripts register listeners before or after Connect().
    private readonly List<OscListenerRegistration> listenerRegistrations = new();

    #endregion

    #region Unity Lifecycle
    // What:
    // Initializes the persistent network client singleton.
    //
    // How:
    // Runs before most scene scripts. The first Client becomes Instance and is kept
    // between scenes. Any duplicate Client object is destroyed.
    //
    // Important:
    // Built-in listeners are registered here, but they are not attached to an
    // OSCDispatcher until Connect() creates the dispatcher.
    private void Awake()
    {
        if (!SetupSingleton())
            return;

        Application.quitting += OnApplicationQuitting;

        RegisterBuiltInListeners();
    }

    // What:
    // Runs the client networking loop once per Unity frame.
    //
    // Order:
    // 1. Let OSCDispatcher do any internal update work.
    // 2. Read all available TCP packets into incomingPackets.
    // 3. Process queued packets into OSC messages.
    // 4. Detect socket disconnects.
    // 5. Send heartbeat ping or timeout if needed.
    private void Update()
    {
        dispatcher?.Update();

        ReadNetworkPackets();
        ProcessQueuedPackets();
        DetectConnectionLost();
        UpdateHeartbeat();
    }
    // What: Cleans up the network client when Unity destroys this object.
    // How: Removes listeners and closes the socket unless the application is already quitting.
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
    // What: Guarantees that only one Client instance exists.
    // How: Stores this component in the static Instance field and destroys duplicates created by scene reloads.
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

    // What: Registers client-level server messages that every scene can use.
    // How: Hooks common messages like errors and disconnect responses before game/lobby-specific controllers add their listeners.
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

    // What:
    // Starts a TCP connection attempt to the server.
    //
    // Parameters:
    // ip   = server IP address, for example 127.0.0.1 for local testing.
    // port = TCP port, for example 55000.
    //
    // How it works:
    // 1. Blocks duplicate connects with isConnecting.
    // 2. Clears any old connection.
    // 3. Creates TcpNetworkConnection in async mode.
    // 4. Waits up to 5 seconds for the socket to connect.
    // 5. Creates OSCDispatcher and re-registers all listeners.
    // 6. Starts heartbeat and fires OnConnected.
    //
    // Failure:
    // If the socket fails or times out, it cleans everything and fires OnConnectionFailed.
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


    /// What:
    // Disconnects this client from the server.
    //
    // Parameter:
    // reason = text used for logs and UI callbacks.
    //
    // How it works:
    // 1. Stops heartbeat.
    // 2. If still connected, tries to send Msg.C_DISCONNECT first.
    // 3. Closes the socket and clears packet queues.
    // 4. Cancels pending operation timeouts.
    // 5. Resets session state like ClientId and CurrentRoom.
    // 6. Returns to the main menu unless Unity is quitting.
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

    // What:
    // Waits until the TCP connection either succeeds or fails.
    //
    // Return:
    // true  = connection.Status became Connected before timeout.
    // false = timeout reached, connection closed, or connection failed.
    //
    // Why async:
    // This avoids freezing Unity while the socket is still connecting.
    private async Task<bool> WaitForConnection(float timeoutSeconds)
    {
        float startTime = Time.time;

        while (connection != null &&  connection.Status == ConnectionStatus.Connecting && Time.time - startTime < timeoutSeconds)
        {
            await Task.Delay(10);
        }

        return connection != null && connection.Status == ConnectionStatus.Connected;
    }

    // What: Safely closes and clears the active TCP connection.
    // How: Stops using the socket, removes temporary state, and prevents later sends from using a dead connection.
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
    // What: Resets data that belongs to the current server session.
    // How: Clears client id, player name, room name, and connection flags after disconnect or failure.
    private void ResetClientState()
    {
        IsConnected = false;
        isConnecting = false;
        CurrentRoom = null;
        ClientId = -1;
    }

    // What: Detects unexpected server disconnects.
    // How: Checks the connection status every frame and fires the same cleanup path as a normal disconnect.
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

    // What:
    // Reads every full OSC packet currently waiting on the socket.
    //
    // Data received:
    // connection.GetPacket() returns raw byte[] data. That byte[] should be one
    // complete OSC packet sent by the server.
    //
    // Why queue it:
    // This method only collects packets. ProcessQueuedPackets() later parses them
    // on the Unity frame, where it is safer to call gameplay/UI listeners.
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

    // What:
    // Dispatches received packets to OSC listeners.
    //
    // Data received:
    // incomingPackets.TryDequeue(out byte[] packet)
    //
    // "out byte[] packet" means:
    // TryDequeue returns true if a packet existed. When it returns true, the
    // removed packet is placed into the packet variable.
    //
    // Why update lastServerContactTime here:
    // Any packet from the server counts as proof that the server is still alive.
    private void ProcessQueuedPackets()
    {
        while (incomingPackets.TryDequeue(out byte[] packet))
        {
            lastServerContactTime = Time.realtimeSinceStartup;
            ProcessPacket(packet);
        }
    }

    // What:
    // Processes one raw network packet from the server.
    //
    // Data received:
    // data = raw byte[] from TcpNetworkConnection.GetPacket().
    //
    // How:
    // - If the packet is an OSC bundle, ProcessBundle handles it.
    // - If it is a normal OSC message, ProcessMessage handles it.
    //
    // Safety:
    // Exceptions are caught here so one bad packet does not crash the client.
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

    // What:
    // Processes an OSC bundle packet.
    //
    // Data received:
    // data = raw byte[] containing a bundle of OSC messages.
    //
    // Current behavior:
    // This currently logs the bundle. If you later use bundles for real gameplay,
    // this is where each message inside the bundle should be extracted and sent
    // through dispatcher.HandlePacket(...).
    private void ProcessBundle(byte[] data)
    {
        OSCBundleIn bundle = new OSCBundleIn(data, null);

        if (!bundle.corrupt)
            Log("Server", bundle.ToString());
        else
            Log("Debug", "Corrupt bundle skipped.");
    }

    // What:
    // Processes one OSC message from the server.
    //
    // Data received:
    // data = raw byte[] containing one OSC message.
    //
    // Important:
    // OSCMessageIn msg = new OSCMessageIn(data) parses the bytes so we can check
    // if the message is corrupt and log the readable version.
    //
    // dispatcher.HandlePacket(data, null):
    // Sends the same raw packet to OSCDispatcher. The dispatcher checks the OSC
    // address and calls every listener registered for that address.
    //
    // Why sender is null:
    // This is a TCP client. We already know the only sender is the connected server,
    // so there is no UDP IPEndPoint sender to pass in.
    private void ProcessMessage(byte[] data)
    {
        OSCMessageIn msg = new OSCMessageIn(data);

        // Corrupt means the packet did not match a valid OSC message format.
        // We skip it because reading payload values from it would be unsafe.
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

    // What:
    // Sends an OSC message to the server.
    //
    // Data sent:
    // msg = OSCMessageOut with:
    // - address, for example Msg.C_PING
    // - optional payload values added with AddInt, AddString, AddBool, etc.
    //
    // How:
    // msg.GetBytes() serializes the OSC message into raw byte[].
    // connection.Send(packet) writes those bytes to the TCP socket.
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

    // What:
    // Tries to tell the server this client is leaving.
    //
    // Message sent:
    // Address: Msg.C_DISCONNECT
    // Payload: none
    //
    // Why failures are ignored:
    // Disconnect can happen because the connection is already broken, so this send
    // is helpful but not guaranteed.
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
    // What:
    // Adds a callback for a server OSC message.
    //
    // Parameters:
    // address = OSC address to listen for, for example Msg.S_SERVER_MESSAGE.
    // handler = method that receives (OSCMessageIn msg, IPEndPoint sender).
    // args    = optional expected OSC argument types. Example: OSCUtil.STRING.
    //
    // How it works:
    // The listener is stored in listenerRegistrations first. This is important
    // because the dispatcher only exists after Connect(). When the dispatcher is
    // already alive, the listener is also added to it immediately.
    //
    // Example:
    // AddListener(Msg.S_SERVER_MESSAGE, OnServerMessage, OSCUtil.STRING)
    // means OnServerMessage expects:
    //   [0] string message
    public void AddListener(string address, Action<OSCMessageIn, IPEndPoint> handler, params string[] args)
    {
        if (HasListener(address, handler))
            return;

        listenerRegistrations.Add(new OscListenerRegistration(address, handler, args));

        if (dispatcher != null)
            dispatcher.AddListener(address, handler, args);
    }

    // What:
    // Removes a previously registered server-message callback.
    //
    // Why:
    // Scene objects should remove their listeners in OnDestroy. Otherwise a
    // destroyed lobby/game UI script could still receive server messages.
    public void RemoveListener(string address, Action<OSCMessageIn, IPEndPoint> handler)
    {
        listenerRegistrations.RemoveAll(listener =>
            listener.Address == address &&
            listener.Handler == handler);

        dispatcher?.RemoveListener(address, handler);
    }

    // What:
    // Re-applies every stored listener to a newly created dispatcher.
    //
    // Why:
    // Scripts may call AddListener before the client connects. This method makes
    // sure those early listeners still work once Connect() creates the dispatcher.
    private void RegisterAllListenersToDispatcher()
    {
        foreach (OscListenerRegistration listener in listenerRegistrations)
            dispatcher.AddListener(listener.Address, listener.Handler, listener.Args);
    }
    // What:
    // Prevents the same handler being registered twice for the same OSC address.
    //
    // Why:
    // Duplicate listeners would make one server message call the same method
    // multiple times, which can create duplicated UI updates or duplicated actions.
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
    // Incoming message:
    // Address: Msg.S_SHUTDOWN or Msg.S_DISCONNECT
    // Payload:
    //   [0] string reason
    //
    // Example:
    //   Server sends reason = "Server closed"
    //
    // Result:
    // The client logs the reason, closes the connection, clears state, and returns
    // to the main menu.
    private void OnShutdown(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();

        Log("System", $"Server shutdown: {reason}");

        Disconnect(reason);
    }
    // Incoming message:
    // Address: Msg.S_SERVER_MESSAGE
    // Payload:
    //   [0] string message
    //
    // Example:
    //   Server sends message = "Room update failed"
    //
    // Result:
    // The message is written to the in-game console / Unity Console.
    private void OnServerMessage(OSCMessageIn msg, IPEndPoint sender)
    {
        string message = msg.ReadString();

        Log("Server Broadcast", message);
    }

    #endregion

    #region Timeout Controls
    // Timeout controlls, They are self explanaotry Corutines with a Msg.TIMEOUT_ name or timeout.
    // This logs any timout required and invokes the On TimeOut.

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

    // Hartbeat system to keep the connection alive and detect server timeouts. 
    // It sends periodic ping messages to the server and expects pong responses. 
    // If no response is received within the specified timeout, the client will disconnect and return to the main menu.

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

    // What: Writes a namespaced client debug message.
    // How: Adds a small category prefix so network, lobby, and game logs are easier to filter.
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

    // Debug tool for manually sending OSC messages from the inspector.
    //
    // Input format:
    //   /address /type_value /type_value
    //
    // Supported parameter tokens:
    //   /bool_true
    //   /int_5
    //   /float_1.5
    //   /string_PlayerName
    //
    // Example:
    //   /c_debug_message /string_Hello /int_3
    //
    // Result:
    // Sends an OSCMessageOut with address /c_debug_message, then a string, then an int.
    //
    // This is mostly not in use anymore. The malicious client has taken over the job.
    [DebugButton]
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
    // What:
    // Converts one debug text token into a typed OSC payload value.
    //
    // Example:
    // token "/int_3" calls msg.AddInt(3)
    // token "/string_Hello" calls msg.AddString("Hello")
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
    // What:
    // Loads the main menu after disconnects, but only when needed.
    //
    // Why:
    // If the player is in Lobby or Game and loses connection, they should not stay
    // in a scene that depends on server state.
    private void ReturnToMainMenuIfNeeded()
    {
        if (isQuitting)
            return;

        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.name == Scenes.MainMenu)
            return;

        SceneManager.LoadSceneAsync(Scenes.MainMenu);
    }
    // Stores listener data even when the dispatcher does not exist yet.
    //
    // Address:
    //   OSC address to listen for.
    // Handler:
    //   Callback method to run when that address arrives.
    // Args:
    //   Optional OSC argument type expectations.
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