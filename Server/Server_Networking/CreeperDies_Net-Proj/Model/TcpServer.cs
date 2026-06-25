using CreeperDice_Net_Proj.Model;
using NetworkConnections;
using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
namespace CreeperDice_Net_Proj
{
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

        private const int MaxUsernameLength = 12;

        public object SyncRoot => _sync;
        public OSCDispatcher Dispatcher => _dispatcher;
        public IReadOnlyDictionary<string, RoomData> Rooms => _rooms;
        public IReadOnlyDictionary<int, ClientInfo> Clients => _clients;
        public readonly LobbyState lobby;
        public readonly GameState game;


        public readonly Dictionary<ClientInfo, int> _maliciousStrikes = new();

        public TcpServer()
        {
            _dispatcher = new OSCDispatcher();

            lobby = new LobbyState(this);
            game = new GameState(this);
        }

        public void Start(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _dispatcher.AddListener(Msg.C_REGISTER, OnRegister, OSCUtil.STRING);
            _dispatcher.AddListener(Msg.C_DISCONNECT, OnDisconnect);


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
        private void OnRegister(OSCMessageIn msg, IPEndPoint sender)
        {
            var conn = GetConnectionByEndpoint(sender);
            if (conn == null) return;

            string username = ReadCappedString(msg, MaxUsernameLength, "username");
            if (username == null)
            {
                SendError(conn, $"Username too long (max {MaxUsernameLength})");
                return;
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                SendError(conn, "Username cannot be empty");
                return;
            }

            int id = GetNextClientId();
            var client = new ClientInfo
            {
                Id = id,
                Name = username,
                Connection = conn,
                CurrentRoom = null
            };
            RegisterClient(id, client);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Registered {username} with ID {id}");

            var reply = new OSCMessageOut(Msg.S_REGISTERED).AddInt(id).AddString(username);
            Send(conn, reply);
        }
        private void OnDisconnect(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = GetClientByEndpoint(sender);
            if (client != null) RemoveClient(client);
        }
        public string ReadCappedString(OSCMessageIn msg, int max, string field)
        {
            string val = msg.ReadString();
            if (val == null || val.Length > max)
            {
                Console.WriteLine($"Invalid {field} length (max {max}): {val?.Length ?? 0}");
                return null;
            }
            return val;
        }

        public void AddMaliciousStrike(ClientInfo client)
        {
            if (!_maliciousStrikes.ContainsKey(client))
                _maliciousStrikes[client] = 0;
            _maliciousStrikes[client]++;
            if (_maliciousStrikes[client] >= 3)
            {
                Console.WriteLine($"[SECURITY] {client.Name} (ID {client.Id}) kicked for 3 malicious strikes");
                KickUser(client.Id);
            }
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

        #region --- Public methods for console commands ---

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
                var msg = new OSCMessageOut(Msg.S_SERVER_MESSAGE).AddString($"[Console PM]: {message}");
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
                var fakeEndpoint = new IPEndPoint(IPAddress.Loopback, new Random().Next(50000, 60000));
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

                // Create room
                var room = new RoomData(roomName.GetHashCode(), roomName, host.Name, pointGoal);
                room.Participants.Add(new Participant(host.Id, host.Name, 0));
                _rooms[roomName] = room;
                host.CurrentRoom = roomName;

                Console.WriteLine($"Console created room '{roomName}' with goal {pointGoal}, host {host.Name}");

                // Broadcast to all clients that a new room was created
                var confirmMsg = new OSCMessageOut(Msg.S_CREATED_ROOM)
                    .AddString(roomName)
                    .AddInt(room.Participants.Count)    // 1
                    .AddString(host.Name)
                    .AddInt(pointGoal)
                    .AddBool(false);
                BroadcastToAll(confirmMsg);

                // Optionally, send a private S_JOINED to the host so the client shows the waiting/host view
                var joinedMsg = new OSCMessageOut(Msg.S_JOINED)
                    .AddString(roomName)
                    .AddInt(room.Participants.Count)
                    .AddString(host.Name)
                    .AddInt(pointGoal)
                    .AddBool(false);
                Send(host.Connection, joinedMsg);

                return true;
            }
        }

