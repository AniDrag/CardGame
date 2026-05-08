using OSCTools;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
/// <summary>
/// OSC Server for a multiplayer game.
/// Handles client registration, room management (create, join, leave), game start,
/// rate limiting, string length caps, and console administration commands.
/// Uses UDP + OSC (Open Sound Control) protocol.
/// </summary>
class OSCServer
{
    // ------------------------- NETWORK & DISPATCHER -------------------------
    private static UdpClient udp = null!;
    private static OSCDispatcher dispatcher = null!;


    // ------------------------- CLIENT & ROOM DATA -------------------------
    private static int nextId = 1;                                  // Next available client ID
    private static readonly Dictionary<int, ClientInfo> clients = new();   // ID -> client info
    private static readonly Dictionary<string, RoomEntryData> rooms = new();        // room name -> room
    private static readonly Dictionary<IPEndPoint, int> endpointToId = new(); // endpoint -> client ID

    // ------------------------- RATE LIMITING & SECURITY -------------------------
    private static readonly Dictionary<IPAddress, ClientRateInfo> rateLimits = new();
    private static readonly HashSet<IPAddress> bannedIPs = new();

    private const int MAX_REQUESTS_PER_SECOND = 50;   // Max packets per second per IP
    private const int BAN_THRESHOLD = 5;              // How many violations before ban
    private const int BAN_DURATION_SECONDS = 300;     // 5 minutes

    private const int MAX_USERNAME_LENGTH = 12;       // Prevent oversized usernames
    private const int MAX_ROOM_NAME_LENGTH = 20;      // Prevent oversized room names
    private const int MAX_MESSAGE_STRING_LENGTH = 150; // For future use

    // ------------------------- SHUTTING DOWN -------------------------
    private static bool isShuttingDown = false;

    // ------------------------- MAIN ENTRY POINT -------------------------
    /// <summary>
    /// Starts the UDP server, registers OSC handlers, and enters the console command loop.
    /// </summary>
    static void Main()
    {
        udp = new UdpClient(55000);
        dispatcher = new OSCDispatcher();

        // Register OSC message handlers – these are called when a client sends the matching address.
        dispatcher.AddListener("/register", OnRegister, OSCUtil.STRING);
        dispatcher.AddListener("/disconnect", OnDisconnect);
        dispatcher.AddListener("/create_room", OnCreateRoom, OSCUtil.STRING, OSCUtil.INT);
        dispatcher.AddListener("/join_room", OnJoinRoom, OSCUtil.STRING);
        dispatcher.AddListener("/leave_room", OnLeaveRoom);
        dispatcher.AddListener("/start_game", OnStartGame);
        dispatcher.AddListener("/list_rooms", OnListRooms);
        dispatcher.AddListener("/close_room", OnCloseRoom);

        Console.WriteLine("OSC Server running on port 55000");
        Console.WriteLine("Type /help for list of console commands.");
        udp.BeginReceive(OnReceive, null);

        // Catch Ctrl+C (SIGINT) to send shutdown notification
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            if (isShuttingDown) return;
            isShuttingDown = true;

            Console.WriteLine("\nForce shutdown requested. Notifying clients...");
            try
            {
                BroadcastToAll("Server is shutting down (forced).");
                System.Threading.Thread.Sleep(200);
            }
            catch { }
            finally
            {
                udp?.Close();
                Environment.Exit(0);
            }
        };

