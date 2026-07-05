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
    /*
     * TcpServer
     *
     * Purpose:
     * This is the main TCP OSC server for Creeper Dice.
     *
     * It handles:
     * - Starting and stopping the TCP server.
     * - Accepting new TCP clients.
     * - Reading OSC packets from clients.
     * - Dispatching OSC messages to the correct server state.
     * - Registering clients.
     * - Disconnecting and cleaning up clients.
     * - Tracking rooms.
     * - Sending OSC messages to one client, a room, or everyone.
     * - Basic security checks like rate limits, registration checks, and malicious strikes.
     * - Console command support for debugging/admin control.
     *
     * Naming rule used:
     * - On prefix = receives OSC message from a client.
     * - Send prefix = sends OSC message to client, room, or all clients.
     * - No On prefix = normal logic, helper, registry, validation, security, or console function.
     *
     * Important:
     * This server is authoritative.
     * Clients send requests, but the server decides what is allowed.
     */
    public class TcpServer
    {
        #region Sync

        /*
         * _sync:
         * Lock object used to protect shared server data.
         *
         * Why this matters:
         * The server stores clients, rooms, and connections in shared collections.
         * Locking helps avoid issues when those collections are accessed or changed.
         */
        private readonly object _sync = new object();

        /*
         * Public lock access.
         * Other server systems can use the same lock if needed.
         */
        public object SyncRoot => _sync;

        #endregion

        #region Networking Fields

        /*
         * _listener:
         * TCP listener that waits for new client connections.
         */
        private TcpListener _listener;

        /*
         * _connections:
         * All currently connected TCP connections.
         */
        private readonly List<TcpNetworkConnection> _connections = new();

        /*
         * _dispatcher:
         * OSC message dispatcher.
         *
         * It maps OSC addresses like /c_register or /c_select_dice
         * to the correct server function.
         */
        private readonly OSCDispatcher _dispatcher;

        /*
         * _isShuttingDown:
         * True when the server is closing.
         * Used to stop Update from processing more server logic.
         */
        private bool _isShuttingDown;

        /*
         * Public access to the OSC dispatcher.
         * LobbyState and GameState use this to register their own OSC handlers.
         */
        public OSCDispatcher Dispatcher => _dispatcher;

        #endregion

        #region Client State

        /*
         * _nextId:
         * Next server assigned client id.
         *
         * First client gets id 1.
         * Then this value increases.
         */
        private int _nextId = 1;

        /*
         * _clients:
         * Registered clients.
         *
         * Key:
         * Client id.
         *
         * Value:
         * ClientInfo for that player.
         */
        private readonly Dictionary<int, ClientInfo> _clients = new();

        /*
         * _connectionToId:
         * Links a TCP connection to a registered client id.
         *
         * This lets the server find who sent a packet.
         */
        private readonly Dictionary<TcpNetworkConnection, int> _connectionToId = new();

        /*
         * _selectedUser:
         * Used by console commands.
         * This is the currently selected user for admin/debug actions.
         */
        private ClientInfo _selectedUser;

        /*
         * MaxUsernameLength:
         * Maximum username length accepted by the server.
         */
        private const int MaxUsernameLength = 12;

        /*
         * Legacy addresses:
         * Older Unity scripts may still use these old OSC addresses.
         * They are kept for compatibility.
         */
        private const string LegacyRegisterAddress = "/register";
        private const string LegacyRegisteredAddress = "/registered";
        private const string LegacyDisconnectAddress = "/disconnect";

        /*
         * Read-only public access to connected clients.
         */
        public IReadOnlyDictionary<int, ClientInfo> Clients => _clients;

        #endregion

        #region Room State

        /*
         * _rooms:
         * All active rooms on the server.
         *
         * Key:
         * Room name.
         *
         * Value:
         * RoomData for that room.
         */
        private readonly Dictionary<string, RoomData> _rooms = new();

        public IReadOnlyDictionary<string, RoomData> Rooms => _rooms;

        #endregion

        #region Security State

        /*
         * _rateLimits:
         * Tracks request rate per IP address.
         * Used to block spam.
         */
        private readonly Dictionary<IPAddress, ClientRateInfo> _rateLimits = new();

        /*
         * _bannedIPs:
         * IPs temporarily banned for rate limit abuse.
         */
        private readonly HashSet<IPAddress> _bannedIPs = new();

        // a buffer for any weird packer tepeted sends or if user frustratedly presses buttons too fast and a for some reason it sends multiple requests. This is a reasonable limit for a single user.
        private const int MaxRequestsPerSecond = 50;
        /*
         * BanThreshold:
         * Amount of repeated rate limit abuses before an IP is banned.
         */
        private const int BanThreshold = 3;

        /*
         * BanDurationSeconds:
         * How long an IP stays banned.
         */
        private const int BanDurationSeconds = 3000;

        /*
         * _maliciousStrikes:
         * Tracks bad validated gameplay/lobby actions per registered client.
         *
         * Example:
         * Selecting dice when it is not your turn can add a strike.
         */
        public readonly Dictionary<ClientInfo, int> _maliciousStrikes = new();

        #endregion

        #region Server States

        public readonly LobbyState lobby;
        public readonly GameState game;

        #endregion

        #region Constructor

        /*
         * Constructor.
         *
         * What this does:
         * Creates the OSC dispatcher.
         * Creates GameState and LobbyState.
         *
         * Important:
         * GameState and LobbyState register their own OSC handlers inside their constructors.
         */
        public TcpServer()
        {
            _dispatcher = new OSCDispatcher();

            game = new GameState(this);
            lobby = new LobbyState(this);
        }

        #endregion

        #region Server Lifecycle

        /*
         * What this does:
         * Starts the TCP server on the given port.
         *
         * Flow:
         * 1. Create TcpListener on all network interfaces.
         * 2. Start listening.
         * 3. Register general server OSC handlers.
         * 4. Print available server IP addresses.
         */
        public void Start(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            RegisterHandlers();

            Console.WriteLine($"TCP OSC Server running on port {port}");
            PrintServerAddresses(port);
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

        /*
 * What this does:
 * Main server update loop.
 *
 * Expected caller:
 * Program.Main calls this repeatedly.
 *
 * Flow:
 * 1. Check if the server is shutting down.
 * 2. Lock the shared server state.
 * 3. Accept new TCP clients.
 * 4. Read packets from connected clients.
 * 5. Dispatch valid OSC messages.
 * 6. Clean up dead connections.
 * 7. Update game state logic, such as stake answer timeouts.
 *
 * Why lock(_sync) is used here:
 * The server has more than one thread.
 *
 * Main server thread:
 * - Runs this Update method.
 * - Handles client packets.
 * - Changes clients, rooms, and game state.
 *
 * Console command thread:
 * - Reads admin commands from the console.
 * - Can also change clients, rooms, and game state.
 *
 * Without this lock:
 * The console thread could change a room, player, or game state at the same time
 * that the main server thread is processing gameplay.
 *
 * Example problem:
 * The game is looping through room.Participants while a console command removes a player.
 *
 * Using lock(_sync):
 * Makes sure only one thread can change shared server data at a time.
 * This keeps clients, rooms, and GameState safer.
 * Instead of making separate random locks
 */
        public void Update()
        {
            if (_isShuttingDown)
                return;

            try
            {
                lock (_sync)
                {
                    AcceptNewConnections();
                    UpdateConnections();
                    CleanupConnections();

                    game.Update();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER UPDATE ERROR] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        #endregion

        #region Message Registration

        /*
         * What this does:
         * Registers general server OSC handlers.
         *
         * OSC received:
         *
         * Msg.C_REGISTER
         * Payload:
         * [0] string username
         *
         * LegacyRegisterAddress "/register"
         * Payload:
         * [0] string username
         *
         * Msg.C_DISCONNECT
         * Payload:
         * No data.
         *
         * LegacyDisconnectAddress "/disconnect"
         * Payload:
         * No data.
         *
         * Msg.C_PING
         * Payload:
         * No data.
         */
        private void RegisterHandlers()
        {
            _dispatcher.AddListener(Msg.C_REGISTER, OnRegister, OSCUtil.STRING);
            _dispatcher.AddListener(LegacyRegisterAddress, OnRegister, OSCUtil.STRING);
            _dispatcher.AddListener(Msg.C_DISCONNECT, OnDisconnect);
            _dispatcher.AddListener(LegacyDisconnectAddress, OnDisconnect);
            _dispatcher.AddListener(Msg.C_PING, OnPing);
        }

        #endregion

        #region Received OSC Messages

        /*
         * OSC RECEIVE: Msg.C_REGISTER
         *
         * Payload received:
         * [0] string username
         *
         * Example:
         * username = "Nik"
         *
         * What this does:
         * Registers a new TCP connection as a real player/client.
         *
         * Validation:
         * - Connection must exist.
         * - Connection must not already be registered.
         * - Username must not be too long.
         * - Username must not be empty.
         *
         * Sends on success:
         * Msg.S_REGISTERED
         * Payload:
         * [0] int id
         * [1] string username
         *
         * Also sends legacy /registered for older clients.
         */
        private void OnRegister(OSCMessageIn msg, IPEndPoint sender)
        {
            TcpNetworkConnection connection = GetConnectionByEndpoint(sender);

            if (connection == null)
                return;

            if (GetClientByConnection(connection) != null)
            {
                SendError(connection, "This connection is already registered.");
                return;
            }

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

            // Compatibility for older Unity scripts that still listen for /registered.
            var legacyReply = new OSCMessageOut(LegacyRegisteredAddress)
                .AddInt(id)
                .AddString(username);

            SendToConnection(connection, legacyReply);
        }

        /*
         * OSC RECEIVE: Msg.C_PING
         *
         * Payload received:
         * No data.
         *
         * What this does:
         * Replies with Msg.S_PONG so the client knows the server is still alive.
         *
         * Sends:
         * Msg.S_PONG
         * Payload:
         * No data.
         */
        private void OnPing(OSCMessageIn msg, IPEndPoint sender)
        {
            var conn = GetConnectionByEndpoint(sender);

            if (conn == null)
                return;

            var pong = new OSCMessageOut(Msg.S_PONG);
            Send(conn, pong);
        }

        /*
         * OSC RECEIVE: Msg.C_DISCONNECT
         *
         * Payload received:
         * No data.
         *
         * What this does:
         * Removes a client from server state and closes their TCP connection.
         */
        private void OnDisconnect(OSCMessageIn msg, IPEndPoint sender)
        {
            TcpNetworkConnection connection = GetConnectionByEndpoint(sender);
            ClientInfo client = GetClientByEndpoint(sender);

            if (client != null)
                RemoveClient(client);

            if (connection != null)
            {
                lock (_sync)
                    _connections.Remove(connection);

                connection.Close();
            }
        }

        #endregion

        #region Connection Updating

        /*
         * What this does:
         * Accepts all pending TCP connections from the listener.
         *
         * New connections are added to _connections.
         * They are not registered yet.
         *
         * Important:
         * A connected socket is not a registered player until it sends Msg.C_REGISTER.
         */
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

        /*
         * What this does:
         * Updates all active TCP connections and processes their packets.
         *
         * A snapshot is used so the server can loop safely even if the real list changes.
         */
        private void UpdateConnections()
        {
            List<TcpNetworkConnection> snapshot;

            lock (_sync)
                snapshot = _connections.ToList();

            foreach (TcpNetworkConnection connection in snapshot)
                ProcessConnectionPackets(connection);
        }

        /*
         * What this does:
         * Reads all available packets from one TCP connection.
         *
         * Flow:
         * 1. Ignore null or disconnected connections.
         * 2. Read all available packets.
         * 3. Apply rate limiting.
         * 4. Validate if the packet is allowed for this connection.
         * 5. Pass valid packets to the OSC dispatcher.
         */
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

                    if (!ValidatePacketAllowedForConnection(connection, packet))
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

        /*
         * What this does:
         * Security validation before dispatching an OSC packet.
         *
         * Rules:
         * - Packet must have a readable OSC header.
         * - Unregistered connections may only send register.
         * - Registered connections cannot register again.
         *
         * Returns:
         * true if packet can be dispatched.
         * false if packet should be blocked.
         */
        private bool ValidatePacketAllowedForConnection(TcpNetworkConnection connection, byte[] packet)
        {
            if (!TryReadOscHeader(packet, out string header))
            {
                Console.WriteLine($"[SECURITY] Dropping corrupt packet from {connection.Remote}.");
                DropConnection(connection, "Corrupt packet.");
                return false;
            }

            ClientInfo client = GetClientByConnection(connection);

            if (client == null)
            {
                if (IsRegistrationAddress(header))
                    return true;

                Console.WriteLine($"[SECURITY] Unregistered connection {connection.Remote} tried to send {header}.");
                DropConnection(connection, "Register before sending gameplay or lobby messages.");
                return false;
            }

            if (IsRegistrationAddress(header))
            {
                SendError(connection, "Already registered.");
                return false;
            }

            return true;
        }

        /*
         * What this does:
         * Attempts to read the OSC address/header from a raw packet.
         *
         * Output:
         * header = OSC address, or "#bundle" if packet is an OSC bundle.
         *
         * Returns:
         * true if a header was found.
         * false if packet is corrupt or unreadable.
         */
        private bool TryReadOscHeader(byte[] packet, out string header)
        {
            header = null;

            try
            {
                if (packet == null || packet.Length == 0)
                    return false;

                if (OSCObject.IsBundle(packet))
                {
                    header = "#bundle";
                    return true;
                }

                OSCMessageIn message = new OSCMessageIn(packet);

                if (message.corrupt || string.IsNullOrEmpty(message.header))
                    return false;

                header = message.header;
                return true;
            }
            catch
            {
                return false;
            }
        }


        private bool IsRegistrationAddress(string header)
        {
            return header == Msg.C_REGISTER || header == LegacyRegisterAddress;
        }

        /*
         * What this does:
         * Sends an error, removes the client if registered, removes the connection,
         * and closes the TCP socket.
         */
        private void DropConnection(TcpNetworkConnection connection, string reason)
        {
            if (connection == null)
                return;

            SendError(connection, reason);

            ClientInfo client = GetClientByConnection(connection);

            if (client != null)
                RemoveClient(client);

            lock (_sync)
                _connections.Remove(connection);

            connection.Close();
        }

        /*
         * What this does:
         * Finds disconnected/dead connections and removes them.
         */
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

        /*
         * What this does:
         * Cleans one dead connection.
         *
         * If the connection belongs to a registered client,
         * that client is removed from client and room state.
         */
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

        /*
         * What this does:
         * Fully removes a registered client from the server.
         *
         * Flow:
         * 1. Remove from current room if needed.
         * 2. Remove connection-to-id mapping.
         * 3. Remove from client dictionary.
         * 4. Remove malicious strikes.
         */
        public void RemoveClient(ClientInfo client)
        {
            lock (_sync)
            {
                if (client == null)
                    return;

                RemoveClientFromRoom(client);

                _connectionToId.Remove(client.Connection);
                _clients.Remove(client.Id);
                _maliciousStrikes.Remove(client);

                Console.WriteLine($"[DISCONNECT] {client.Name} (ID {client.Id}) disconnected");
            }
        }

        /*
         * What this does:
         * Removes a client from their current room.
         *
         * Cases:
         * - Room becomes empty: remove room.
         * - Host leaves before game starts: assign new host.
         * - Host leaves during game: close room.
         * - Non-host leaves during game: update GameState.
         */
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
                if (room.GameStarted && room.data != null)
                    game.HandlePlayerRemovedFromGame(room, client.Id, $"{client.Name} left the game.");

                SendRoomUpdate(room);
            }
        }

        /*
         * What this does:
         * Closes a room when the host disconnects during an active game.
         *
         * Sends:
         * Msg.S_RETURN_TO_LOBBY to players still in the room.
         * Msg.S_CLOSED_ROOM to all clients.
         */
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

        /*
         * OSC SEND: Msg.S_ROOM_UPDATE
         *
         * Payload sent:
         * [0] string roomName
         * [1] int participantCount
         * [2] string hostName
         * [3] int pointGoal
         * [4] bool gameStarted
         *
         * What this does:
         * Sends the latest room information to all clients.
         */
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

        /*
         * What this does:
         * Finds a registered client from a TCP connection.
         */
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

        /*
         * What this does:
         * Finds a registered client from their remote endpoint.
         */
        public ClientInfo GetClientByEndpoint(IPEndPoint endpoint)
        {
            TcpNetworkConnection connection = GetConnectionByEndpoint(endpoint);
            return GetClientByConnection(connection);
        }

        /*
         * What this does:
         * Finds the TCP connection for a remote endpoint.
         */
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
        //can be accesed by Console comand thread so lock is needed to avoid errors or raise exceptions

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

        /*
         * OSC SEND:
         * Sends one OSC message to one TCP connection.
         *
         * This is the lowest-level send helper.
         */
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

        /*
         * OSC SEND:
         * Sends one OSC message to one registered client.
         */
        public void SendToClient(ClientInfo client, OSCMessageOut msg)
        {
            if (client == null)
                return;

            SendToConnection(client.Connection, msg);
        }

        /*
         * OSC SEND: Msg.S_ERROR
         *
         * Payload sent:
         * [0] string message
         *
         * What this does:
         * Sends an error message to one connection.
         */
        public void SendError(TcpNetworkConnection connection, string message)
        {
            var errorMsg = new OSCMessageOut(Msg.S_ERROR)
                .AddString(message);

            SendToConnection(connection, errorMsg);

            Console.WriteLine($"[ERROR] Sent to {connection?.Remote}: {message}");
        }

        /*
         * OSC SEND: Msg.S_SERVER_MESSAGE
         *
         * Payload sent:
         * [0] string message
         *
         * Sends a server text message to one client.
         */
        public void SendServerMessageToClient(ClientInfo client, string message)
        {
            var msg = new OSCMessageOut(Msg.S_SERVER_MESSAGE)
                .AddString(message);

            SendToClient(client, msg);
        }

        /*
         * OSC SEND: Msg.S_SERVER_MESSAGE
         *
         * Payload sent:
         * [0] string message
         *
         * Sends a server text message to one room.
         */
        public void SendServerMessageToRoom(string roomName, string message)
        {
            var msg = new OSCMessageOut(Msg.S_SERVER_MESSAGE)
                .AddString(message);

            SendToRoom(roomName, msg);
        }

        /*
         * OSC SEND: Msg.S_SERVER_MESSAGE
         *
         * Payload sent:
         * [0] string message
         *
         * Sends a server text message to every registered client.
         */
        public void SendServerMessageToAll(string message)
        {
            var msg = new OSCMessageOut(Msg.S_SERVER_MESSAGE)
                .AddString(message);

            SendToAll(msg);
        }

        /*
         * OSC SEND:
         * Sends one OSC message to all clients currently inside a room.
         */
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

        /*
         * OSC SEND:
         * RoomData overload for SendToRoom.
         */
        public void SendToRoom(RoomData room, OSCMessageOut msg)
        {
            if (room == null)
                return;

            SendToRoom(room.roomName, msg);
        }

        /*
         * OSC SEND:
         * Sends one OSC message to every registered client.
         */
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

        /*
         * Compatibility wrapper.
         * Some older code calls Send instead of SendToConnection.
         */
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

        /*
         * What this does:
         * Checks if an endpoint should be blocked because of rate limiting.
         *
         * Rules:
         * - If IP is banned, block immediately.
         * - Track how many packets this IP sends in one second.
         * - If request count is too high many times, ban the IP temporarily.
         *
         * Returns:
         * true if the packet should be blocked.
         * false if the packet can continue.
         */
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

        #region Server Info Printing

        /*
         * What this does:
         * Prints useful local IPv4 addresses to the console.
         *
         * This helps the host know which IP other devices on the same network should connect to.
         */
        private void PrintServerAddresses(int port)
        {
            Console.WriteLine();
            Console.WriteLine("=== Server Network Addresses ===");
            Console.WriteLine($"Listening on all interfaces: 0.0.0.0:{port}");
            Console.WriteLine($"Same PC only: 127.0.0.1:{port}");
            Console.WriteLine();
            Console.WriteLine("Use one of these IPv4 addresses from another device on the same network:");

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                IPInterfaceProperties properties = networkInterface.GetIPProperties();

                foreach (UnicastIPAddressInformation addressInfo in properties.UnicastAddresses)
                {
                    IPAddress address = addressInfo.Address;

                    if (address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    if (IPAddress.IsLoopback(address))
                        continue;

                    Console.WriteLine($" - {address}:{port}    ({networkInterface.Name})");
                }
            }

            Console.WriteLine("================================");
            Console.WriteLine();
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

        /*
         * Console command support.
         *
         * What this does:
         * Kicks a user by client id.
         *
         * Sends:
         * Msg.S_RETURN_TO_LOBBY
         * Msg.S_ERROR
         *
         * Then closes their TCP connection.
         */
        public bool KickUser(int id)
        {
            ClientInfo client;

            lock (_sync)
            {
                if (!_clients.TryGetValue(id, out client))
                    return false;
            }

            var returnMsg = new OSCMessageOut(Msg.S_RETURN_TO_LOBBY)
                .AddString("You were kicked by the server.");

            SendToConnection(client.Connection, returnMsg);
            SendError(client.Connection, "You were kicked by the server.");

            lock (_sync)
                _connections.Remove(client.Connection);

            RemoveClient(client);
            client.Connection.Close();

            Console.WriteLine($"Kicked user {client.Name} (ID {id}) and closed their TCP connection.");

            return true;
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

        /*
         * Console command support.
         *
         * What this does:
         * Creates a fake user for testing server lists and commands.
         *
         * Important:
         * The fake connection is not a real connected client.
         */
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

        /*
         * Console command support.
         *
         * What this does:
         * Creates a room from the server console.
         *
         * Sends:
         * Msg.S_JOINED to the host.
         * Msg.S_ROOM_UPDATE to all clients.
         */
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

        /*
         * Console command support.
         *
         * What this does:
         * Closes a room by room name.
         *
         * Sends:
         * Msg.S_CLOSED_ROOM to all clients.
         */
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

        /*
         * Console command support.
         *
         * What this does:
         * Starts a room from the server console.
         *
         * Sends:
         * Msg.S_GAME_STARTED to the room.
         * Msg.S_ROOM_UPDATE to all clients.
         *
         * Difference from normal lobby start:
         * This also directly calls game.StartGameForRoom(room).
         */
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