        public bool CloseRoom(string roomName)
        {
            lock (_sync)
            {
                if (!_rooms.TryGetValue(roomName, out var room)) return false;

                // Clear CurrentRoom from all participants
                foreach (var p in room.Participants.ToList())
                {
                    if (_clients.TryGetValue(p.id, out var client))
                        client.CurrentRoom = null;
                }

                // Broadcast room closed to all clients
                var closeMsg = new OSCMessageOut(Msg.S_CLOSED_ROOM).AddString(roomName);
                BroadcastToAll(closeMsg);

                _rooms.Remove(roomName);
                Console.WriteLine($"Room '{roomName}' closed via console.");
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

                // 1. Tell all clients in the room to load the game scene
                var gameStartedMsg = new OSCMessageOut(Msg.S_GAME_STARTED);
                BroadcastToRoom(room, gameStartedMsg);

                // 2. Broadcast room update to ALL clients (so room disappears from lobby list)
                var updateMsg = new OSCMessageOut(Msg.S_ROOM_UPDATE)
                    .AddString(roomName)
                    .AddInt(room.Participants.Count)
                    .AddString(room.host)
                    .AddInt(room.pointGoal)
                    .AddBool(true);  // gameStarted = true
                BroadcastToAll(updateMsg);

                // 3. Initialize game state (first turn, dice, etc.)
                game.StartGameForRoom(room);

                Console.WriteLine($"Room '{roomName}' started via console.");
                return true;
            }
        }