        // Optional: handle normal process exit (e.g., when console window is closed)
        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            // Very limited time is available here; do minimal work
            try
            {
                BroadcastToAll("Server is shutting down.");
                System.Threading.Thread.Sleep(100);
            }
            catch { }
        };

        // Console command loop (runs on main thread)
        while (true)
        {
            string input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) continue;

            if (!input.StartsWith("/"))
            {
                Console.WriteLine("Commands start with '/'. Type /help");
                continue;
            }

            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLower();
            ConsoleComands(cmd, parts);
            
        }
    }
    #region Server Helper methods
    private static void ConsoleComands(string cmd, string[] parts)
    {
        switch (cmd)
        {
            case "/help":
                ShowHelp();
                break;
            case "/all":
                if (parts.Length > 1 && parts[1].ToLower() == "rooms")
                    ListAllRooms();
                else if (parts.Length > 1 && parts[1].ToLower() == "players")
                    ListAllPlayers();
                else
                    Console.WriteLine("Usage: /all rooms | /all players");
                break;
            case "/playerid":
                if (parts.Length < 2)
                    Console.WriteLine("Usage: /playerid <id>");
                else if (int.TryParse(parts[1], out int id))
                    ShowPlayerById(id);
                else
                    Console.WriteLine("Invalid ID.");
                break;
            case "/player":
                if (parts.Length < 2)
                    Console.WriteLine("Usage: /player <name>");
                else
                    ShowPlayerByName(parts[1]);
                break;
            case "/close":
            case "/shutdown":
                ShutdownServer();
                return; // exit the loop and program
            case "/broadcast":
                if (parts.Length < 2)
                    Console.WriteLine("Usage: /broadcast <message>");
                else
                {
                    string message = string.Join(" ", parts, 1, parts.Length - 1);
                    BroadcastToAll(message);
                }
                break;
            default:
                Console.WriteLine("Unknown command. Type /help");
                break;
        }
    }


    // ------------------------- RATE LIMITING -------------------------
    /// <summary>
    /// Checks whether a client's IP should be blocked due to rate limiting or previous ban.
    /// Updates request counters and bans IPs that exceed the threshold.
    /// </summary>
    /// <param name="endpoint">The client's IP endpoint.</param>
    /// <returns>True if the packet should be dropped, false if allowed.</returns>
    private static bool ShouldBlock(IPEndPoint endpoint)
    {
        var ip = endpoint.Address;
        if (bannedIPs.Contains(ip))
            return true;

        if (!rateLimits.TryGetValue(ip, out var info))
        {
            info = new ClientRateInfo { LastRequestTime = DateTime.UtcNow, RequestCountInCurrentSecond = 0, BanCount = 0 };
            rateLimits[ip] = info;
        }

        var now = DateTime.UtcNow;
        if ((now - info.LastRequestTime).TotalSeconds >= 1)
        {
            // new second window
            info.RequestCountInCurrentSecond = 0;
            info.LastRequestTime = now;
        }

        info.RequestCountInCurrentSecond++;
        if (info.RequestCountInCurrentSecond > MAX_REQUESTS_PER_SECOND)
        {
            info.BanCount++;
            if (info.BanCount >= BAN_THRESHOLD)
            {
                bannedIPs.Add(ip);
                Console.WriteLine($"Banned IP {ip} due to rate limit abuse.");
                // Schedule unban after BAN_DURATION_SECONDS
                _ = Task.Delay(BAN_DURATION_SECONDS * 1000).ContinueWith(_ => { bannedIPs.Remove(ip); Console.WriteLine($"Unbanned IP {ip}"); });
            }
            return true; // block this request
        }
        else
        {
            info.BanCount = 0; // reset ban count if behaving
            return false;
        }
    }

    // ------------------------- STRING VALIDATION -------------------------
    /// <summary>
    /// Reads a string from an OSC message and validates its length.
    /// If the string is too long or null, logs an error and returns null.
    /// Use this for all user‑supplied strings to prevent abuse.
    /// </summary>
    private static string ReadCappedString(OSCMessageIn msg, int maxLength, string fieldName)
    {
        string value = msg.ReadString();
        if (value == null || value.Length > maxLength)
        {
            Console.WriteLine($"Invalid {fieldName} length (max {maxLength}): {value?.Length ?? 0}");
            return null;
        }
        return value;
    }

    #endregion
    // ------------------------- UDP RECEIVE -------------------------
    /// <summary>
    /// Called asynchronously when a UDP packet arrives.
    /// Enforces rate limiting, then passes the raw data to the OSC dispatcher.
    /// </summary>
    private static void OnReceive(IAsyncResult ar)
    {
        if (isShuttingDown) return;

        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        byte[] data = udp.EndReceive(ar, ref sender);

        if (ShouldBlock(sender))
        {
            // Only re‑post if not shutting down
            if (!isShuttingDown)
                udp.BeginReceive(OnReceive, null);
            return;
        }

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Received {data.Length} bytes from {sender}");
        dispatcher.HandlePacket(data, sender);

        if (!isShuttingDown)
            udp.BeginReceive(OnReceive, null);
    }
    //TODO: add this method to broadcast room list updates to all clients
    private static void BroadcastRoomListUpdate(string operation, RoomEntryData room)
    {
        var roomData = new RoomEntryData(
            GetRoomId(room.Name), // generate a deterministic ID (e.g., hash)
            room.Name,
            clients[room.HostId].Name,
            room.PointGoal,
            room.PlayerIds.Count
        );
        string json = JsonUtility.ToJson(roomData);
        var msg = new OSCMessageOut("/room_list_update");
        msg.AddString(operation);
        msg.AddString(json);
        foreach (var client in clients.Values)
            Send(client.Endpoint, msg);
    }
    // Helper: simple ID generator (just for serialization)
    private static int GetRoomId(string roomName) => roomName.GetHashCode();

