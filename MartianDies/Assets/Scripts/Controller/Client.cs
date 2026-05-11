using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using OSCTools;

public class Client : MonoBehaviour
{
    public static Client Instance { get; private set; }

    [Header("Connection")]
    public string ServerIP = "127.0.0.1";
    public int ServerPort = 55000;
    public string Username;

    private UdpClient udpClient;
    private OSCDispatcher dispatcher;
    private IPEndPoint serverEndpoint;
    private Dictionary<string, Coroutine> pendingTimeouts = new();
    private bool isConnecting = false;
    private ConcurrentQueue<byte[]> incomingPackets = new();

    public bool IsConnected { get; private set; }
    public static event Action<string> OnConsoleLog;
    public event Action OnConnected;
    public event Action<string> OnDisconnected;  // string = reason
    /// <summary>
    /// Ok Follow allong.
    /// We get a client instance. THe central comunication tower. all messages are passed with it and accepthed with it.
    /// We also sub to 2 main methods the server publishes to us. and any Disconection handeling is handeled here
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // persistance
        Application.quitting += () => Disconnect(); 
        AddListener("/shutdown", OnShutdown, OSCUtil.STRING);
        AddListener("/server_message", OnServerMessage, OSCUtil.STRING);
    }

    /// <summary>
    /// Waits for messages, and handels their dispaching here. controllers listen to the actions
    /// </summary>
    private void Update()
    {
        dispatcher?.Update();
        // Process all queued packets on the main thread
        while (incomingPackets.TryDequeue(out byte[] packet))
            ProcessPacket(packet);
    }
    /// <summary>
    ///  Non persistent conection request to server. Default port is 55000
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    public void Connect(string ip, int port)
    {
        if (isConnecting)
        {
            Log("System", "Connecting in progres........");
            return;
        }

        if(IsConnected)
        {
            Log("System", "Already conected, why can you still press the button?");
        }
        
        // Clean up any previous failed/leftover connection
        CleanupConnection();

        isConnecting = true;

        // Validate and parse IP address
        if (!IPAddress.TryParse(ip, out IPAddress address))
        {
            Log("System", $"Connection failed: Invalid IP address '{ip}'");
            isConnecting = false;
            return;
        }


        ServerIP = ip;
        ServerPort = port;
        serverEndpoint = new IPEndPoint(IPAddress.Parse(ip), port);

        try
        {
            udpClient = new UdpClient(0);
            dispatcher = new OSCDispatcher();
            dispatcher.ShowIncomingMessages = true;

            udpClient.BeginReceive(OnReceive, null);
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

    /// <summary>
    /// Safely closes the UDP client and resets related state.
    /// </summary>
    private void CleanupConnection()
    {
        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error closing UDP client: {e.Message}");
            }
            udpClient = null;
        }
        dispatcher = null;
        serverEndpoint = null;
    }
    /// <summary>
    /// IAysncResult recives data and decompiles along side runtime code
    /// </summary>
    /// <param name="ar"></param>
    private void OnReceive(IAsyncResult ar)
    {
        try
        {
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udpClient.EndReceive(ar, ref sender);
            incomingPackets.Enqueue(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"Background receive error: {e.Message}");
        }
        finally
        {
            try { udpClient?.BeginReceive(OnReceive, null); } 
            catch { }
        }
    }

    private void ProcessPacket(byte[] data)
    {
        // This runs on the main thread
        if (data == null) return;

        // Raw log for debugging (using Debug.Log directly, which is thread-safe)
        Debug.Log($"[{DateTime.Now:HH:mm}] Debug | Raw packet: {data.Length} bytes");
        // This whole part handels my ing game Debuging system my Debug log. and displays coms with the server
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
                // Optionally log parsed details
                msg.ResetRead();
                while (msg.NextType() != 0)
                {
                    char t = msg.NextType();
                    if (t == OSCObject.INT)
                        Log("Debug", $"  int: {msg.ReadInt()}");
                    else if (t == OSCObject.STRING)
                        Log("Debug", $"  string: {msg.ReadString()}");
                    else
                        msg.ReadInt(); // skip
                }
                msg.ResetRead();
            }
            else
            {
                Log("Debug", $"Message corrupt");
            }
        }

        dispatcher?.HandlePacket(data, null);
    }

    public void Send(OSCMessageOut msg)
    {
        if (udpClient == null || serverEndpoint == null)
        {
            Log("System", "Cannot send: not connected.");
            return;
        }
        byte[] packet = msg.GetBytes();
        udpClient.Send(packet, packet.Length, serverEndpoint);
        Log("Client", msg.ToString());
    }

    public void AddListener(string address, Action<OSCMessageIn, IPEndPoint> handler, params string[] args)
    {
        dispatcher?.AddListener(address, handler, args);
    }

    public void RemoveListener(string address, Action<OSCMessageIn, IPEndPoint> handler)
    {
        dispatcher?.RemoveListener(address, handler);
    }

    /// <summary>
    /// When server shuts down we go to main menu.
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="sender"></param>
    private void OnShutdown(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();
        Log("System", $"Server shutdown: {reason}");

        // Clean up and return to main menu
        Disconnect(reason);
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("0_SC_MainMenu");
    }

    /// <summary>
    /// application crash or anything handeles this Disconect is called manualy too for when user wishes to disconect.
    /// </summary>
    /// <param name="reason"></param>
    public void Disconnect(string reason = "User requested")
    {
        if (!IsConnected) return;

        if (IsConnected && udpClient != null)
        {
            var disconnectMsg = new OSCMessageOut("/disconnect");
            Send(disconnectMsg);
        }

        foreach (var kvp in pendingTimeouts)
            StopCoroutine(kvp.Value);
        pendingTimeouts.Clear();

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }

        CleanupConnection();
        IsConnected = false;
        isConnecting = false;
        dispatcher = null;
        Log("System", $"Disconnected: {reason}");
        OnDisconnected?.Invoke(reason);
    }

    // --- Logging (now safe because called from main thread) ---
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
    #region Timeout controls
    public void StartTimeout(string operationId, float duration, Action onTimeout)
    {
        if (pendingTimeouts.ContainsKey(operationId))
            CancelTimeout(operationId);
        pendingTimeouts[operationId] = StartCoroutine(TimeoutCoroutine(operationId, duration, onTimeout));
    }

    private System.Collections.IEnumerator TimeoutCoroutine(string operationId, float duration, Action onTimeout)
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