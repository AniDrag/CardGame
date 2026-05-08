using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class TcpClientService : MonoBehaviour
{
    // No more hardcoded IP/port here
    private string serverIP;
    private int serverPort;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private bool isConnected = false;
    private bool isDisconnecting = false;

    public System.Action OnConnected;
    public System.Action<string> OnMessageReceived;
    public System.Action<string> OnDisconnected;

    public bool IsConnected => isConnected;

    // New method: pass IP and port when connecting
    public async void Connect(string ip, int port)
    {
        if (isConnected || isDisconnecting) return;

        serverIP = ip;
        serverPort = port;

        try
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(serverIP, serverPort);
            stream = tcpClient.GetStream();
            isConnected = true;
            OnConnected?.Invoke();

            _ = ListenForMessages();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Connection to {ip}:{port} failed: {e.Message}");
            OnDisconnected?.Invoke($"Connection failed: {e.Message}");
        }
    }

    // Optional: keep old overload for convenience
    public void Connect() => Connect(serverIP ?? "127.0.0.1", serverPort != 0 ? serverPort : 55000);

    private async Task ListenForMessages()
    {
        byte[] buffer = new byte[4096];
        while (isConnected && tcpClient.Connected)
        {
            try
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Disconnect("Server closed the connection.");
                    break;
                }
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                OnMessageReceived?.Invoke(message);
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"Socket error: {ex.Message}");
                Disconnect($"Network error: {ex.Message}");
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Unexpected error: {ex.Message}");
                Disconnect("Unexpected error.");
                break;
            }
        }
    }

    public void SendMessage(string message)
    {
        if (!isConnected || stream == null || !tcpClient.Connected)
        {
            Debug.LogWarning("Cannot send: not connected.");
            return;
        }
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            stream.Write(data, 0, data.Length);
        }
        catch (SocketException ex)
        {
            Debug.LogError($"Send failed: {ex.Message}");
            Disconnect("Send failed.");
        }
    }

    public void Disconnect(string reason = null)
    {
        if (!isConnected && !isDisconnecting) return;
        if (isDisconnecting) return;
        isDisconnecting = true;

        isConnected = false;
        try
        {
            stream?.Close();
            tcpClient?.Close();
        }
        catch { }
        finally
        {
            stream = null;
            tcpClient = null;
            isDisconnecting = false;
        }
        OnDisconnected?.Invoke(reason ?? "Disconnected.");
    }

    public async void ReconnectWithDelay(float delaySeconds = 2.0f)
    {
        if (isConnected) return;
        Disconnect();
        await Task.Delay((int)(delaySeconds * 1000));
        Connect(); // uses stored IP/port
    }

    void OnDestroy() => Disconnect("Application quitting.");
}