// Modify OnCreateRoom: after creating room, broadcast "add"
// Modify OnJoinRoom: after joining, broadcast "update" for that room
// Modify OnLeaveRoom: after leaving, broadcast "update" (or "remove" if empty)
// Add OnCloseRoom handler:
private static void OnCloseRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

        if (rooms.TryGetValue(client.CurrentRoom, out var room) && room.HostId == client.Id)
        {
            rooms.Remove(client.CurrentRoom);
            BroadcastRoomListUpdate("remove", room);
            client.CurrentRoom = null;
        }
    }

    // ------------------------- HELPERS: SEND & BROADCAST -------------------------
    #region Messages Helpers
    /// <summary> Sends an OSC message to a specific client. </summary>
    private static void Send(IPEndPoint target, OSCMessageOut msg)
    {
        udp.Send(msg.GetBytes(), msg.GetBytes().Length, target);
    }

    /// <summary> Broadcasts an OSC message to all players in a room, optionally excluding one. </summary>
    private static void BroadcastToRoom(RoomEntryData room, OSCMessageOut msg, int excludeClientId = -1)
    {
        foreach (int id in room.PlayerIds)
        {
            if (id == excludeClientId) continue;
            if (clients.TryGetValue(id, out var client))
                Send(client.Endpoint, msg);
        }
    }

    /// <summary> Finds a client by their UDP endpoint. </summary>
    private static ClientInfo? GetClientByEndpoint(IPEndPoint endpoint)
    {
        return endpointToId.TryGetValue(endpoint, out int id) && clients.TryGetValue(id, out var client) ? client : null;
    }

    /// <summary>
    /// Removes a client from the server.
    /// Also removes them from any room they were in, updates room host if needed,
    /// and notifies remaining players.
    /// </summary>
    private static void RemoveClient(ClientInfo client)
    {
        if (client == null) return;

        // Remove from room if in one
        if (!string.IsNullOrEmpty(client.CurrentRoom) && rooms.TryGetValue(client.CurrentRoom, out var room))
        {
            room.PlayerIds.Remove(client.Id);

            // If host left, assign new host or delete room
            if (room.HostId == client.Id)
            {
                if (room.PlayerIds.Count > 0)
                {
                    room.HostId = room.PlayerIds[0];
                    var hostChangeMsg = new OSCMessageOut("/room_update")
                        .AddString(room.Name)
                        .AddInt(room.PlayerIds.Count)
                        .AddInt(4) // max players
                        .AddString(clients[room.HostId].Name)
                        .AddBool(room.GameStarted);
                    BroadcastToRoom(room, hostChangeMsg);
                }
                else
                {
                    rooms.Remove(room.Name);
                }
            }
            else
            {
                // Just update room info for remaining players
                var updateMsg = new OSCMessageOut("/room_update")
                    .AddString(room.Name)
                    .AddInt(room.PlayerIds.Count)
                    .AddInt(4)
                    .AddString(clients[room.HostId].Name)
                    .AddBool(room.GameStarted);
                BroadcastToRoom(room, updateMsg);
            }
        }

        endpointToId.Remove(client.Endpoint);
        clients.Remove(client.Id);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Client {client.Name} (ID {client.Id}) disconnected");
    }

    private static void OnListRooms(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null) return;

        // Build a list of rooms
        var roomList = new OSCMessageOut("/room_list");
        // Send number of rooms first
        roomList.AddInt(rooms.Count);
        foreach (var room in rooms.Values)
        {
            // For each room: name, pointGoal, hostName, playerCount, state (0=waiting, 1=starting/inGame)
            roomList.AddString(room.Name);
            roomList.AddInt(room.PointGoal);
            roomList.AddString(clients.TryGetValue(room.HostId, out var host) ? host.Name : "Unknown");
            roomList.AddInt(room.PlayerIds.Count);
            roomList.AddInt(room.GameStarted ? 1 : 0);
        }
        Send(sender, roomList);
    }

    #endregion
    // ------------------------- OSC MESSAGE HANDLERS -------------------------
    #region OSC Messages
    /// <summary> Handles /register: assigns a new ID and replies with /registered. </summary>
    private static void OnRegister(OSCMessageIn msg, IPEndPoint sender)
    {
        string username = ReadCappedString(msg, MAX_USERNAME_LENGTH, "username");
        if (username == null)
        {
            var error = new OSCMessageOut("/error").AddString($"Username too long (max {MAX_USERNAME_LENGTH} chars)");
            Send(sender, error);
            return;
        }
        if (string.IsNullOrWhiteSpace(username))
        {
            var error = new OSCMessageOut("/error").AddString("Username cannot be empty");
            Send(sender, error);
            return;
        }

        int id = nextId++;
        var client = new ClientInfo
        {
            Id = id,
            Name = username,
            Endpoint = sender,
            CurrentRoom = null
        };
        clients[id] = client;
        endpointToId[sender] = id;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Registered {username} with ID {id}");

        var reply = new OSCMessageOut("/registered");
        reply.AddInt(id).AddString(username);
        Send(sender, reply);
    }

    /// <summary> Handles /disconnect: removes the client. </summary>
    private static void OnDisconnect(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client != null)
            RemoveClient(client);
    }

    /// <summary> Handles /create_room: creates a new room (client becomes host). </summary>
    private static void OnCreateRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null)
        {
            var error = new OSCMessageOut("/error").AddString("Not registered");
            Send(sender, error);
            return;
        }

        string roomName = ReadCappedString(msg, MAX_ROOM_NAME_LENGTH, "room name");
        if (roomName == null)
        {
            var error = new OSCMessageOut("/error").AddString($"Room name too long (max {MAX_ROOM_NAME_LENGTH} characters)");
            Send(sender, error);
            return;
        }
        int pointGoal = msg.ReadInt();

        if (rooms.ContainsKey(roomName))
        {
            var error = new OSCMessageOut("/error").AddString("Room already exists");
            Send(sender, error);
            return;
        }

        var room = new RoomEntryData
        {
            Name = roomName,
            PointGoal = pointGoal,
            HostId = client.Id,
            PlayerIds = new List<int> { client.Id },
            GameStarted = false
        };
        rooms[roomName] = room;
        client.CurrentRoom = roomName;

        var success = new OSCMessageOut("/room_update")
            .AddString(roomName)
            .AddInt(1)
            .AddInt(4)
            .AddString(client.Name)
            .AddBool(false);
        Send(sender, success);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {client.Name} created room '{roomName}' (goal {pointGoal})");
    }

    /// <summary> Handles /join_room: adds a client to an existing room. </summary>
    private static void OnJoinRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null)
        {
            var error = new OSCMessageOut("/error").AddString("Not registered");
            Send(sender, error);
            return;
        }

        string roomName = ReadCappedString(msg, MAX_ROOM_NAME_LENGTH, "room name");
        if (roomName == null)
        {
            var error = new OSCMessageOut("/error").AddString($"Room name too long (max {MAX_ROOM_NAME_LENGTH} characters)");
            Send(sender, error);
            return;
        }

        if (!rooms.TryGetValue(roomName, out var room))
        {
            var error = new OSCMessageOut("/error").AddString("Room not found");
            Send(sender, error);
            return;
        }

        if (room.PlayerIds.Count >= 4)
        {
            var error = new OSCMessageOut("/error").AddString("Room is full");
            Send(sender, error);
            return;
        }

        if (room.GameStarted)
        {
            var error = new OSCMessageOut("/error").AddString("Game already started");
            Send(sender, error);
            return;
        }

        room.PlayerIds.Add(client.Id);
        client.CurrentRoom = roomName;

        // Notify everyone in the room about the updated player list
        var updateMsg = new OSCMessageOut("/room_update")
            .AddString(room.Name)
            .AddInt(room.PlayerIds.Count)
            .AddInt(4)
            .AddString(clients[room.HostId].Name)
            .AddBool(false);
        BroadcastToRoom(room, updateMsg);

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {client.Name} joined room '{roomName}'");
    }

    /// <summary> Handles /leave_room: removes a client from their current room. </summary>
    private static void OnLeaveRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

        if (rooms.TryGetValue(client.CurrentRoom, out var room))
        {
            room.PlayerIds.Remove(client.Id);
            client.CurrentRoom = null;

            if (room.PlayerIds.Count == 0)
            {
                rooms.Remove(room.Name);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Room '{room.Name}' deleted (empty)");
            }
            else
            {
                if (room.HostId == client.Id && room.PlayerIds.Count > 0)
                {
                    room.HostId = room.PlayerIds[0];
                }
                var updateMsg = new OSCMessageOut("/room_update")
                    .AddString(room.Name)
                    .AddInt(room.PlayerIds.Count)
                    .AddInt(4)
                    .AddString(clients[room.HostId].Name)
                    .AddBool(false);
                BroadcastToRoom(room, updateMsg);
            }
        }

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {client.Name} left room");
    }

    /// <summary> Handles /start_game: only the room host can start the game. </summary>
    private static void OnStartGame(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

        if (!rooms.TryGetValue(client.CurrentRoom, out var room))
            return;

        if (room.HostId != client.Id)
        {
            var error = new OSCMessageOut("/error").AddString("Only host can start the game");
            Send(sender, error);
            return;
        }

        if (room.GameStarted)
            return;

        room.GameStarted = true;
        var startMsg = new OSCMessageOut("/game_started");
        BroadcastToRoom(room, startMsg);

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Game started in room '{room.Name}' by {client.Name}");
    }
    #endregion

    #region Console commands
    // ------------------------- CONSOLE COMMAND HANDLERS -------------------------
    /// <summary> Displays the list of available server console commands. </summary>
    private static void ShowHelp()
    {
        Console.WriteLine("\n=== Server Console Commands ===");
        Console.WriteLine("/help                 - Show this help");
        Console.WriteLine("/all rooms            - List all rooms");
        Console.WriteLine("/all players          - List all connected players");
        Console.WriteLine("/playerid <id>        - Show player info by ID");
        Console.WriteLine("/player <name>        - Show player info by name");
        Console.WriteLine("/broadcast <message>  - Send a message to all clients");
        Console.WriteLine("/shutdown             - Gracefully shut down the server");
        Console.WriteLine("===============================\n");
    }

    /// <summary> Lists all active rooms with their host and player count. </summary>
    private static void ListAllRooms()
    {
        if (rooms.Count == 0)
        {
            Console.WriteLine("No active rooms.");
            return;
        }

        Console.WriteLine($"=== Rooms ({rooms.Count}) ===");
        foreach (var room in rooms.Values)
        {
            string hostName = clients.TryGetValue(room.HostId, out var host) ? host.Name : "?";
            Console.WriteLine($"- {room.Name} | Host: {hostName} | Players: {room.PlayerIds.Count}/4 | GameStarted: {room.GameStarted}");
        }
    }

    /// <summary> Lists all connected players with their ID, name, room status, and endpoint. </summary>
    private static void ListAllPlayers()
    {
        if (clients.Count == 0)
        {
            Console.WriteLine("No connected players.");
            return;
        }

        Console.WriteLine($"=== Players ({clients.Count}) ===");
        foreach (var client in clients.Values)
        {
            string roomInfo = string.IsNullOrEmpty(client.CurrentRoom) ? "In lobby" : $"In room '{client.CurrentRoom}'";
            Console.WriteLine($"- ID {client.Id}: {client.Name} | {roomInfo} | Endpoint: {client.Endpoint}");
        }
    }

    /// <summary> Shows details of a player by their numeric ID. </summary>
    private static void ShowPlayerById(int id)
    {
        if (clients.TryGetValue(id, out var client))
        {
            string roomInfo = string.IsNullOrEmpty(client.CurrentRoom) ? "Not in a room" : $"In room '{client.CurrentRoom}'";
            Console.WriteLine($"Player ID {id}: {client.Name} | {roomInfo} | Endpoint: {client.Endpoint}");
        }
        else
        {
            Console.WriteLine($"Player with ID {id} not found.");
        }
    }

    /// <summary> Shows details of a player(s) by name (case‑insensitive). </summary>
    private static void ShowPlayerByName(string name)
    {
        var matches = clients.Values.Where(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
        {
            Console.WriteLine($"Player '{name}' not found.");
        }
        else
        {
            foreach (var client in matches)
            {
                string roomInfo = string.IsNullOrEmpty(client.CurrentRoom) ? "Not in a room" : $"In room '{client.CurrentRoom}'";
                Console.WriteLine($"ID {client.Id}: {client.Name} | {roomInfo} | Endpoint: {client.Endpoint}");
            }
        }
    }

    /// <summary> Sends a text message to all connected clients (OSC address /server_message). </summary>
    private static void BroadcastToAll(string message)
    {
        var msg = new OSCMessageOut("/server_message").AddString(message);
        foreach (var client in clients.Values)
        {
            Send(client.Endpoint, msg);
        }
        Console.WriteLine($"Broadcast message sent to {clients.Count} clients: \"{message}\"");
    }

    /// <summary> Gracefully shuts down the server, notifies clients, and exits. </summary>
    private static void ShutdownServer()
    {
        if (isShuttingDown) return;
        isShuttingDown = true;

        Console.WriteLine("Shutting down server...");
        BroadcastToAll("Server is shutting down.");

        var shutdownMsg = new OSCMessageOut("/shutdown").AddString("Server is shutting down");
        foreach (var client in clients.Values)
        {
            Send(client.Endpoint, shutdownMsg);
        }

        // Give clients a moment to receive (but don't block if they're gone)
        System.Threading.Thread.Sleep(200);

        try
        {
            udp?.Close();
        }
        catch (ObjectDisposedException) { /* already disposed */ }

        Environment.Exit(0);
    }
    #endregion
}
// ------------------------- DATA CLASSES -------------------------
/// <summary> Holds information about a connected client. </summary>
class ClientInfo
{
    public int Id;
    public string Name = null!;
    public IPEndPoint Endpoint = null!;
    public string? CurrentRoom;   // null if not in any room
}
/// <summary> Tracks rate limiting data for a single IP address. </summary>
class ClientRateInfo
{
    public DateTime LastRequestTime;
    public int RequestCountInCurrentSecond;
    public int BanCount;                  // how many times this IP has exceeded the limit
}

/// <summary> Represents a game room. </summary>
class Room
{
    public int Id;
    public string Name = null!;       // points needed to win
    public int HostId;                    // client ID of the room creator
    public List<int> PlayerIds = new();   // list of client IDs in the room
    public bool GameStarted;
}
[Serializable]
public class RoomEntryData
{
    public int ID;
    public string roomName;
    public string host;
    public int pointGoal;
    public int currParticipants;
    public RoomEntryData(int pId, string pRoomName, string pHostName, int pPointGoal, int pCurrParticipants)
    {
        ID = pId;
        roomName = pRoomName;
        host = pHostName;
        pointGoal = pPointGoal;
        currParticipants = pCurrParticipants;
    }
}
