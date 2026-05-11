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
    #region variables
    // ------------------------- NETWORK & DISPATCHER -------------------------
    private static UdpClient udp = null!;
    private static OSCDispatcher dispatcher = null!;


    // ------------------------- CLIENT & ROOM DATA -------------------------
    private static int nextId = 1;
    private static readonly Dictionary<int, ClientInfo> clients = new();
    private static readonly Dictionary<string, RoomEntryData> rooms = new();
    private static readonly Dictionary<IPEndPoint, int> endpointToId = new();
    private static readonly Dictionary<int, RoomEntryData> clientInRoomMap = new();

    // ------------------------- RATE LIMITING & SECURITY -------------------------
    private static readonly Dictionary<IPAddress, ClientRateInfo> rateLimits = new();
    private static readonly HashSet<IPAddress> bannedIPs = new();

    private const int MAX_REQUESTS_PER_SECOND = 50;
    private const int BAN_THRESHOLD = 5;
    private const int BAN_DURATION_SECONDS = 300;     // 5 minutes

    private const int MAX_USERNAME_LENGTH = 12;
    private const int MAX_ROOM_NAME_LENGTH = 20;
    private const int MAX_MESSAGE_STRING_LENGTH = 150; // For future use

    // ------------------------- SHUTTING DOWN -------------------------
    private static bool isShuttingDown = false;
    #endregion
    // ------------------------- MAIN ENTRY POINT -------------------------
    #region void Main
    static void Main()
    {
        udp = new UdpClient(55000);
        dispatcher = new OSCDispatcher();

        // Register OSC message handlers – these are called when a client sends the matching address.
        RegisterHandlers();

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
            ConsoleCommands(cmd, parts);
        }
    }

    private static void RegisterHandlers()
    {
        dispatcher.AddListener("/register", OnRegister, OSCUtil.STRING);
        dispatcher.AddListener("/disconnect", OnDisconnect);
        dispatcher.AddListener("/create_room", OnCreateRoom, OSCUtil.STRING, OSCUtil.INT);
        dispatcher.AddListener("/join_room", OnJoinRoom, OSCUtil.STRING);
        dispatcher.AddListener("/leave_room", OnLeaveRoom);
        dispatcher.AddListener("/start_game", OnStartGame);
        dispatcher.AddListener("/list_rooms", OnListRooms);
        dispatcher.AddListener("/close_room", OnCloseRoom);
    }

    #endregion

    // ------------------------- CONSOLE FUNC -------------------------
    #region Console
    private static void ConsoleCommands(string cmd, string[] parts)
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
                return;
            case "/broadcast":
                if (parts.Length < 2)
                    Console.WriteLine("Usage: /broadcast <message>");
                else
                {
                    string message = string.Join(" ", parts, 1, parts.Length - 1);
                    BroadcastToAll(message);
                }
                break;
            case "/send":
                HandleSendCommand(parts);
                break;
            case "/dods":
                HandleDodsCommand(parts);
                break;
            case "/ban":
                if (parts.Length < 2)
                    Console.WriteLine("Usage: /ban <IP>");
                else if (IPAddress.TryParse(parts[1], out var ipToBan))
                {
                    bannedIPs.Add(ipToBan);
                    // Kick any client with that IP
                    var clientsToKick = clients.Values.Where(c => c.Endpoint.Address.Equals(ipToBan)).ToList();
                    foreach (var c in clientsToKick)
                        RemoveClient(c);
                    Console.WriteLine($"Banned IP {ipToBan} and kicked {clientsToKick.Count} client(s).");
                    // Auto-unban after duration
                    _ = Task.Delay(BAN_DURATION_SECONDS * 1000).ContinueWith(_ =>
                    {
                        bannedIPs.Remove(ipToBan);
                        Console.WriteLine($"Auto-unbanned IP {ipToBan}");
                    });
                }
                else Console.WriteLine("Invalid IP address.");
                break;
            default:
                Console.WriteLine("Unknown command. Type /help");
                break;
        }
    }

    // normal commands
    #region Console commands
    private static void HandleSendCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: /send <osc_address> [param1] [param2] ...");
            return;
        }

        string address = parts[1];
        if (!address.StartsWith("/"))
        {
            Console.WriteLine($"Invalid OSC address: '{address}' – must start with '/'");
            return;
        }

        try
        {
            var msgOut = new OSCMessageOut(address);
            for (int i = 2; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int intVal))
                    msgOut.AddInt(intVal);
                else
                    msgOut.AddString(parts[i]);
            }

            byte[] data = msgOut.GetBytes();
            var fakeSender = new IPEndPoint(IPAddress.Loopback, new Random().Next(10000, 60000));

            Console.WriteLine($"[CONSOLE] Sending synthetic OSC: {address} with {parts.Length - 2} params");
            dispatcher.HandlePacket(data, fakeSender);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to send synthetic OSC: {ex.Message}");
        }
    }
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
        Console.WriteLine("/send <addr> [params] - Manually trigger an OSC message");
        Console.WriteLine("/dods <id|name>       - Force disconnect a user (Denial Of Service)");
        Console.WriteLine("/shutdown             - Gracefully shut down the server");
        Console.WriteLine("===============================\n");
    }

    private static void HandleDodsCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: /dods <id|name>");
            return;
        }

        string target = parts[1];
        ClientInfo? client = null;

        // Try as ID
        if (int.TryParse(target, out int id))
        {
            clients.TryGetValue(id, out client);
        }
        // Otherwise as name (case-insensitive)
        if (client == null)
        {
            client = clients.Values.FirstOrDefault(c => c.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
        }

        if (client == null)
        {
            Console.WriteLine($"No client found with ID or name '{target}'");
            return;
        }

        Console.WriteLine($"[DODS] Force disconnecting {client.Name} (ID {client.Id})");
        RemoveClient(client);
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
            //string hostName = clients.TryGetValue(room.HostId, out var host) ? host.Name : "?";
            //Console.WriteLine($"- {room.Name} | Host: {hostName} | Players: {room.PlayerIds.Count}/4 | GameStarted: {room.GameStarted}");
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

        try { udp?.Close(); }
        catch (ObjectDisposedException) { /* already disposed */ }

        Environment.Exit(0);
    }
    #endregion

    //DebugCommands
    #endregion

    #region Security
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
        byte[] data;
        try
        {
            data = udp.EndReceive(ar, ref sender);
        }
        catch (ObjectDisposedException)
        {
            // Socket closed during shutdown – ignore
            return;
        }
        catch (SocketException ex)
        {
            // This happens when a client disconnects uncleanly (e.g., game closed)
            Console.WriteLine($"[SOCKET] Receive error (client disconnected): {ex.Message}");
            // Continue listening if server is still running
            if (!isShuttingDown)
            {
                try { udp.BeginReceive(OnReceive, null); } catch { }
            }
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Unexpected receive error: {ex.Message}");
            if (!isShuttingDown)
            {
                try { udp.BeginReceive(OnReceive, null); } catch { }
            }
            return;
        }

        if (ShouldBlock(sender))
        {
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
        //var roomData = new RoomEntryData(
        //    GetRoomId(room.Name), // generate a deterministic ID (e.g., hash)
        //    room.Name,
        //    clients[room.HostId].Name,
        //    room.PointGoal,
        //    room.PlayerIds.Count
        //);
        //string json = JsonUtility.ToJson(roomData);
        //var msg = new OSCMessageOut("/room_list_update");
        //msg.AddString(operation);
        //msg.AddString(json);
        //foreach (var client in clients.Values)
        //    Send(client.Endpoint, msg);
    }
    // Helper: simple ID generator (just for serialization)
    private static int GetRoomId(string roomName) => roomName.GetHashCode();

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
        if (client != null) RemoveClient(client);
    }

    /// <summary> Handles /create_room: creates a new room (client becomes host). </summary>
    private static void OnCreateRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null) { SendError(sender, "Not registered"); return; }

        if (!string.IsNullOrEmpty(client.CurrentRoom))
        {
            SendError(sender, "You are already in a room. Leave it first.");
            return;
        }

        string roomName = ReadCappedString(msg, MAX_ROOM_NAME_LENGTH, "room name");
        if (roomName == null) { SendError(sender, $"Room name too long (max {MAX_ROOM_NAME_LENGTH})"); return; }

        int pointGoal = msg.ReadInt();

        if (rooms.ContainsKey(roomName)) { SendError(sender, "Room already exists"); return; }

        var roomData = new RoomEntryData(roomName.GetHashCode(), roomName, client.Name, pointGoal, 1);
        rooms[roomName] = roomData;
        client.CurrentRoom = roomName;

        var success = new OSCMessageOut("/room_update")
            .AddString(roomName).AddInt(1).AddInt(4).AddString(client.Name).AddBool(false);
        Send(sender, success);
        Console.WriteLine($"[ROOM] {client.Name} created room '{roomName}' (goal {pointGoal})");
    }

    /// <summary> Handles /join_room: adds a client to an existing room. </summary>
    private static void OnJoinRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null) { SendError(sender, "Not registered"); return; }

        if (!string.IsNullOrEmpty(client.CurrentRoom))
        {
            SendError(sender, "You are already in a room. Leave it first.");
            return;
        }

        string roomName = ReadCappedString(msg, MAX_ROOM_NAME_LENGTH, "room name");
        if (roomName == null) { SendError(sender, $"Room name too long (max {MAX_ROOM_NAME_LENGTH})"); return; }

        if (!rooms.TryGetValue(roomName, out var room)) { SendError(sender, "Room not found"); return; }

        if (room.currParticipants >= 4)
        {
            SendError(sender, "Room is full");
            return;
        }

        client.CurrentRoom = roomName;
        room.currParticipants++;

        var updateMsg = new OSCMessageOut("/room_update")
            .AddString(roomName).AddInt(room.currParticipants).AddInt(4).AddString(room.host).AddBool(false);
        BroadcastToRoom(room, updateMsg);
        Console.WriteLine($"[ROOM] {client.Name} joined {roomName}");
    }

    /// <summary> Handles /leave_room: removes a client from their current room. </summary>
    private static void OnLeaveRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

        if (rooms.TryGetValue(client.CurrentRoom, out var room))
        {
            room.currParticipants--;
            client.CurrentRoom = null;

            if (room.host == client.Name && room.currParticipants > 0)
            {
                var newHostClient = clients.Values.FirstOrDefault(c => c.CurrentRoom == room.roomName);
                if (newHostClient != null)
                {
                    room.host = newHostClient.Name;
                    Console.WriteLine($"[ROOM] New host for '{room.roomName}' is {room.host}");
                }
            }

            if (room.currParticipants <= 0)
            {
                rooms.Remove(room.roomName);
                Console.WriteLine($"[ROOM] Room '{room.roomName}' deleted (empty)");
            }
            else
            {
                var updateMsg = new OSCMessageOut("/room_update")
                    .AddString(room.roomName).AddInt(room.currParticipants).AddInt(4).AddString(room.host).AddBool(false);
                BroadcastToRoom(room, updateMsg);
            }
            Console.WriteLine($"[ROOM] {client.Name} left room");
        }
    }

    /// <summary> Handles /start_game: only the room host can start the game. </summary>
    private static void OnStartGame(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

        if (!rooms.TryGetValue(client.CurrentRoom, out var room)) return;

        if (room.host != client.Name)
        {
            SendError(sender, "Only host can start the game");
            return;
        }

        if (room.GameStarted)
        {
            SendError(sender, "Game already started");
            return;
        }

        room.GameStarted = true;
        var startMsg = new OSCMessageOut("/game_started");
        BroadcastToRoom(room, startMsg);
        Console.WriteLine($"[GAME] Game started in room '{room.roomName}' by {client.Name}");
    }

    private static void OnListRooms(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null) return;

        var roomList = new OSCMessageOut("/room_list");
        roomList.AddInt(rooms.Count);
        foreach (var room in rooms.Values)
        {
            roomList.AddString(room.roomName);
            roomList.AddInt(room.pointGoal);
            roomList.AddString(room.host);
            roomList.AddInt(room.currParticipants);
            roomList.AddInt(0); // game started flag
        }
        Send(sender, roomList);
    }

    private static void OnCloseRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

        if (rooms.Remove(client.CurrentRoom))
        {
            client.CurrentRoom = null;
            Console.WriteLine($"[ROOM] {client.Name} closed room");
        }
    }
    #endregion

    #region Helper Methods (Send, Broadcast, Validation, Rate Limit)
    /// <summary> Sends an OSC message to a specific client. </summary>
    private static void Send(IPEndPoint target, OSCMessageOut msg)
    {
        try { udp.Send(msg.GetBytes(), msg.GetBytes().Length, target); }
        catch (ObjectDisposedException) { }
    }

    private static void SendError(IPEndPoint target, string message)
    {
        var errorMsg = new OSCMessageOut("/error").AddString(message);
        Send(target, errorMsg);
        Console.WriteLine($"[ERROR] Sent to {target}: {message}");
    }
    private static void BroadcastToAll(string message)
    {
        var msg = new OSCMessageOut("/server_message").AddString(message);
        foreach (var client in clients.Values)
            Send(client.Endpoint, msg);
        Console.WriteLine($"Broadcast to {clients.Count} clients: \"{message}\"");
    }
    /// <summary> Broadcasts an OSC message to all players in a room, optionally excluding one. </summary>
    private static void BroadcastToRoom(RoomEntryData room, OSCMessageOut msg)
    {
        foreach (var client in clients.Values)
            if (client.CurrentRoom == room.roomName)
                Send(client.Endpoint, msg);
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
        if (!string.IsNullOrEmpty(client.CurrentRoom) && rooms.TryGetValue(client.CurrentRoom, out var room))
        {
            room.currParticipants--;
            if (room.currParticipants <= 0)
                rooms.Remove(room.roomName);
            else if (room.host == client.Name && room.currParticipants > 0)
            {
                // assign new host (first client in room)
                var newHost = clients.Values.FirstOrDefault(c => c.CurrentRoom == room.roomName);
                if (newHost != null) room.host = newHost.Name;
            }
        }
        endpointToId.Remove(client.Endpoint);
        clients.Remove(client.Id);
        Console.WriteLine($"[DISCONNECT] {client.Name} (ID {client.Id}) disconnected");
    }

    #endregion
}
// ------------------------- DATA CLASSES -------------------------
#region Data Classes
/// <summary> Holds information about a connected client. </summary>
class ClientInfo
{
    public int Id;
    public string Name = null!;
    public IPEndPoint Endpoint = null!;
    public string? CurrentRoom;
}
/// <summary> Tracks rate limiting data for a single IP address. </summary>
class ClientRateInfo
{
    public DateTime LastRequestTime;
    public int RequestCountInCurrentSecond;
    public int BanCount;
}

/// <summary> Represents a game room. </summary>
[Serializable]
public class RoomEntryData
{
    public int ID;
    public string roomName;
    public string host;
    public int pointGoal;
    public int currParticipants;
    public bool GameStarted;
    public RoomEntryData(int pId, string pRoomName, string pHostName, int pPointGoal, int pCurrParticipants)
    {
        ID = pId; 
        roomName = pRoomName; 
        host = pHostName; 
        pointGoal = pPointGoal; 
        currParticipants = pCurrParticipants;
        GameStarted = false;
    }
}
#endregion