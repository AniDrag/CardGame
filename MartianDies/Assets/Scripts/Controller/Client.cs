using NetworkConnections;   // Your TcpNetworkConnection class
using OSCTools;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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