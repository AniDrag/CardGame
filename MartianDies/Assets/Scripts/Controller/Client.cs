using AniDrag.Utility;
using NetworkConnections;   // Your TcpNetworkConnection class
using OSCTools;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

public class Client : MonoBehaviour
{
    public static Client Instance { get; private set; }

    [Header("Connection")]
    public string ServerIP = "127.0.0.1";
    public int ServerPort = 55000;
    public string Username;
    public string CurrentRoom;

    private TcpNetworkConnection connection;
    private OSCDispatcher dispatcher;
    private bool isConnecting = false;
    private ConcurrentQueue<byte[]> incomingPackets = new();
    private Dictionary<string, Coroutine> pendingTimeouts = new();

    public bool IsConnected { get; private set; }
    public static event Action<string> OnConsoleLog;
    public event Action OnConnected;
    public event Action<string> OnDisconnected;
    private List<Action> pendingListeners = new();
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.quitting += () => Disconnect();
        AddListener(Msg.S_SHUTDOWN, OnShutdown, OSCUtil.STRING);
        AddListener(Msg.S_SERVER_MESSAGE, OnServerMessage, OSCUtil.STRING);
        AddListener(Msg.S_DISCONNECT, OnShutdown, OSCUtil.STRING);
    }

    private void Update()
    {
        while (incomingPackets.TryDequeue(out byte[] packet))
            ProcessPacket(packet);

        if (IsConnected && connection != null)
        {
            while (connection.Available() > 0)
            {
                byte[] packet = connection.GetPacket();
                if (packet != null)
                    incomingPackets.Enqueue(packet);
            }
        }
    }

    public async void Connect(string ip, int port)
    {
        if (isConnecting)
        {
            Log("System", "Connecting in progress...");
            return;
        }
        if (IsConnected)
        {
            Log("System", "Already connected.");
            return;
        }

        CleanupConnection();
        isConnecting = true;

        try
        {
            connection = new TcpNetworkConnection(ip, port, asynchronous: true, fast: true);

            float timeout = 5f;
            float startTime = Time.time;
            while (connection.Status == ConnectionStatus.Connecting && Time.time - startTime < timeout)
                await Task.Delay(10);

            if (connection.Status != ConnectionStatus.Connected)
                throw new Exception("Connection failed or timeout");

            dispatcher = new OSCDispatcher();
            dispatcher.ShowIncomingMessages = true;

            // Process any listeners that were queued before connection was ready
            foreach (var action in pendingListeners)
            {
                action?.Invoke();
            }
            pendingListeners.Clear();

            IsConnected = true;
            isConnecting = false;

            Log("System", $"Connected to {ip}:{port}");
            OnConnected?.Invoke();
        }
        catch (Exception e)
        {
            Log("System", $"Connection failed: {e.Message}");
            CleanupConnection();
            isConnecting = false;
            IsConnected = false;
        }
    }

    private void CleanupConnection()
    {
        if (connection != null)
        {
            connection.Close();
            connection = null;
        }
        dispatcher = null;
    }

    private void ProcessPacket(byte[] data)
    {
        if (data == null) return;

        try
        {
            if (OSCObject.IsBundle(data))
            {
                OSCBundleIn bundle = new OSCBundleIn(data, null);
                if (!bundle.corrupt)
                    Log("Server", bundle.ToString());
            }
            else
            {
                OSCMessageIn msg = new OSCMessageIn(data);
                if (!msg.corrupt)
                {
                    Log("Server", msg.ToString());
                    dispatcher?.HandlePacket(data, null);
                }
                else
                {
                    Log("Debug", "Corrupt message, skipping.");
                }
            }
        }
        catch (Exception e)
        {
            Log("Error", $"Exception in ProcessPacket: {e.Message}\n{e.StackTrace}");
        }
    }

    public void Send(OSCMessageOut msg)
    {
        if (!IsConnected || connection == null)
        {
            Log("System", "Cannot send: not connected.");
            return;
        }
        byte[] packet = msg.GetBytes();
        connection.Send(packet);
        Log("Client", msg.ToString());
    }

    public void AddListener(string address, Action<OSCMessageIn, IPEndPoint> handler, params string[] args)
    {
        if (dispatcher != null)
        {
            dispatcher.AddListener(address, handler, args);
        }
        else
        {
            // Queue the listener for later
            pendingListeners.Add(() => dispatcher.AddListener(address, handler, args));
            Log("Debug", $"Queued listener for {address}");
        }
    }

    public void RemoveListener(string address, Action<OSCMessageIn, IPEndPoint> handler)
    {
        dispatcher?.RemoveListener(address, handler);
    }

    private void OnShutdown(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();
        Log("System", $"Server shutdown: {reason}");
        Disconnect(reason);
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(Scenes.MainMenu);
    }

    public void Disconnect(string reason = "User requested")
    {
        if (!IsConnected) return;

        if (IsConnected && connection != null && connection.Status == ConnectionStatus.Connected)
        {
            var disconnectMsg = new OSCMessageOut(Msg.C_DISCONNECT);
            Send(disconnectMsg);
        }

        CleanupConnection();
        IsConnected = false;
        isConnecting = false;
        CurrentRoom = null;
        Log("System", $"Disconnected: {reason}");
        OnDisconnected?.Invoke(reason);
    }

    private static string GetTimestamp() => DateTime.Now.ToString("HH:mm");
    public static void Log(string sender, string message)
    {
        string formatted = $"[{GetTimestamp()}] {sender} | {message}";
        Debug.Log(formatted);
        OnConsoleLog?.Invoke(formatted);
    }
    public static void Log(string message) => Log("System", message);

    private void OnDestroy() => Disconnect();

    private void OnServerMessage(OSCMessageIn msg, IPEndPoint sender)
    {
        string message = msg.ReadString();
        Log("Server Broadcast", message);
    }

    [Button]
    public void Debug_SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        string[] tokens = message.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return;

        // First token = OSC address (header)
        string address = tokens[0];
        if (!address.StartsWith("/"))
        {
            Debug.LogWarning($"Address '{address}' must start with '/'");
            return;
        }
        var msg = new OSCMessageOut(address);

        // Parse parameters from the remaining tokens
        for (int i = 1; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!token.StartsWith("/")) continue; // safety

            string trimmed = token.Substring(1);          // remove leading '/'
            int underscoreIndex = trimmed.IndexOf('_');
            if (underscoreIndex == -1) continue;          // skip invalid tokens

            string type = trimmed.Substring(0, underscoreIndex).ToLower();
            string value = trimmed.Substring(underscoreIndex + 1);

            switch (type)
            {
                case "bool":
                    if (bool.TryParse(value, out bool b))
                        msg.AddBool(b);
                    break;

                case "int":
                    if (int.TryParse(value, out int j))
                        msg.AddInt(j);
                    break;

                case "float":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                        msg.AddFloat(f);
                    break;

                case "string":
                    msg.AddString(value);
                    break;

                default:
                    // Unknown type – add as string .. mybe? could be good for testing 
                    Debug.LogWarning("[Client] - [Debug message] | type wmismatch incorect string attached or i deserialized it wrong");
                    // msg.AddString(value);
                    break;
            }
        }

        Send(msg);
    }

    // Map headers to actual OSC addresses
    private string GetAddressFromHeader(string header)
    {
        // Add your own mappings here
        switch (header)
        {
            case "initial": return Msg.C_CLOSE_ROOM;   // e.g., "/close_room"
                                                       // case "another": return "/some/other/address";
            default: return null; // unknown header
        }
    }

    #region Timeout controls
    public void StartTimeout(string operationId, float duration, Action onTimeout)
    {
        if (pendingTimeouts.ContainsKey(operationId))
            CancelTimeout(operationId);
        pendingTimeouts[operationId] = StartCoroutine(TimeoutCoroutine(operationId, duration, onTimeout));
    }

    private IEnumerator TimeoutCoroutine(string operationId, float duration, Action onTimeout)
    {
        yield return new WaitForSeconds(duration);
        if (pendingTimeouts.ContainsKey(operationId))
        {
            pendingTimeouts.Remove(operationId);
            Log("System", $"Timeout: {operationId}");
            onTimeout?.Invoke();
        }
    }

    public void CancelTimeout(string operationId)
    {
        if (pendingTimeouts.TryGetValue(operationId, out Coroutine coroutine))
        {
            StopCoroutine(coroutine);
            pendingTimeouts.Remove(operationId);
        }
    }
    #endregion
}

