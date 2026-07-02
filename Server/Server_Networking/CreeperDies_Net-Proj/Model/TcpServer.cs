using NetworkConnections;
using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace CreeperDice_Net_Proj.Model
{
    public class TcpServer
    {
        #region Sync

        private readonly object _sync = new object();

        public object SyncRoot => _sync;

        #endregion

        #region Networking Fields

        private TcpListener _listener;
        private readonly List<TcpNetworkConnection> _connections = new();
        private readonly OSCDispatcher _dispatcher;

        private bool _isShuttingDown;

        public OSCDispatcher Dispatcher => _dispatcher;

        #endregion

        #region Client State

        private int _nextId = 1;

        private readonly Dictionary<int, ClientInfo> _clients = new();
        private readonly Dictionary<TcpNetworkConnection, int> _connectionToId = new();

        private ClientInfo _selectedUser;

        private const int MaxUsernameLength = 12;

        public IReadOnlyDictionary<int, ClientInfo> Clients => _clients;

        #endregion

        #region Room State

        private readonly Dictionary<string, RoomData> _rooms = new();

        public IReadOnlyDictionary<string, RoomData> Rooms => _rooms;

        #endregion

        #region Security State

        private readonly Dictionary<IPAddress, ClientRateInfo> _rateLimits = new();
        private readonly HashSet<IPAddress> _bannedIPs = new();

        private const int MaxRequestsPerSecond = 50;
        private const int BanThreshold = 5;
        private const int BanDurationSeconds = 300;

        public readonly Dictionary<ClientInfo, int> _maliciousStrikes = new();

        #endregion

        #region Server States

        public readonly LobbyState lobby;
        public readonly GameState game;

        #endregion

        #region Constructor

        public TcpServer()
        {
            _dispatcher = new OSCDispatcher();

            game = new GameState(this);// possible null ref
            lobby = new LobbyState(this);
        }

        #endregion

        #region Server Lifecycle

        public void Start(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            RegisterHandlers();

            Console.WriteLine($"TCP OSC Server running on port {port}");
        }

        public void Stop()
        {
            lock (_sync)
            {
                _isShuttingDown = true;

                foreach (TcpNetworkConnection connection in _connections)
                    connection.Close();

                _listener?.Stop();
            }
        }

        public void Update()
        {
            if (_isShuttingDown)
                return;

            try
            {
                AcceptNewConnections();
                UpdateConnections();
                CleanupConnections();

                game.Update();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER UPDATE ERROR] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        #endregion

        #region Message Registration

        private void RegisterHandlers()
        {
            _dispatcher.AddListener(Msg.C_REGISTER, OnRegister, OSCUtil.STRING);
            _dispatcher.AddListener(Msg.C_DISCONNECT, OnDisconnect);
            _dispatcher.AddListener(Msg.C_PING, OnPing);

        }

        #endregion

        #region Received Messages

        private void OnRegister(OSCMessageIn msg, IPEndPoint sender)
        {
            TcpNetworkConnection connection = GetConnectionByEndpoint(sender);

            if (connection == null)
                return;

            string username = ReadCappedString(msg, MaxUsernameLength, "username");

            if (username == null)
            {
                SendError(connection, $"Username too long. Max {MaxUsernameLength} characters.");
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                SendError(connection, "Username cannot be empty.");
                return;
            }

            int id = GetNextClientId();

            var client = new ClientInfo
            {
                Id = id,
                Name = username,
                Connection = connection,
                CurrentRoom = null
            };

            RegisterClient(id, client);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Registered {username} with ID {id}");

            var reply = new OSCMessageOut(Msg.S_REGISTERED)
                .AddInt(id)
                .AddString(username);

            SendToConnection(connection, reply);
        }

        private void OnPing(OSCMessageIn msg, IPEndPoint sender)
        {
            var conn = GetConnectionByEndpoint(sender);

            if (conn == null)
                return;

            var pong = new OSCMessageOut(Msg.S_PONG);
            Send(conn, pong);
        }
        private void OnDisconnect(OSCMessageIn msg, IPEndPoint sender)
        {
            ClientInfo client = GetClientByEndpoint(sender);

            if (client != null)
                RemoveClient(client);
        }

        #endregion

        #region Connection Updating

        private void AcceptNewConnections()
        {
            if (_listener == null)
                return;

            while (_listener.Pending())
            {
                try
                {
                    TcpClient tcpClient = _listener.AcceptTcpClient();
                    var connection = new TcpNetworkConnection(tcpClient);

                    if (connection.Status == ConnectionStatus.Connected)
                    {
                        lock (_sync)
                            _connections.Add(connection);

                        Console.WriteLine($"[NET] New connection from {connection.Remote}");
                    }
                    else
                    {
                        connection.Close();
                        Console.WriteLine("[NET] Rejected connection.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ACCEPT ERROR] {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }

        private void UpdateConnections()
        {
            List<TcpNetworkConnection> snapshot;

            lock (_sync)
                snapshot = _connections.ToList();

            foreach (TcpNetworkConnection connection in snapshot)
                ProcessConnectionPackets(connection);
        }

        private void ProcessConnectionPackets(TcpNetworkConnection connection)
        {
            if (connection == null)
                return;

            if (connection.Status != ConnectionStatus.Connected)
                return;

            try
            {
                while (connection.Available() > 0)
                {
                    byte[] packet = connection.GetPacket();

                    if (packet == null)
                        continue;

                    if (ShouldBlock(connection.Remote))
                        continue;

                    _dispatcher.HandlePacket(packet, connection.Remote);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Reading from {connection.Remote}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void CleanupConnections()
        {
            List<TcpNetworkConnection> deadConnections;

            lock (_sync)
            {
                deadConnections = _connections
                    .Where(connection => connection.Status != ConnectionStatus.Connected)
                    .ToList();

                foreach (TcpNetworkConnection connection in deadConnections)
                    _connections.Remove(connection);
            }

            foreach (TcpNetworkConnection connection in deadConnections)
                CleanupConnection(connection);
        }

        private void CleanupConnection(TcpNetworkConnection connection)
        {
            ClientInfo client = GetClientByConnection(connection);

            if (client != null)
                RemoveClient(client);

            connection.Close();

            Console.WriteLine($"[NET] Removed dead connection {connection.Remote}");
        }

        #endregion

        #region Client Registry

        public int GetNextClientId()
        {
            lock (_sync)
                return _nextId++;
        }

        public void RegisterClient(int id, ClientInfo client)
        {
            lock (_sync)
            {
                _clients[id] = client;
                _connectionToId[client.Connection] = id;
            }
        }

        public ClientInfo FindPlayerById(int id)
        {
            lock (_sync)
            {
                return _clients.TryGetValue(id, out ClientInfo client) ? client : null;
            }
        }

        public ClientInfo FindPlayerByName(string name)
        {
            lock (_sync)
            {
                return _clients.Values.FirstOrDefault(client =>
                    client.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void UpdateClientRoom(ClientInfo client, string roomName)
        {
            lock (_sync)
            {
                if (client != null)
                    client.CurrentRoom = roomName;
            }
        }

        public void RemoveClient(ClientInfo client)
        {
            lock (_sync)
            {
                if (client == null)
                    return;

                RemoveClientFromRoom(client);

                _connectionToId.Remove(client.Connection);
                _clients.Remove(client.Id);

                Console.WriteLine($"[DISCONNECT] {client.Name} (ID {client.Id}) disconnected");
            }
        }

        private void RemoveClientFromRoom(ClientInfo client)
        {
            if (string.IsNullOrEmpty(client.CurrentRoom))
                return;

            if (!_rooms.TryGetValue(client.CurrentRoom, out RoomData room))
                return;

            bool wasHost = room.host == client.Name;

            Participant participant = room.Participants.FirstOrDefault(p => p.id == client.Id);

            if (participant != null)
                room.Participants.Remove(participant);

            client.CurrentRoom = null;

            if (room.Participants.Count == 0)
            {
                _rooms.Remove(room.roomName);
                return;
            }

            if (wasHost)
            {
                if (room.GameStarted)
                {
                    CloseRoomBecauseHostDisconnected(room);
                    return;
                }

                Participant newHost = room.Participants.First();
                room.host = newHost.clientName;

                SendRoomUpdate(room);
            }
            else
            {
                SendRoomUpdate(room);
            }
        }
        private void CloseRoomBecauseHostDisconnected(RoomData room)
        {
            if (room == null)
                return;

            string roomName = room.roomName;

            var returnMsg = new OSCMessageOut(Msg.S_RETURN_TO_LOBBY)
                .AddString("Host disconnected. Room closed.");

            foreach (Participant participant in room.Participants.ToList())
            {
                if (_clients.TryGetValue(participant.id, out ClientInfo client))
                {
                    client.CurrentRoom = null;
                    SendToClient(client, returnMsg);
                }
            }

            var closedMsg = new OSCMessageOut(Msg.S_CLOSED_ROOM)
                .AddString(roomName);

            SendToAll(closedMsg);

            _rooms.Remove(roomName);

            Console.WriteLine($"[ROOM] Host disconnected. Closed room '{roomName}'.");
        }

        private void SendRoomUpdate(RoomData room)
        {
            if (room == null)
                return;

            var updateMsg = new OSCMessageOut(Msg.S_ROOM_UPDATE)
                .AddString(room.roomName)
                .AddInt(room.Participants.Count)
                .AddString(room.host)
                .AddInt(room.pointGoal)
                .AddBool(room.GameStarted);

            SendToAll(updateMsg);
        }
        public ClientInfo GetClientByConnection(TcpNetworkConnection connection)
        {
            lock (_sync)
            {
                if (connection == null)
                    return null;

                return _connectionToId.TryGetValue(connection, out int id) &&
                       _clients.TryGetValue(id, out ClientInfo client)
                    ? client
                    : null;
            }
        }

        public ClientInfo GetClientByEndpoint(IPEndPoint endpoint)
        {
            TcpNetworkConnection connection = GetConnectionByEndpoint(endpoint);
            return GetClientByConnection(connection);
        }

        public TcpNetworkConnection GetConnectionByEndpoint(IPEndPoint endpoint)
        {
            lock (_sync)
            {
                return _connections.FirstOrDefault(connection =>
                    connection.Remote != null &&
                    connection.Remote.Equals(endpoint));
            }
        }

        #endregion

        #region Room Registry

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

        public bool FindRoom(string name)
        {
            lock (_sync)
                return _rooms.ContainsKey(name);
        }

        #endregion

        #region Sending Messages

        public void SendToConnection(TcpNetworkConnection connection, OSCMessageOut msg)
        {
            if (connection == null)
                return;

            try
            {
                connection.Send(msg.GetBytes());
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine($"[SEND ERROR] Connection already closed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEND ERROR] {ex.Message}");
            }
        }

        public void SendToClient(ClientInfo client, OSCMessageOut msg)
        {
            if (client == null)
                return;

            SendToConnection(client.Connection, msg);
        }

        public void SendError(TcpNetworkConnection connection, string message)
        {
            var errorMsg = new OSCMessageOut(Msg.S_ERROR)
                .AddString(message);

            SendToConnection(connection, errorMsg);

            Console.WriteLine($"[ERROR] Sent to {connection?.Remote}: {message}");
        }

        public void SendServerMessageToClient(ClientInfo client, string message)
        {
            var msg = new OSCMessageOut(Msg.S_SERVER_MESSAGE)
                .AddString(message);

            SendToClient(client, msg);
        }

        public void SendServerMessageToRoom(string roomName, string message)
        {
            var msg = new OSCMessageOut(Msg.S_SERVER_MESSAGE)
                .AddString(message);

            SendToRoom(roomName, msg);
        }

        public void SendServerMessageToAll(string message)
        {
            var msg = new OSCMessageOut(Msg.S_SERVER_MESSAGE)
                .AddString(message);

            SendToAll(msg);
        }

        public void SendToRoom(string roomName, OSCMessageOut msg)
        {
            if (string.IsNullOrEmpty(roomName) || msg == null)
                return;

            List<ClientInfo> targets;

            lock (_sync)
            {
                targets = _clients.Values
                    .Where(client => client.CurrentRoom == roomName)
                    .ToList();
            }

            foreach (ClientInfo client in targets)
                SendToConnection(client.Connection, msg);
        }

        public void SendToRoom(RoomData room, OSCMessageOut msg)
        {
            if (room == null)
                return;

            SendToRoom(room.roomName, msg);
        }

        public void SendToAll(OSCMessageOut msg)
        {
            if (msg == null)
                return;
            lock (_sync)
            {
                List<ClientInfo> targets;

           
                targets = _clients.Values.ToList();

            foreach (ClientInfo client in targets)
                SendToConnection(client.Connection, msg);
            }
        }

        #endregion

        #region Compatibility Wrappers

        public void Send(TcpNetworkConnection connection, OSCMessageOut msg)
        {
            SendToConnection(connection, msg);
        }

        public void BroadcastToRoom(string roomName, string message)
        {
            SendServerMessageToRoom(roomName, message);
        }

        public void BroadcastToRoom(string roomName, OSCMessageOut msg)
        {
            SendToRoom(roomName, msg);
        }

        public void BroadcastToRoom(RoomData room, OSCMessageOut msg)
        {
            SendToRoom(room, msg);
        }

        public void BroadcastToAll(string message)
        {
            SendServerMessageToAll(message);
        }

        public void BroadcastToAll(OSCMessageOut msg)
        {
            SendToAll(msg);
        }

        #endregion

        #region Security

        public string ReadCappedString(OSCMessageIn msg, int max, string field)
        {
            string value = msg.ReadString();

            if (value == null || value.Length > max)
            {
                Console.WriteLine($"Invalid {field} length. Max {max}. Got {value?.Length ?? 0}");
                return null;
            }

            return value;
        }

        public void AddMaliciousStrike(ClientInfo client)
        {
            if (client == null)
                return;

            if (!_maliciousStrikes.ContainsKey(client))
                _maliciousStrikes[client] = 0;

            _maliciousStrikes[client]++;

            if (_maliciousStrikes[client] < 3)
                return;

            Console.WriteLine($"[SECURITY] {client.Name} kicked for 3 malicious strikes.");
            KickUser(client.Id);
        }

        private bool ShouldBlock(IPEndPoint endpoint)
        {
            if (endpoint == null)
                return false;

            IPAddress ip = endpoint.Address;

            lock (_sync)
            {
                if (_bannedIPs.Contains(ip))
                    return true;

                ClientRateInfo info = GetRateInfo(ip);

                DateTime now = DateTime.UtcNow;

                if ((now - info.LastRequestTime).TotalSeconds >= 1)
                {
                    info.RequestCountInCurrentSecond = 0;
                    info.LastRequestTime = now;
                }

                info.RequestCountInCurrentSecond++;

                if (info.RequestCountInCurrentSecond <= MaxRequestsPerSecond)
                {
                    info.BanCount = 0;
                    return false;
                }

                info.BanCount++;

                if (info.BanCount >= BanThreshold)
                    BanIp(ip);

                return true;
            }
        }

        private ClientRateInfo GetRateInfo(IPAddress ip)
        {
            if (!_rateLimits.TryGetValue(ip, out ClientRateInfo info))
            {
                info = new ClientRateInfo
                {
                    LastRequestTime = DateTime.UtcNow
                };

                _rateLimits[ip] = info;
            }

            return info;
        }

        private void BanIp(IPAddress ip)
        {
            _bannedIPs.Add(ip);

            Console.WriteLine($"Banned IP {ip} due to rate limit abuse.");

            new Timer(
                _ =>
                {
                    lock (_sync)
                        _bannedIPs.Remove(ip);

                    Console.WriteLine($"Unbanned IP {ip}");
                },
                null,
                BanDurationSeconds * 1000,
                Timeout.Infinite
            );
        }

        #endregion

        #region Console Command Support

        public string GetAllPlayersInfo()
        {
            lock (_sync)
            {
                if (_clients.Count == 0)
                    return "No connected players.";

                List<string> lines = new List<string>
                {
                    $"=== Players ({_clients.Count}) ==="
                };

                foreach (ClientInfo client in _clients.Values)
                {
                    lines.Add(
                        $"- ID {client.Id}: {client.Name} | Room: {client.CurrentRoom ?? "lobby"} | Endpoint: {client.Connection.Remote}"
                    );
                }

                return string.Join("\n", lines);
            }
        }

        public string GetAllRoomsInfo()
        {
            lock (_sync)
            {
                if (_rooms.Count == 0)
                    return "No active rooms.";

                List<string> lines = new List<string>
                {
                    $"=== Rooms ({_rooms.Count}) ==="
                };

                foreach (RoomData room in _rooms.Values)
                {
                    lines.Add(
                        $"- {room.roomName} | Host: {room.host} | Players: {room.Participants.Count}/4 | GameStarted: {room.GameStarted}"
                    );
                }

                return string.Join("\n", lines);
            }
        }

        public bool KickUser(int id)
        {
            lock (_sync)
            {
                if (!_clients.TryGetValue(id, out ClientInfo client))
                    return false;

                RemoveClient(client);

                Console.WriteLine($"Kicked user {client.Name} (ID {id})");

                return true;
            }
        }

        public bool SelectUser(int id)
        {
            lock (_sync)
            {
                _selectedUser = FindPlayerById(id);

                if (_selectedUser == null)
                    return false;

                Console.WriteLine($"Selected user: {_selectedUser.Name} (ID {_selectedUser.Id})");

                return true;
            }
        }

        public ClientInfo GetSelectedUser()
        {
            return _selectedUser;
        }

        public bool SendPrivateMessage(int userId, string message)
        {
            ClientInfo client = FindPlayerById(userId);

            if (client == null)
                return false;

            SendServerMessageToClient(client, $"[Console PM]: {message}");

            return true;
        }

        public bool ChangeUserName(int userId, string newName)
        {
            lock (_sync)
            {
                ClientInfo client = FindPlayerById(userId);

                if (client == null)
                    return false;

                if (string.IsNullOrEmpty(newName) || newName.Length > MaxUsernameLength)
                    return false;

                client.Name = newName;

                Console.WriteLine($"User {userId} renamed to {newName}");

                return true;
            }
        }

        public bool CreateFakeUser(string name)
        {
            lock (_sync)
            {
                var fakeConnection = new TcpNetworkConnection(new TcpClient());

                int id = _nextId++;

                var client = new ClientInfo
                {
                    Id = id,
                    Name = name,
                    Connection = fakeConnection,
                    CurrentRoom = null
                };

                _clients[id] = client;
                _connectionToId[fakeConnection] = id;

                Console.WriteLine($"Created fake user {name} with ID {id}");

                return true;
            }
        }

        public bool CreateRoomViaConsole(string roomName, int pointGoal, int hostId)
        {
            lock (_sync)
            {
                if (_rooms.ContainsKey(roomName))
                    return false;

                ClientInfo host = FindPlayerById(hostId);

                if (host == null)
                    return false;

                RoomData room = new RoomData(roomName.GetHashCode(), roomName, host.Name, pointGoal);

                room.Participants.Add(new Participant(host.Id, host.Name, 0));

                _rooms[roomName] = room;
                host.CurrentRoom = roomName;

                Console.WriteLine($"Console created room '{roomName}' with goal {pointGoal}, host {host.Name}");

                var joinedMsg = new OSCMessageOut(Msg.S_JOINED)
                    .AddString(room.roomName)
                    .AddInt(room.Participants.Count)
                    .AddString(room.host)
                    .AddInt(room.pointGoal)
                    .AddBool(room.GameStarted);

                SendToClient(host, joinedMsg);

                var updateMsg = new OSCMessageOut(Msg.S_ROOM_UPDATE)
                    .AddString(room.roomName)
                    .AddInt(room.Participants.Count)
                    .AddString(room.host)
                    .AddInt(room.pointGoal)
                    .AddBool(room.GameStarted);

                SendToAll(updateMsg);

                return true;
            }
        }

        public bool CloseRoom(string roomName)
        {
            lock (_sync)
            {
                if (!_rooms.TryGetValue(roomName, out RoomData room))
                    return false;

                foreach (Participant participant in room.Participants.ToList())
                {
                    if (_clients.TryGetValue(participant.id, out ClientInfo client))
                        client.CurrentRoom = null;
                }

                var closeMsg = new OSCMessageOut(Msg.S_CLOSED_ROOM)
                    .AddString(roomName);

                SendToAll(closeMsg);

                _rooms.Remove(roomName);

                Console.WriteLine($"Room '{roomName}' closed via console.");

                return true;
            }
        }

        public bool StartRoom(string roomName)
        {
            lock (_sync)
            {
                if (!_rooms.TryGetValue(roomName, out RoomData room))
                    return false;

                if (room.GameStarted)
                    return false;

                room.GameStarted = true;

                var gameStartedMsg = new OSCMessageOut(Msg.S_GAME_STARTED);
                SendToRoom(room, gameStartedMsg);

                var updateMsg = new OSCMessageOut(Msg.S_ROOM_UPDATE)
                    .AddString(roomName)
                    .AddInt(room.Participants.Count)
                    .AddString(room.host)
                    .AddInt(room.pointGoal)
                    .AddBool(true);

                SendToAll(updateMsg);

                game.StartGameForRoom(room);

                Console.WriteLine($"Room '{roomName}' started via console.");

                return true;
            }
        }

        #endregion
    }
}