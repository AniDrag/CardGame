using UnityEngine;

public class NetworkClientManager : MonoBehaviour
{
    public static NetworkClientManager Instance { get; private set; }

    // TCP client for lobby/rooms (reliable)
    public TcpClientService Tcp { get; private set; }

    // OSC client for game actions (low latency, you can add later)
    // public UdpOscClient Osc { get; private set; }

    [Header("Connection Info")]
    public string ServerIP = "127.0.0.1";
    public int ServerPort = 55000;

    public bool IsConnected => Tcp != null && Tcp.IsConnected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Create TCP client service
        Tcp = gameObject.AddComponent<TcpClientService>();
        // Optionally configure it here
    }

    public void ConnectToServer(string ip, int port)
    {
        ServerIP = ip;
        ServerPort = port;
        Tcp.Connect(ip, port);
    }

    public void Disconnect()
    {
        Tcp?.Disconnect("Client disconnected.");
    }

    private void OnDestroy()
    {
        Disconnect();
    }
}