/*
Q & A session

Q1: Why use the Singleton pattern (Instance, Awake, DontDestroyOnLoad)?
A1: The Client is a core network manager that must exist globally and persist across scene loads. 
    The Singleton ensures only one instance is ever active, and DontDestroyOnLoad keeps it alive 
    during level transitions. This simplifies access from any script via Client.Instance.

Q2: Why use TcpNetworkConnection with asynchronous = true and fast = true?
A2: Asynchronous mode prevents blocking the main thread during network I/O, which is critical 
    for real-time applications (Unity’s main thread must stay responsive). The "fast" flag 
    likely enables optimised buffer handling for low-latency messaging.

Q3: Why use a ConcurrentQueue<byte[]> for incoming packets instead of processing directly in Update?
A3: Incoming packets arrive on a background thread (from TcpNetworkConnection). Directly processing 
    them on that thread would cause thread-safety issues and potential crashes. The ConcurrentQueue 
    allows safe, lock-free enqueue from the network thread and dequeue on the main thread (Update) 
    for processing, following the standard producer-consumer pattern.

Q4: Why separate packet receiving (connection.Available()) and processing (ProcessPacket)?
A4: This separation keeps the Update loop lightweight. We first collect all pending packets into 
    the queue, then process them in a while loop. This avoids holding the connection while processing 
    and allows batching, improving throughput.

Q5: Why use an OSCDispatcher and why queue listeners with pendingListeners?
A5: OSCDispatcher handles routing incoming OSC messages to registered callbacks. However, the dispatcher 
    is only created after a successful connection. To avoid missing early listener registrations 
    (e.g., during Awake/Start before Connect is called), we store them in pendingListeners and apply 
    them once the dispatcher is ready. This decouples listener setup from connection state.

Q6: Why use async/await in Connect() with a timeout loop instead of a coroutine?
A6: async/await provides cleaner asynchronous flow control. The while loop with Task.Delay checks 
    connection status without blocking the main thread, and the 5-second timeout prevents hanging 
    indefinitely. This is simpler than managing a custom coroutine with yield return.

Q7: Why have separate CleanupConnection() and Disconnect() methods?
A7: CleanupConnection() resets the internal network objects (TcpNetworkConnection, dispatcher) without 
    sending a disconnect message. It is used internally for failure recovery. Disconnect() sends a 
    proper C_DISCONNECT OSC message to the server before cleaning up, ensuring a graceful shutdown.

Q8: Why process both OSC bundles and single messages in ProcessPacket?
A8: OSC supports both individual messages and bundles (which contain multiple messages with timestamps). 
    The server may send either. Our code handles both by checking OSCObject.IsBundle(data) and acting 
    accordingly. This ensures compatibility with all OSC traffic.

Q9: Why use AddListener/RemoveListener instead of directly subscribing to dispatcher?
A9: The AddListener method handles the pending listener queue transparently, so external code doesn’t 
    need to check if the dispatcher exists. This hides complexity and reduces bugs. It also centralises 
    listener management, making it easier to add logging or debouncing later.

Q10: Why use static Log methods with an OnConsoleLog event?
A10: Static logging allows any part of the code to log messages without holding a Client instance. 
     The OnConsoleLog event allows external UI (e.g., an in-game console) to display logs without 
     coupling to the Client. The timestamp improves readability.

Q11: Why include a Debug_SendMessage method with a custom deserializer?
A11: This provides a quick, text?based way to send any OSC message for testing purposes. It parses 
     typed parameters (/bool_, /int_, /float_, /string_) from a single string, making it easy to 
     simulate server commands from a debug input field or console. It reduces the need to write 
     separate methods for each message type.

Q12: Why use CultureInfo.InvariantCulture when parsing floats?
A12: Floats are typically formatted with a dot (.) as the decimal separator. On systems where the 
     current culture uses a comma (e.g., "123,45"), parsing would fail. InvariantCulture ensures 
     consistent interpretation across all locales, so "123.45" always works.

Q13: Why ignore extra address tokens (like a second /header) in Debug_SendMessage?
A13: An OSC message has only one address. If the user types multiple slash?prefixed tokens without 
     underscores, the first is the address and the rest are meaningless. We skip them to avoid 
     accidentally adding garbage to the message. This matches the requirement: "if there is one 
     already then we can't do anything".

Q14: Why implement timeouts (StartTimeout/CancelTimeout) with Coroutines?
A14: Many network operations (e.g., waiting for a server response) need a timeout to prevent 
     indefinite blocking. Using coroutines with WaitForSeconds allows a clean, non?blocking 
     cancellation mechanism. The dictionary tracks active timeouts so they can be cancelled 
     individually by operation ID.

Q15: Why use OnDestroy to call Disconnect()?
A15: Ensures that when the Client GameObject is destroyed (e.g., application quit), the connection 
     is properly closed and the server is notified. This prevents stale connections on the server side.

Q16: Why is the connection status checked in Update before reading packets?
A16: The connection may become disconnected at any time. Checking IsConnected and connection != null 
     before calling connection.Available() avoids null reference exceptions and ensures we only 
     attempt to read from a valid, open connection.

Q17: Why use Application.quitting += () => Disconnect(); in Awake?
A17: When the application exits, the OnDestroy may not be called on all objects (depending on 
     execution order). Subscribing to Application.quitting gives an early notification to 
     gracefully close the connection before the process terminates, ensuring a clean shutdown.

Q18: Why have both IsConnected and isConnecting flags?
A18: IsConnected reflects a stable, fully established connection. isConnecting tracks an ongoing 
     asynchronous connection attempt. This prevents multiple simultaneous connection attempts and 
     allows the UI to show a "connecting..." state without falsely indicating a live connection.

Q19: Why use Unity's [Button] attribute on Debug_SendMessage?
A19: The [Button] attribute (likely from Odin Inspector or a similar plugin) exposes the method 
     as a clickable button in the Unity Inspector. This is extremely useful for quick manual testing – 
     you can type a message in an input field and press the button to send it without writing additional UI code.

Q20: Why log both "Server" and "Client" messages separately?
A20: Separating logs by direction (incoming vs outgoing) makes debugging network communication 
     much easier. It helps identify whether issues are on the client side (send) or server side 
     (receive/processing). The timestamp and sender prefix provide a clear audit trail.
*/