        public void BroadcastToRoom(string roomName, string message)
        {
            lock (_sync)
            {
                if (_rooms.TryGetValue(roomName, out var room))
                {
                    var msg = new OSCMessageOut(Msg.S_SERVER_MESSAGE).AddString($"Broadcast to room '{roomName}': {message}");
                    BroadcastToRoom(room, msg);
                }
            }
        }
        public void BroadcastToRoom(string roomName, OSCMessageOut msg)
        {
            lock (_sync)
            {
                if (_rooms.TryGetValue(roomName, out var room))
                {
                    Console.WriteLine($"{roomName} {msg}");
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
                _clients[id] = client; 
                _connectionToId[client.Connection] = id;
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
            var errorMsg = new OSCMessageOut(Msg.S_ERROR).AddString(message); 
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
            var msg = new OSCMessageOut(Msg.S_SERVER_MESSAGE).AddString(textMessage);
            lock (_sync)
            {
                foreach (var client in _clients.Values)
                    Send(client.Connection, msg);
            }
            Console.WriteLine($"Broadcast to {_clients.Count} clients: \"{textMessage}\"");
        }
        public void BroadcastToAll(OSCMessageOut msg)
        {
            lock (_sync)
            {
                foreach (var client in _clients.Values)
                    Send(client.Connection, msg);
            }
            Console.WriteLine($"Broadcast to {_clients.Count} clients: {msg.header ?? "unknown message"}");
        }

        #endregion
    }
}

/*
Q & A session – TcpServer

Q1: What is the primary responsibility of TcpServer?
A1: It manages the server-side networking, OSC message routing, client connections, room state, and game logic.
    It acts as the central hub for the multiplayer application, processing incoming requests and broadcasting
    updates to connected clients. It also includes administrative features (console commands, rate limiting, banning).

Q2: Why is there a _sync object used with lock statements throughout the class?
A2: The server handles multiple clients concurrently. The Update() loop runs on the main thread, but network
    callbacks (like OnRegister, OnDisconnect) are triggered from the OSC dispatcher, which may be called from
    different contexts (e.g., thread pool). To prevent race conditions when accessing shared collections
    (_connections, _clients, _rooms, etc.), we use lock(_sync) to ensure thread safety.

Q3: Why does the server use a synchronous Update() loop with Thread.Sleep instead of async/await?
A3: This is a console application with no UI, so a simple synchronous loop is sufficient. It processes network
    updates at ~25 Hz, which is adequate for a test server. Async/await would add complexity without much benefit.
    The internal TCP operations (TcpListener, TcpClient) may still be asynchronous, but the top-level loop is
    synchronous for simplicity and clarity.

Q4: How does the server handle client connections and disconnections?
A4: AcceptNewConnections() checks for pending connections and adds them to the _connections list. UpdateConnections()
    reads incoming packets from each connection. CleanupConnections() removes connections that are no longer connected
    and calls RemoveClient() to clean up associated data (rooms, IDs). Disconnections are also triggered by the
    C_DISCONNECT OSC message, which calls RemoveClient().

Q5: Why are there separate methods for AcceptNewConnections, UpdateConnections, and CleanupConnections?
A5: This follows the Single Responsibility Principle. Each method handles one aspect of the connection lifecycle:
    accepting new ones, processing data from existing ones, and cleaning up dead ones. This makes the code easier
    to read, test, and modify.

Q6: What is the purpose of the OSCDispatcher, and how is it used?
A6: The OSCDispatcher routes incoming OSC messages to registered handlers based on the message address. We register
    handlers for C_REGISTER and C_DISCONNECT in Start(). When a packet arrives, UpdateConnections() calls
    _dispatcher.HandlePacket(), which invokes the appropriate callback (OnRegister or OnDisconnect). This decouples
    message parsing from business logic.

Q7: How does the server handle invalid or malicious clients?
A7: The server implements:
    - Rate limiting: tracks requests per second per IP. If a client exceeds MaxRequestsPerSecond (50), it gets a ban
      strike. After BanThreshold (5) strikes, the IP is banned for BanDurationSeconds (300s).
    - Malicious strikes: The AddMaliciousStrike() method increments a per-client counter; after 3 strikes, the user
      is kicked. This can be triggered by invalid messages or abuse.
    - Capped usernames: OnRegister reads strings with a maximum length (12) to prevent buffer overflow or spam.

Q8: Why does the server store clients in a Dictionary<int, ClientInfo> by ID and a separate Dictionary<TcpNetworkConnection, int> mapping connections to IDs?
A8: Having both dictionaries allows efficient lookups by either ID (for console commands) or by connection (for
    handling incoming messages where we only have the connection). This avoids iterating over the entire client list
    for each operation.

Q9: How does the server manage rooms and participants?
A9: Rooms are stored in a Dictionary<string, RoomData> keyed by room name. Each RoomData contains a list of
    Participant objects (id, name, score). When a client creates or joins a room, the room is updated, and the
    client's CurrentRoom field is set. When a client disconnects, they are removed from the room, and if the room
    becomes empty, it is deleted.

Q10: What is the role of the LobbyState and GameState properties?
A10: They likely encapsulate the game logic for the lobby (e.g., room listing, joining) and the active game
    (e.g., turn management, dice rolls). This separates concerns: TcpServer handles networking and state storage,
    while these state objects handle the specific game rules. The server passes itself to them via constructor,
    allowing them to access clients and send messages.

Q11: Why are there console command methods (GetAllPlayersInfo, KickUser, SendPrivateMessage, etc.)?
A11: These methods expose server functionality to the ConsoleCommandHandler, allowing administrators to monitor
    and control the server via the console. This is useful for debugging, moderation, and manual testing without
    a client UI. The methods are designed to be called from a separate thread (or main loop) safely, using locks.

Q12: How does the server send messages to clients, and why is there a Send method that catches exceptions?
A12: The Send() method wraps conn.Send(msg.GetBytes()) in try-catch. This prevents exceptions (e.g., ObjectDisposedException)
    from crashing the server if a client disconnects abruptly while we are sending. It logs the error and continues,
    maintaining server stability.

Q13: Why does the server use IPEndPoint for identifying clients in some places and TcpNetworkConnection in others?
A13: The OSC dispatcher passes the remote IPEndPoint, so we use GetConnectionByEndpoint to find the corresponding
    connection. This is necessary because the dispatcher only knows the endpoint, not the connection object. The
    server then uses the connection for sending messages.

Q14: How does the server handle broadcasting to a room or to all clients?
A14: BroadcastToRoom() iterates over all clients and sends the message to those whose CurrentRoom matches the room
    name. BroadcastToAll() sends to every connected client. Both methods use locking to safely iterate over the
    client dictionary.

Q15: Why are there separate messages for S_CREATED_ROOM, S_JOINED, S_ROOM_UPDATE, etc.?
A15: These messages correspond to the OSC messages defined in the client's Msg class. They ensure the server and
    client communicate using a consistent protocol. Each message type carries specific data (room name, participant
    count, host, etc.) to update the client's UI state accordingly.

Q16: How does the server handle the case where a host leaves a room?
A16: In RemoveClient(), if the client is in a room and was the host, the server finds the first remaining participant
    and assigns them as the new host (room.host = newHost.clientName). This ensures the room remains functional
    even if the original host disconnects.

Q17: Why is there a _maliciousStrikes dictionary and an AddMaliciousStrike method?
A17: This is an additional security measure to automatically kick abusive clients after repeated violations (e.g.,
    sending invalid messages, spamming, trying to cheat). It complements the IP-based rate limiting. The strikes
    are per client, not per IP, so a legitimate user on a shared IP is not unfairly penalised.

Q18: What is the purpose of _selectedUser in the server?
A18: It stores a reference to a client selected via a console command (SelectUser). This allows subsequent commands
    (e.g., SendPrivateMessage, ChangeUserName) to operate on that user without needing to specify the ID each time.
    It's a convenience for interactive console use.

Q19: Why does the server use a ReaderWriterLockSlim or only a simple lock?
A19: The code uses a simple lock (_sync) for all shared resource access. Since the server is not highly concurrent
    (one main Update loop and occasional callbacks), a simple lock is sufficient and avoids the complexity of
    ReaderWriterLockSlim. This keeps the code straightforward.

Q20: How does the server handle the transition from lobby to game (StartRoom)?
A20: When StartRoom is called (via console or internal logic):
     1. It sets room.GameStarted = true.
     2. It sends S_GAME_STARTED to all clients in the room, instructing them to load the game scene.
     3. It broadcasts an S_ROOM_UPDATE to ALL clients with gameStarted = true, so the room disappears from the lobby list.
     4. It calls game.StartGameForRoom(room) to initialise game state (e.g., dice rolling order).
    This ensures a smooth transition for all clients.

Q21: Why is there no async/await in the server's networking code, while the client uses async/await extensively?
A21: The client is a Unity application with a UI that must remain responsive. Async/await allows non-blocking network
    operations without freezing the main thread. The server is a console app that runs in a single thread with a
    dedicated update loop – it doesn't have a UI to freeze, so a synchronous loop is simpler and more predictable.
    The server's TcpNetworkConnection class may internally use async I/O (BeginReceive), but the exposed Update() method
    processes data synchronously. This design works well for both environments.

Q22: How does the server ensure data consistency when modifying rooms and clients simultaneously?
A22: All modifications to shared collections are guarded with lock(_sync). This includes adding/removing clients,
    updating room participants, and changing CurrentRoom. The lock ensures that operations are atomic and that the
    server state remains consistent even when multiple callbacks are invoked concurrently.

Q23: What are the weaknesses of this server design?
A23: Some potential weaknesses:
      - The synchronous loop with Thread.Sleep may not scale to many clients.
      - No persistent storage (rooms are lost on restart).
      - Limited error recovery (e.g., if a client disconnects uncleanly, the server might not detect it immediately).
      - The rate limiting and banning are simple and could be bypassed with distributed attacks.
      - Console commands are mixed with server logic, making the class large.
    However, for a test/development server, these trade-offs are acceptable.

Q24: Why is Msg.PORT defined as 55000 in the client, and the server defaults to that port?
A24: Consistency. Both sides use the same port number, so they can communicate without additional configuration.
    55000 is an arbitrary, unprivileged port commonly used for custom applications, avoiding conflicts with well-known services.

Q25: How does the server handle malformed OSC packets?
A25: The OSCDispatcher may catch parsing exceptions, and the UpdateConnections() loop catches general exceptions
    when reading from a connection. If a packet cannot be parsed, it is skipped, and an error is logged. The client
    connection is not immediately dropped; the server continues processing subsequent packets. This makes the server
    resilient to accidental corruption.
*/