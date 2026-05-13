using CreeperDies_Net_Proj.Model;
using NetworkConnections;
using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class TcpServer
{
    private readonly object _sync = new object();
    private TcpListener _listener;
    private List<TcpNetworkConnection> _connections = new();
    private OSCDispatcher _dispatcher;
    private bool _isShuttingDown;

    private int _nextId = 1;
    private Dictionary<int, ClientInfo> _clients = new();
    private Dictionary<string, RoomData> _rooms = new();
    private Dictionary<TcpNetworkConnection, int> _connectionToId = new();

    private Dictionary<IPAddress, ClientRateInfo> _rateLimits = new();
    private HashSet<IPAddress> _bannedIPs = new();
    private const int MaxRequestsPerSecond = 50;
    private const int BanThreshold = 5;
    private const int BanDurationSeconds = 300;

    // Selected user for console "selected user" commands
    private ClientInfo _selectedUser;

    public object SyncRoot => _sync;
    public OSCDispatcher Dispatcher => _dispatcher;
    public IReadOnlyDictionary<string, RoomData> Rooms => _rooms;
    public IReadOnlyDictionary<int, ClientInfo> Clients => _clients;

    public TcpServer()
    {
        _dispatcher = new OSCDispatcher();
    }

    public void Start(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Console.WriteLine($"TCP OSC Server running on port {port}");
    }

    public void Stop()
    {
        _isShuttingDown = true;
        lock (_sync)
        {
            foreach (var conn in _connections)
                conn.Close();
            _listener?.Stop();
        }
    }

    public void Update()
    {
        AcceptNewConnections();
        UpdateConnections();
        CleanupConnections();
    }

    private void AcceptNewConnections()
    {
        if (_listener.Pending())
        {
            var tcpClient = _listener.AcceptTcpClient();
            var conn = new TcpNetworkConnection(tcpClient);
            if (conn.Status == ConnectionStatus.Connected)
            {
                lock (_sync) _connections.Add(conn);
                Console.WriteLine($"[NET] New connection from {conn.Remote}");
            }
            else
            {
                conn.Close();
                Console.WriteLine($"[NET] Rejected connection (not connected)");
            }
        }
    }

    private void UpdateConnections()
    {
        List<TcpNetworkConnection> snapshot;
        lock (_sync) snapshot = _connections.ToList();

        foreach (var conn in snapshot)
        {
            try
            {
                while (conn.Available() > 0)
                {
                    var packet = conn.GetPacket();
                    if (packet != null)
                    {
                        if (ShouldBlock(conn.Remote)) continue;
                        _dispatcher.HandlePacket(packet, conn.Remote);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Reading from {conn.Remote}: {ex.Message}");
            }
        }
    }

    private void CleanupConnections()
    {
        List<TcpNetworkConnection> dead;
        lock (_sync)
        {
            dead = _connections.Where(c => c.Status != ConnectionStatus.Connected).ToList();
            foreach (var conn in dead)
            {
                _connections.Remove(conn);
                if (_connectionToId.TryGetValue(conn, out var id) && _clients.TryGetValue(id, out var client))
                    RemoveClient(client);
                conn.Close();
            }
        }
        foreach (var conn in dead)
            Console.WriteLine($"[NET] Removed dead connection {conn.Remote}");
    }

    private bool ShouldBlock(IPEndPoint endpoint)
    {
        var ip = endpoint.Address;
        lock (_sync)
        {
            if (_bannedIPs.Contains(ip)) return true;

            if (!_rateLimits.TryGetValue(ip, out var info))
            {
                info = new ClientRateInfo();
                _rateLimits[ip] = info;
            }

            var now = DateTime.UtcNow;
            if ((now - info.LastRequestTime).TotalSeconds >= 1)
            {
                info.RequestCountInCurrentSecond = 0;
                info.LastRequestTime = now;
            }

            info.RequestCountInCurrentSecond++;
            if (info.RequestCountInCurrentSecond > MaxRequestsPerSecond)
            {
                info.BanCount++;
                if (info.BanCount >= BanThreshold)
                {
                    _bannedIPs.Add(ip);
                    Console.WriteLine($"Banned IP {ip} due to rate limit abuse.");
                    new Timer(_ => { lock (_sync) _bannedIPs.Remove(ip); Console.WriteLine($"Unbanned IP {ip}"); },
                        null, BanDurationSeconds * 1000, Timeout.Infinite);
                }
                return true;
            }
            info.BanCount = 0;
            return false;
        }
    }

    // --- Public methods for console commands ---

    public string GetAllPlayersInfo()
    {
        lock (_sync)
        {
            if (_clients.Count == 0) return "No connected players.";
            var lines = new List<string> { $"=== Players ({_clients.Count}) ===" };
            foreach (var client in _clients.Values)
                lines.Add($"- ID {client.Id}: {client.Name} | Room: {client.CurrentRoom ?? "lobby"} | Endpoint: {client.Connection.Remote}");
            return string.Join("\n", lines);
        }
    }

    public ClientInfo FindPlayerById(int id)
    {
        lock (_sync)
        {
            return _clients.TryGetValue(id, out var c) ? c : null;
        }
    }

    public ClientInfo FindPlayerByName(string name)
    {
        lock (_sync)
        {
            return _clients.Values.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool KickUser(int id)
    {
        lock (_sync)
        {
            if (_clients.TryGetValue(id, out var client))
            {
                RemoveClient(client);
                Console.WriteLine($"Kicked user {client.Name} (ID {id})");
                return true;
            }
            return false;
        }
    }

    public bool SelectUser(int id)
    {
        lock (_sync)
        {
            _selectedUser = FindPlayerById(id);
            if (_selectedUser != null)
            {
                Console.WriteLine($"Selected user: {_selectedUser.Name} (ID {_selectedUser.Id})");
                return true;
            }
            return false;
        }
    }

    public ClientInfo GetSelectedUser() => _selectedUser;

    public bool SendPrivateMessage(int userId, string message)
    {
        lock (_sync)
        {
            var client = FindPlayerById(userId);
            if (client == null) return false;
            var msg = new OSCMessageOut("/server_message").AddString($"[Console PM]: {message}");
            Send(client.Connection, msg);
            return true;
        }
    }

    public bool ChangeUserName(int userId, string newName)
    {
        lock (_sync)
        {
            var client = FindPlayerById(userId);
            if (client == null) return false;
            if (string.IsNullOrEmpty(newName) || newName.Length > 12) return false;
            client.Name = newName;
            Console.WriteLine($"User {userId} renamed to {newName}");
            return true;
        }
    }

    public bool CreateFakeUser(string name)
    {
        // Create a dummy connection with loopback IP and random port
        lock (_sync)
        {
            var fakeEndpoint = new IPEndPoint(IPAddress.Loopback, new Random().Next(10000, 60000));
            var fakeConn = new TcpNetworkConnection(new TcpClient()); // minimal, just for storage
            int id = _nextId++;
            var client = new ClientInfo
            {
                Id = id,
                Name = name,
                Connection = fakeConn,
                CurrentRoom = null
            };
            _clients[id] = client;
            _connectionToId[fakeConn] = id;
            Console.WriteLine($"Created fake user {name} with ID {id}");
            return true;
        }
    }

    public string GetAllRoomsInfo()
    {
        lock (_sync)
        {
            if (_rooms.Count == 0) return "No active rooms.";
            var lines = new List<string> { $"=== Rooms ({_rooms.Count}) ===" };
            foreach (var room in _rooms.Values)
                lines.Add($"- {room.roomName} | Host: {room.host} | Players: {room.Participants.Count}/4 | GameStarted: {room.GameStarted}");
            return string.Join("\n", lines);
        }
    }

    public bool FindRoom(string name)
    {
        lock (_sync)
        {
            return _rooms.ContainsKey(name);
        }
    }

    public bool CreateRoomViaConsole(string roomName, int pointGoal, int hostId)
    {
        lock (_sync)
        {
            if (_rooms.ContainsKey(roomName)) return false;
            var host = FindPlayerById(hostId);
            if (host == null) return false;
            var room = new RoomData(roomName.GetHashCode(), roomName, host.Name, pointGoal);
            room.Participants.Add(new Participant(host.Id, host.Name, 0));
            _rooms[roomName] = room;
            host.CurrentRoom = roomName;
            Console.WriteLine($"Console created room '{roomName}' with goal {pointGoal}, host {host.Name}");
            return true;
        }
    }

    public bool CloseRoom(string roomName)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomName, out var room)) return false;
            foreach (var p in room.Participants.ToList())
            {
                if (_clients.TryGetValue(p.id, out var client))
                    client.CurrentRoom = null;
            }
            _rooms.Remove(roomName);
            Console.WriteLine($"Room '{roomName}' closed.");
            return true;
        }
    }

    public bool StartRoom(string roomName)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomName, out var room)) return false;
            if (room.GameStarted) return false;
            room.GameStarted = true;
            Console.WriteLine($"Room '{roomName}' started.");
            return true;
        }
    }

    public void BroadcastToRoom(string roomName, string message)
    {
        lock (_sync)
        {
            if (_rooms.TryGetValue(roomName, out var room))
            {
                var msg = new OSCMessageOut("/server_message").AddString($"Broadcast to room '{roomName}': {message}");
                BroadcastToRoom(room, msg);
            }
        }
    }

    public TcpNetworkConnection GetConnectionByEndpoint(IPEndPoint endpoint)
    {
        lock (_sync)
        {
            return _connections.FirstOrDefault(c => c.Remote != null && c.Remote.Equals(endpoint));
        }
    }

    public ClientInfo GetClientByConnection(TcpNetworkConnection conn)
    {
        lock (_sync)
        {
            return _connectionToId.TryGetValue(conn, out int id) && _clients.TryGetValue(id, out var client) ? client : null;
        }
    }

    public ClientInfo GetClientByEndpoint(IPEndPoint endpoint) => GetClientByConnection(GetConnectionByEndpoint(endpoint));

    public bool TryGetRoom(string name, out RoomData room)
    {
        lock (_sync)
            return _rooms.TryGetValue(name, out room);
    }
    public void AddRoom(RoomData room)
    {
        lock (_sync)
            _rooms[room.roomName] = room;
    }
    public bool RemoveRoom(string name)
    {
        lock (_sync)
            return _rooms.Remove(name);
    }
    public int GetNextClientId()
    {
        lock (_sync)
            return _nextId++;
    }
    public void RegisterClient(int id, ClientInfo client)
    {
        lock (_sync)
        {
            _clients[id] = client; _connectionToId[client.Connection] = id;
        }
    }
    public void UpdateClientRoom(ClientInfo client, string roomName)
    {
        lock (_sync) client.CurrentRoom = roomName;
    }
    public void RemoveClient(ClientInfo client)
    {
        lock (_sync)
        {
            if (client == null) return;
            if (!string.IsNullOrEmpty(client.CurrentRoom) && _rooms.TryGetValue(client.CurrentRoom, out var room))
            {
                var participant = room.Participants.FirstOrDefault(p => p.id == client.Id);
                if (participant != null) room.Participants.Remove(participant);
                if (room.Participants.Count == 0)
                    _rooms.Remove(room.roomName);
                else if (room.host == client.Name && room.Participants.Count > 0)
                {
                    var newHost = room.Participants.First();
                    room.host = newHost.clientName;
                }
            }
            _connectionToId.Remove(client.Connection);
            _clients.Remove(client.Id);
            Console.WriteLine($"[DISCONNECT] {client.Name} (ID {client.Id}) disconnected");
        }
    }
    public void Send(TcpNetworkConnection conn, OSCMessageOut msg)
    {
        try { conn.Send(msg.GetBytes()); }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { Console.WriteLine($"[SEND ERROR] {ex.Message}"); }
    }
    public void SendError(TcpNetworkConnection conn, string message)
    {
        var errorMsg = new OSCMessageOut("/error").AddString(message);
        Send(conn, errorMsg);
        Console.WriteLine($"[ERROR] Sent to {conn.Remote}: {message}");
    }
    public void BroadcastToRoom(RoomData room, OSCMessageOut msg)
    {
        lock (_sync)
        {
            foreach (var client in _clients.Values)
                if (client.CurrentRoom == room.roomName)
                    Send(client.Connection, msg);
        }
    }
    public void BroadcastToAll(string textMessage)
    {
        var msg = new OSCMessageOut("/server_message").AddString(textMessage);
        lock (_sync)
        {
            foreach (var client in _clients.Values)
                Send(client.Connection, msg);
        }
        Console.WriteLine($"Broadcast to {_clients.Count} clients: \"{textMessage}\"");
    }
}