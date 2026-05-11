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
    private static readonly Dictionary<string, RoomData> rooms = new();
    private static readonly Dictionary<IPEndPoint, int> endpointToId = new();
    private static readonly Dictionary<int, RoomData> clientInRoomMap = new();

    // ------------------------- RATE LIMITING & SECURITY -------------------------
    private static readonly Dictionary<IPAddress, ClientRateInfo> rateLimits = new();
    private static readonly HashSet<IPAddress> bannedIPs = new();

    private const int MAX_REQUESTS_PER_SECOND = 50;
    private const int BAN_THRESHOLD = 5;
    private const int BAN_DURATION_SECONDS = 300;     // 5 minutes

    private const int MAX_USERNAME_LENGTH = 12;
    private const int MAX_ROOM_NAME_LENGTH = 20;
    private const int MAX_MESSAGE_STRING_LENGTH = 150; // For future use

    // Dice constants
    private const int DICE_HUMAN = 0;
    private const int DICE_COW = 1;
    private const int DICE_CHICKEN = 2;
    private const int DICE_TANK = 3;
    private const int DICE_UFO = 4;

    // ------------------------- SHUTTING DOWN -------------------------
    private static bool isShuttingDown = false;
    #endregion
    // ------------------------- MAIN ENTRY POINT -------------------------
    #region void Main
    /// <summary>
    /// Entry point of the server. Initializes UDP, OSC dispatcher, registers handlers,
    /// starts listening for packets, and enters the console command loop.
    /// </summary>
    static void Main()
    {
        udp = new UdpClient(55000);
        dispatcher = new OSCDispatcher();
        RegisterHandlers();
        Console.WriteLine("OSC Server running on port 55000");
        udp.BeginReceive(OnReceive, null);

        //Server Disconect handeling
        Console.CancelKeyPress += (sender, e) => { e.Cancel = true; ShutdownServer(); };
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => ShutdownServer(immediate: true);

        //console comand loop
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
            ConsoleCommands(parts[0].ToLower(), parts);
        }
    }

    /// <summary>
    /// Registers all OSC message handlers with the dispatcher.
    /// Each handler is associated with an OSC address pattern and expected argument types.
    /// </summary>
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
        dispatcher.AddListener("/stake_roll", OnStakeRollAnswer, OSCUtil.BOOL);
        dispatcher.AddListener("/select_die", OnSelectedDie, OSCUtil.INT);
    }

    #endregion

    // ------------------------- CONSOLE FUNC -------------------------
    #region Console
    /// <summary>
    /// Routes console commands entered by the server administrator.
    /// </summary>
    /// <param name="cmd">The command string (e.g., "/help").</param>
    /// <param name="parts">The command split into parts (including the command itself).</param>
    private static void ConsoleCommands(string cmd, string[] parts)
    {
        switch (cmd)
        {
            case "/help": ShowHelp(); break;
            case "/all":
                if (parts.Length > 1 && parts[1].ToLower() == "rooms") ListAllRooms();
                else if (parts.Length > 1 && parts[1].ToLower() == "players") ListAllPlayers();
                else Console.WriteLine("Usage: /all rooms | /all players");
                break;
            case "/playerid": if (parts.Length > 1 && int.TryParse(parts[1], out int id)) ShowPlayerById(id); break;
            case "/player": if (parts.Length > 1) ShowPlayerByName(parts[1]); break;
            case "/shutdown": ShutdownServer(); return;
            case "/broadcast":
                if (parts.Length > 1) BroadcastToAll(string.Join(" ", parts, 1, parts.Length - 1));
                break;
            case "/send": HandleSendCommand(parts); break;
            case "/dods": HandleDodsCommand(parts); break;
            case "/ban": HandleBanCommand(parts); break;
            default: Console.WriteLine("Unknown command. Type /help"); break;
        }
    }

    // normal commands
    #region Console commands

    /// <summary>
    /// Handles the /send console command, which manually injects an OSC message
    /// as if it came from a fake local endpoint.
    /// </summary>
    /// <param name="parts">Command parts: /send <address> [param1] ...</param>
    private static void HandleSendCommand(string[] parts)
    {
        if (parts.Length < 2) { Console.WriteLine("Usage: /send <osc_address> [param1] ..."); return; }
        string address = parts[1];
        if (!address.StartsWith("/")) { Console.WriteLine("Invalid OSC address"); return; }
        try
        {
            var msgOut = new OSCMessageOut(address);
            for (int i = 2; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int intVal)) msgOut.AddInt(intVal);
                else msgOut.AddString(parts[i]);
            }
            byte[] data = msgOut.GetBytes();
            var fakeSender = new IPEndPoint(IPAddress.Loopback, new Random().Next(10000, 60000));
            dispatcher.HandlePacket(data, fakeSender);
            Console.WriteLine($"[CONSOLE] Sent synthetic: {address}");
        }
        catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
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
        Console.WriteLine("/dods <id|name>       - Force disconnect a user");
        Console.WriteLine("/ban <IP>             - Ban an IP address");
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
            Console.WriteLine($"- {room.roomName} | Host: {room.host} | Players: {room.Participants.Count}/4 | GameStarted: {room.GameStarted}");
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
            Console.WriteLine($"- ID {client.Id}: {client.Name} | Room: {client.CurrentRoom ?? "lobby"} | Endpoint: {client.Endpoint}");
    }

    /// <summary> Shows details of a player by their numeric ID. </summary>
    private static void ShowPlayerById(int id)
    {
        if (clients.TryGetValue(id, out var client))
            Console.WriteLine($"ID {id}: {client.Name} | Room: {client.CurrentRoom ?? "lobby"} | Endpoint: {client.Endpoint}");
        else 
            Console.WriteLine($"Player ID {id} not found.");
    }

    /// <summary> Shows details of a player(s) by name (case‑insensitive). </summary>
    private static void ShowPlayerByName(string name)
    {
        var matches = clients.Values.Where(client => client.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) 
            Console.WriteLine($"Player '{name}' not found.");
        else 
            foreach (var client in matches) 
                Console.WriteLine($"ID {client.Id}: {client.Name} | Room: {client.CurrentRoom ?? "lobby"}");
    }

    /// <summary> Gracefully shuts down the server, notifies clients, and exits. </summary>
    private static void ShutdownServer(bool immediate = false)
    {
        if (isShuttingDown) return;
        isShuttingDown = true;
        Console.WriteLine("Shutting down server...");
        BroadcastToAll("Server is shutting down.");
        var shutdownMsg = new OSCMessageOut("/shutdown").AddString("Server is shutting down");
        foreach (var client in clients.Values) 
            Send(client.Endpoint, shutdownMsg);
        System.Threading.Thread.Sleep(immediate ? 50 : 200);
        try { udp?.Close(); } 
        catch { }
        Environment.Exit(0);
    }

    private static void HandleBanCommand(string[] parts)
    {
        // TODO: Implement ban by IP logic.
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
    /// <param name="msg">The incoming OSC message.</param>
    /// <param name="maxLength">Maximum allowed length.</param>
    /// <param name="fieldName">Name of the field (for logging).</param>
    /// <returns>The validated string, or null if invalid.</returns>
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
    /// <param name="ar">Async result from BeginReceive.</param>
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
    /// <summary>
    /// Helper to get a stable integer ID from a room name (used for dictionary keys).
    /// </summary>
    /// <param name="roomName">Name of the room.</param>
    /// <returns>Hash code of the room name.</returns>
    private static int GetRoomId(string roomName) => roomName.GetHashCode();

    // ------------------------- OSC MESSAGE HANDLERS -------------------------
    #region OSC Messages
    /// <summary>
    /// Handles /register OSC message: assigns a new ID and replies with /registered.
    /// </summary>
    /// <param name="msg">Incoming OSC message containing username.</param>
    /// <param name="sender">UDP endpoint of the client.</param>
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

    /// <summary>
    /// Handles /disconnect OSC message: removes the client from the server.
    /// </summary>
    /// <param name="msg">Unused.</param>
    /// <param name="sender">Client endpoint.</param>
    private static void OnDisconnect(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client != null) RemoveClient(client);
    }

    /// <summary>
    /// Handles /create_room OSC message: creates a new room (client becomes host).
    /// </summary>
    /// <param name="msg">Message containing room name and point goal.</param>
    /// <param name="sender">Client endpoint.</param>
    private static void OnCreateRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null) 
        { 
            SendError(sender, "Not registered"); 
            return; 
        }

        if (!string.IsNullOrEmpty(client.CurrentRoom)) 
        { 
            SendError(sender, "Already in a room"); 
            return; 
        }

        string roomName = ReadCappedString(msg, MAX_ROOM_NAME_LENGTH, "room name");
        if (roomName == null) 
        {
            SendError(sender, $"Room name too long"); 
            return; 
        }

        int pointGoal = msg.ReadInt();
        if (rooms.ContainsKey(roomName)) 
        { 
            SendError(sender, "Room already exists");
            return; 
        }

        var roomData = new RoomData(roomName.GetHashCode(), roomName, client.Name, pointGoal, null);
        roomData.Participants.Add(new Participant(client.Id, client.Name, 0));
        rooms[roomName] = roomData;
        client.CurrentRoom = roomName;

        var success = new OSCMessageOut("/room_update");
        success.AddString(roomName);
        success.AddInt(1);
        success.AddInt(4);
        success.AddString(client.Name);
        success.AddInt(pointGoal);  
        success.AddBool(false);
        Send(sender, success);
        Console.WriteLine($"[ROOM] {client.Name} created room '{roomName}' (goal {pointGoal})");
    }

    /// <summary>
    /// Handles /join_room OSC message: adds a client to an existing room.
    /// </summary>
    /// <param name="msg">Message containing room name.</param>
    /// <param name="sender">Client endpoint.</param>
    private static void OnJoinRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null) 
        { 
            SendError(sender, "Not registered"); 
            return; 
        }

        if (!string.IsNullOrEmpty(client.CurrentRoom)) 
        { 
            SendError(sender, "Already in a room"); 
            return; 
        }

        string roomName = ReadCappedString(msg, MAX_ROOM_NAME_LENGTH, "room name");
        if (roomName == null) 
        { 
            SendError(sender, $"Room name too long"); 
            return; 
        }

        if (!rooms.TryGetValue(roomName, out var room)) 
        { 
            SendError(sender, "Room not found"); 
            return; 
        }

        if (room.GameStarted) 
        { 
            SendError(sender, "Game already started"); 
            return; 
        }

        if (room.Participants.Count >= 4) 
        { 
            SendError(sender, "Room full");
            return; 
        }

        room.Participants.Add(new Participant(client.Id, client.Name, 0));
        client.CurrentRoom = roomName;

        var success = new OSCMessageOut("/room_update");
        success.AddString(roomName);
        success.AddInt(1);
        success.AddInt(4);
        success.AddString(client.Name);
        success.AddInt(room.pointGoal);  
        success.AddBool(false);
        BroadcastToRoom(room, success);
        Console.WriteLine($"[ROOM] {client.Name} joined {roomName}");
    }

    /// <summary>
    /// Handles /leave_room OSC message: removes a client from their current room.
    /// </summary>
    /// <param name="msg">Unused.</param>
    /// <param name="sender">Client endpoint.</param>
    private static void OnLeaveRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

        if (rooms.TryGetValue(client.CurrentRoom, out var room))
        {
            var part = room.Participants.FirstOrDefault(participant => participant.id == client.Id);
            if (part != null) room.Participants.Remove(part);
            client.CurrentRoom = null;

            if (room.host == client.Name && room.Participants.Count > 0)
            {
                var newHost = room.Participants.First();
                room.host = newHost.clientName;
                Console.WriteLine($"[ROOM] New host: {room.host}");
            }

            if (room.Participants.Count == 0)
                rooms.Remove(room.roomName);
            else
            {
                var updateMsg = new OSCMessageOut("/room_update");
                updateMsg.AddString(room.roomName);
                updateMsg.AddInt(room.Participants.Count);
                updateMsg.AddInt(4);
                updateMsg.AddString(room.host);
                updateMsg.AddBool(room.GameStarted);
                BroadcastToRoom(room, updateMsg);
            }
            Console.WriteLine($"[ROOM] {client.Name} left room");
        }
    }

    /// <summary>
    /// Handles /start_game OSC message: only the room host can start the game.
    /// </summary>
    /// <param name="msg">Unused.</param>
    /// <param name="sender">Client endpoint.</param>
    private static void OnStartGame(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);

        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

        if (!rooms.TryGetValue(client.CurrentRoom, out var room)) return;

        if (room.host != client.Name) 
        { 
            SendError(sender, "Only host can start"); 
            return; 
        }

        if (room.GameStarted) 
        { 
            SendError(sender, "Game already started"); 
            return; 
        }

        room.GameStarted = true;
        room.data = new GameData();
        room.data.participantOrder = room.Participants.Select(participant => participant.id).ToList();
        room.data.currentPlayerIndex = 0;
        room.data.currentPoints = 0;
        room.data.currentDefense = 0;
        room.data.currentDanger = 0;
        room.data.diceToRoll = 13;

        // Send game state to all
        var stateMsg = new OSCMessageOut("/game_state");
        stateMsg.AddInt(room.data.currentPlayerIndex);
        stateMsg.AddInt(room.Participants.Count);
        foreach (var participant in room.Participants)
        {
            stateMsg.AddString(participant.clientName);
            stateMsg.AddInt(participant.currPoints);
        }
        BroadcastToRoom(room, stateMsg);

        var startMsg = new OSCMessageOut("/game_started");
        BroadcastToRoom(room, startMsg);
        Console.WriteLine($"[GAME] Started in '{room.roomName}' by {client.Name}");

        // Begin first turn
        StartTurn(room);
    }

    /// <summary>
    /// Starts a new turn in the given room: resets turn-specific data, informs players, and rolls dice.
    /// </summary>
    /// <param name="room">The room where the turn begins.</param>
    private static void StartTurn(RoomData room)
    {
        int playerId = room.data.participantOrder[room.data.currentPlayerIndex];
        var currentPlayer = room.Participants.First(p => p.id == playerId);
        var client = clients[playerId];

        room.data.currentPoints = 0;
        room.data.currentDefense = 0;
        room.data.currentDanger = 0;
        room.data.diceToRoll = 13;

        var turnMsg = new OSCMessageOut("/your_turn").AddString(currentPlayer.clientName);
        BroadcastToRoom(room, turnMsg);
        var youMsg = new OSCMessageOut("/your_turn").AddString("It's your turn!");
        Send(client.Endpoint, youMsg);

        RollDice(room);
    }

    /// <summary>
    /// Rolls dice for the current player in a room and broadcasts the results.
    /// </summary>
    /// <param name="room">The room where dice are rolled.</param>
    private static void RollDice(RoomData room)
    {
        int[] results = new int[room.data.diceToRoll];

        Random rng = new Random();

        for (int i = 0; i < results.Length; i++)
            results[i] = rng.Next(0, 5);

        room.data.currentRoll = results;

        var diceMsg = new OSCMessageOut("/dice_rolled");
        diceMsg.AddInt(results.Length);

        foreach (int val in results)
            diceMsg.AddInt(val);

        BroadcastToRoom(room, diceMsg);
    }

    /// <summary>
    /// Handles /list_rooms OSC message: sends a list of all available rooms to the client.
    /// </summary>
    /// <param name="msg">Unused.</param>
    /// <param name="sender">Client endpoint.</param>
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
            roomList.AddInt(room.Participants.Count);
            roomList.AddInt(0); // game started flag
        }
        Send(sender, roomList);
    }

    /// <summary>
    /// Handles /close_room OSC message: removes the room if the client is the host.
    /// </summary>
    /// <param name="msg">Unused.</param>
    /// <param name="sender">Client endpoint.</param>
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

    /// <summary>
    /// Handles /select_die OSC message: processes a die selection during a player's turn.
    /// </summary>
    /// <param name="msg">Message containing the index of the selected die.</param>
    /// <param name="sender">Client endpoint.</param>
    private static void OnSelectedDie(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
        if (!rooms.TryGetValue(client.CurrentRoom, out var room)) return;
        if (!room.GameStarted) return;

        int playerId = room.data.participantOrder[room.data.currentPlayerIndex];
        if (client.Id != playerId) { SendError(sender, "Not your turn"); return; }

        int dieIndex = msg.ReadInt();
        if (dieIndex < 0 || dieIndex >= room.data.currentRoll.Length) { SendError(sender, "Invalid die"); return; }

        int dieValue = room.data.currentRoll[dieIndex];
        var newRoll = room.data.currentRoll.ToList();
        newRoll.RemoveAt(dieIndex);
        room.data.currentRoll = newRoll.ToArray();
        room.data.diceToRoll = newRoll.Count;

        switch (dieValue)
        {
            case 0: room.data.currentPoints += 10; break;
            case 1: room.data.currentPoints += 5; break;
            case 2: room.data.currentPoints += 1; break;
            case 3: room.data.currentDanger++; break;
            case 4: room.data.currentDefense++; break;
        }

        if (room.data.diceToRoll == 0) { EndTurn(room); return; }

        if (room.data.currentDefense >= room.data.currentDanger)
        {
            var promptMsg = new OSCMessageOut("/stake_prompt").AddBool(true);
            Send(sender, promptMsg);
        }
        else
        {
            if (room.data.currentPoints > 0)
                Send(sender, new OSCMessageOut("/stake_prompt").AddBool(false).AddString("Cannot stake. Collect or risk bust?"));
            else
                EndTurn(room, busted: true);
        }
    }

    /// <summary>
    /// Handles /stake_roll OSC message: player's answer whether to stake or collect.
    /// </summary>
    /// <param name="msg">Message containing a boolean: true = stake, false = collect.</param>
    /// <param name="sender">Client endpoint.</param>
    private static void OnStakeRollAnswer(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
        if (!rooms.TryGetValue(client.CurrentRoom, out var room)) return;
        if (!room.GameStarted) return;

        int playerId = room.data.participantOrder[room.data.currentPlayerIndex];
        if (client.Id != playerId) { SendError(sender, "Not your turn"); return; }

        bool doStake = msg.ReadBool();
        if (doStake) RollDice(room);
        else EndTurn(room);
    }

    /// <summary>
    /// Ends the current player's turn, updates scores, checks win condition, and starts the next turn.
    /// </summary>
    /// <param name="room">The room where the turn ends.</param>
    /// <param name="busted">If true, the player gets no points (bust).</param>
    private static void EndTurn(RoomData room, bool busted = false)
    {
        int playerId = room.data.participantOrder[room.data.currentPlayerIndex];

        var player = room.Participants.First(p => p.id == playerId);

        if (!busted) 
            player.currPoints += room.data.currentPoints;
        else
        {
            var bustMsg = new OSCMessageOut("/game_announcement");
            bustMsg.AddString($"{player.clientName} busted!");
            BroadcastToRoom(room, bustMsg);
        }

        if (player.currPoints >= room.pointGoal)
        {
            var winMsg = new OSCMessageOut("/game_announcement");
            winMsg.AddString($"{player.clientName} wins!");
            BroadcastToRoom(room, winMsg);
            rooms.Remove(room.roomName);
            return;
        }

        room.data.currentPlayerIndex = (room.data.currentPlayerIndex + 1) % room.Participants.Count;
        var scoreMsg = new OSCMessageOut("/round_results");
        scoreMsg.AddString("Scores updated");
        BroadcastToRoom(room, scoreMsg);

        // Send updated game state
        var stateMsg = new OSCMessageOut("/game_state");
        stateMsg.AddInt(room.data.currentPlayerIndex);
        stateMsg.AddInt(room.Participants.Count);

        foreach (var participant in room.Participants)
        {
            stateMsg.AddString(participant.clientName);
            stateMsg.AddInt(participant.currPoints);
        }
        BroadcastToRoom(room, stateMsg);

        StartTurn(room);
    }
    #endregion

    #region Helper Methods (Send, Broadcast, Validation, Rate Limit)
    /// <summary>
    /// Sends an OSC message to a specific client.
    /// </summary>
    /// <param name="target">UDP endpoint of the target client.</param>
    /// <param name="msg">The OSC message to send.</param>
    private static void Send(IPEndPoint target, OSCMessageOut msg)
    {
        try { udp.Send(msg.GetBytes(), msg.GetBytes().Length, target); }
        catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// Sends an error message to a client.
    /// </summary>
    /// <param name="target">Client endpoint.</param>
    /// <param name="message">Error description.</param>
    private static void SendError(IPEndPoint target, string message)
    {
        var errorMsg = new OSCMessageOut("/error").AddString(message);
        Send(target, errorMsg);
        Console.WriteLine($"[ERROR] Sent to {target}: {message}");
    }

    /// <summary>
    /// Broadcasts a text message to all connected clients.
    /// </summary>
    /// <param name="message">The message text.</param>
    private static void BroadcastToAll(string message)
    {
        var msg = new OSCMessageOut("/server_message").AddString(message);
        foreach (var client in clients.Values)
            Send(client.Endpoint, msg);
        Console.WriteLine($"Broadcast to {clients.Count} clients: \"{message}\"");
    }

    /// <summary>
    /// Broadcasts an OSC message to all players in a room.
    /// </summary>
    /// <param name="room">The target room.</param>
    /// <param name="msg">The OSC message to broadcast.</param>
    private static void BroadcastToRoom(RoomData room, OSCMessageOut msg)
    {
        foreach (var client in clients.Values)
            if (client.CurrentRoom == room.roomName)
                Send(client.Endpoint, msg);
    }

    /// <summary>
    /// Finds a client by their UDP endpoint.
    /// </summary>
    /// <param name="endpoint">The client's IP endpoint.</param>
    /// <returns>The ClientInfo object, or null if not found.</returns>
    private static ClientInfo? GetClientByEndpoint(IPEndPoint endpoint)
    {
        return endpointToId.TryGetValue(endpoint, out int id) && clients.TryGetValue(id, out var client) ? client : null;
    }

    /// <summary>
    /// Removes a client from the server.
    /// Also removes them from any room they were in, updates room host if needed,
    /// and notifies remaining players.
    /// </summary>
    /// <param name="client">The client to remove.</param>
    private static void RemoveClient(ClientInfo client)
    {
        if (client == null) return;
        if (!string.IsNullOrEmpty(client.CurrentRoom) && rooms.TryGetValue(client.CurrentRoom, out var room))
        {
            var participant = room.Participants.FirstOrDefault(p => p.id == client.Id);

            if (participant != null) room.Participants.Remove(participant);

            if (room.Participants.Count == 0)
                rooms.Remove(room.roomName);
            else if (room.host == client.Name && room.Participants.Count > 0)
            {
                var newHost = room.Participants.First();
                room.host = newHost.clientName;
            }
        }
        endpointToId.Remove(client.Endpoint);
        clients.Remove(client.Id);
        Console.WriteLine($"[DISCONNECT] {client.Name} (ID {client.Id}) disconnected");
    }

    #endregion

    #region Main Game logic

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
public class RoomData
{
    public int ID;
    public string roomName;
    public string host;
    public int pointGoal;
    public List<Participant> Participants = new(); // CHANGED: was currParticipants (int)
    public bool GameStarted;
    public GameData data;

    // Keep your original constructor signature but adapt internally
    public RoomData(int pId, string pRoomName, string pHostName, int pPointGoal, object pCurrParticipants = null)
    {
        ID = pId;
        roomName = pRoomName;
        host = pHostName;
        pointGoal = pPointGoal;
        GameStarted = false;
        data = new GameData();
        // If pCurrParticipants is an int, ignore it; we use Participants list now
    }

    public bool AddParticipant(Participant pParticipant)
    {
        if (Participants.Contains(pParticipant)) return false;
        if (Participants.Count >= 4) return false;
        Participants.Add(pParticipant);
        return true;
    }

    // Helper property to keep old code that used currParticipants
    public int CurrParticipants => Participants.Count;
}

public class GameData
{
    public int id; // current round player
    public int diceToRoll; // start at 13
    public int currentPoints;
    public int currentDefense;
    public int currentDanger;
    public List<int> participantOrder = new(); // NEW: track turn order
    public int currentPlayerIndex;             // NEW
    public int[] currentRoll = Array.Empty<int>(); // NEW

    public GameData()
    {
        id = 0;
        diceToRoll = 13;
        currentPoints = 0;
        currentDefense = 0;
        currentDanger = 0;
    }
}

public class Participant
{
    public int id;
    public string clientName;
    public int currPoints;
    public Participant(int pID, string pName, int pCurrPoints = 0)
    {
        id = pID;
        clientName = pName;
        currPoints = pCurrPoints;
    }
}
#endregion