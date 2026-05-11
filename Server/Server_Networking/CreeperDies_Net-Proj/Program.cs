using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetworkConnections;   // Your TcpNetworkConnection class

class TCPServer
{
    #region variables
    private TcpListener listener = null!;
    private static List<TcpNetworkConnection> connections = new();
    private static OSCDispatcher dispatcher = null!;

    // Client data
    private static int nextId = 1;
    private static readonly Dictionary<int, ClientInfo> clients = new();
    private static readonly Dictionary<string, RoomData> rooms = new();
    private static readonly Dictionary<TcpNetworkConnection, int> connectionToId = new();
    private static readonly Dictionary<int, RoomData> clientInRoomMap = new();

    // Rate limiting & security
    private static readonly Dictionary<IPAddress, ClientRateInfo> rateLimits = new();
    private static readonly HashSet<IPAddress> bannedIPs = new();
    private const int MAX_REQUESTS_PER_SECOND = 50;
    private const int BAN_THRESHOLD = 5;
    private const int BAN_DURATION_SECONDS = 300;
    private const int MAX_USERNAME_LENGTH = 12;
    private const int MAX_ROOM_NAME_LENGTH = 20;
    private const int MAX_MESSAGE_STRING_LENGTH = 150;

    // Dice constants
    private const int DICE_HUMAN = 0;
    private const int DICE_COW = 1;
    private const int DICE_CHICKEN = 2;
    private const int DICE_TANK = 3;
    private const int DICE_UFO = 4;

    private static bool isShuttingDown = false;
    #endregion

    #region Main Entry Point
    static void Main()
    {
        var server = new TCPServer();
        server.Start(); // runs the main update loop

        // This bit is in constructor?
        int port = 55000;
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"TCP OSC Server running on port {port}");

        dispatcher = new OSCDispatcher();
        RegisterHandlers();



        // Run network loop in background (non‑blocking)
        // probably remove...?
        Task.Run(() => NetworkLoop());

        // Shutdown handling
        Console.CancelKeyPress += (sender, e) => { e.Cancel = true; ShutdownServer(); };
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => ShutdownServer(immediate: true);

        // Put this in the Start() method: / insert the polling stuff (acceptnewclients, checkforinput, cleanupconnections)

        // Console command loop
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

    #region Network Loop
    // if ur asking why and where di i get this
    //https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios#:~:text=Asynchronous%20code%20uses%20Task%3CT,keyword%20in%20the%20method%20body.
    private static async Task NetworkLoop()
    {
        while (!isShuttingDown)
        {
            AcceptNewConnections();
            UpdateConnections();
            CleanupConnections();
            await Task.Delay(15); // ~60 Hz, similar to Unity's Update
        }
    }

    private static void AcceptNewConnections()
    {
        if (listener.Pending())
        {
            TcpClient tcpClient = listener.AcceptTcpClient();
            var connection = new TcpNetworkConnection(tcpClient);
            if (connection.Status == ConnectionStatus.Connected)
            {
                connections.Add(connection);
                Console.WriteLine($"[NET] New connection from {connection.Remote}");
            }
            else
            {
                connection.Close();
                Console.WriteLine($"[NET] Rejected connection (not connected)");
            }
        }
    }

    private static void UpdateConnections()
    {
        foreach (var conn in connections.ToList())
        {
            try
            {
                while (conn.Available() > 0)
                {
                    byte[] packet = conn.GetPacket();
                    if (packet != null)
                    {
                        // Rate limiting uses IP address from connection.Remote
                        if (ShouldBlock(conn.Remote))
                            continue;

                        dispatcher.HandlePacket(packet, conn.Remote);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Reading from {conn.Remote}: {ex.Message}");
                // Connection will be removed in CleanupConnections
            }
        }
    }

    private static void CleanupConnections()
    {
        var dead = connections.Where(conn => conn.Status != ConnectionStatus.Connected).ToList();
        foreach (var conn in dead)
        {
            Console.WriteLine($"[NET] Removing dead connection {conn.Remote}");
            var client = GetClientByConnection(conn);
            if (client != null) RemoveClient(client);
            connections.Remove(conn);
            conn.Close();
        }
    }
    #endregion

    #region Console Commands (identical to original)
    private static void ConsoleCommands(string cmd, string[] parts)
    {
        // Maybe add a lock here?
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
            case "/ban": HandleBanCommand(parts); break;
            default: Console.WriteLine("Unknown command. Type /help"); break;
        }
    }

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
        Console.WriteLine("/ban <IP>             - Ban an IP address");
        Console.WriteLine("/shutdown             - Gracefully shut down the server");
        Console.WriteLine("===============================\n");
    }

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

    private static void ListAllPlayers()
    {
        if (clients.Count == 0) 
        { 
            Console.WriteLine("No connected players."); 
            return; 
        }
        Console.WriteLine($"=== Players ({clients.Count}) ===");
        foreach (var client in clients.Values)
            Console.WriteLine($"- ID {client.Id}: {client.Name} | Room: {client.CurrentRoom ?? "lobby"} | Endpoint: {client.Connection.Remote}");
    }

    private static void ShowPlayerById(int id)
    {
        if (clients.TryGetValue(id, out var client))
            Console.WriteLine($"ID {id}: {client.Name} | Room: {client.CurrentRoom ?? "lobby"} | Endpoint: {client.Connection.Remote}");
        else 
            Console.WriteLine($"Player ID {id} not found.");
    }

    private static void ShowPlayerByName(string name)
    {
        var matches = clients.Values.Where(client => client.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) 
            Console.WriteLine($"Player '{name}' not found.");
        else 
            foreach (var client in matches) 
                Console.WriteLine($"ID {client.Id}: {client.Name} | Room: {client.CurrentRoom ?? "lobby"}");
    }

    private static void ShutdownServer(bool immediate = false)
    {
        if (isShuttingDown) return;
        isShuttingDown = true;
        Console.WriteLine("Shutting down server...");
        BroadcastToAll("Server is shutting down.");
        var shutdownMsg = new OSCMessageOut("/shutdown").AddString("Server is shutting down");
        foreach (var client in clients.Values) Send(client.Connection, shutdownMsg);
        System.Threading.Thread.Sleep(immediate ? 50 : 200);
        foreach (var conn in connections) conn.Close();
        listener.Stop();
        Environment.Exit(0);
    }

    private static void HandleBanCommand(string[] parts) { /* TODO */ }
    #endregion

    #region Security (Rate limiting - unchanged)
    private static bool ShouldBlock(IPEndPoint endpoint)
    {
        var ip = endpoint.Address;
        if (bannedIPs.Contains(ip)) return true;

        if (!rateLimits.TryGetValue(ip, out var info))
        {
            info = new ClientRateInfo { LastRequestTime = DateTime.UtcNow, RequestCountInCurrentSecond = 0, BanCount = 0 };
            rateLimits[ip] = info;
        }

        var now = DateTime.UtcNow;
        if ((now - info.LastRequestTime).TotalSeconds >= 1)
        {
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
                _ = Task.Delay(BAN_DURATION_SECONDS * 1000).ContinueWith(_ => { bannedIPs.Remove(ip); Console.WriteLine($"Unbanned IP {ip}"); });
            }
            return true;
        }
        else
        {
            info.BanCount = 0;
            return false;
        }
    }

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

    #region OSC Message Handlers (logic unchanged, only transport adapted)
    private static void OnRegister(OSCMessageIn msg, IPEndPoint sender)
    {
        var conn = GetConnectionByEndpoint(sender);
        if (conn == null) return;

        string username = ReadCappedString(msg, MAX_USERNAME_LENGTH, "username");
        if (username == null)
        {
            SendError(conn, $"Username too long (max {MAX_USERNAME_LENGTH} chars)");
            return;
        }
        if (string.IsNullOrWhiteSpace(username))
        {
            SendError(conn, "Username cannot be empty");
            return;
        }

        int id = nextId++;
        var client = new ClientInfo
        {
            Id = id,
            Name = username,
            Connection = conn,
            CurrentRoom = null
        };
        clients[id] = client;
        connectionToId[conn] = id;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Registered {username} with ID {id}");

        var reply = new OSCMessageOut("/registered");
        reply.AddInt(id).AddString(username);
        Send(conn, reply);
    }

    private static void OnDisconnect(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client != null) RemoveClient(client);
    }

    private static void OnCreateRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null) { SendError(GetConnectionByEndpoint(sender), "Not registered"); return; }
        if (!string.IsNullOrEmpty(client.CurrentRoom)) { SendError(client.Connection, "Already in a room"); return; }

        string roomName = ReadCappedString(msg, MAX_ROOM_NAME_LENGTH, "room name");
        if (roomName == null) { SendError(client.Connection, "Room name too long"); return; }

        int pointGoal = msg.ReadInt();
        if (rooms.ContainsKey(roomName)) { SendError(client.Connection, "Room already exists"); return; }

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
        Send(client.Connection, success);
        Console.WriteLine($"[ROOM] {client.Name} created room '{roomName}' (goal {pointGoal})");
    }

    private static void OnJoinRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null) { SendError(GetConnectionByEndpoint(sender), "Not registered"); return; }
        if (!string.IsNullOrEmpty(client.CurrentRoom)) { SendError(client.Connection, "Already in a room"); return; }

        string roomName = ReadCappedString(msg, MAX_ROOM_NAME_LENGTH, "room name");
        if (roomName == null) { SendError(client.Connection, "Room name too long"); return; }

        if (!rooms.TryGetValue(roomName, out var room)) { SendError(client.Connection, "Room not found"); return; }
        if (room.GameStarted) { SendError(client.Connection, "Game already started"); return; }
        if (room.Participants.Count >= 4) { SendError(client.Connection, "Room full"); return; }

        room.Participants.Add(new Participant(client.Id, client.Name, 0));
        client.CurrentRoom = roomName;

        var success = new OSCMessageOut("/room_update");
        success.AddString(roomName);
        success.AddInt(room.Participants.Count);
        success.AddInt(4);
        success.AddString(client.Name);
        success.AddInt(room.pointGoal);
        success.AddBool(false);
        BroadcastToRoom(room, success);
        Console.WriteLine($"[ROOM] {client.Name} joined {roomName}");
    }

    private static void OnLeaveRoom(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

        if (rooms.TryGetValue(client.CurrentRoom, out var room))
        {
            var part = room.Participants.FirstOrDefault(p => p.id == client.Id);
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

    private static void OnStartGame(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
        if (!rooms.TryGetValue(client.CurrentRoom, out var room)) return;
        if (room.host != client.Name) { SendError(client.Connection, "Only host can start"); return; }
        if (room.GameStarted) { SendError(client.Connection, "Game already started"); return; }

        room.GameStarted = true;
        room.data = new GameData();
        room.data.participantOrder = room.Participants.Select(p => p.id).ToList();
        room.data.currentPlayerIndex = 0;
        room.data.currentPoints = 0;
        room.data.currentDefense = 0;
        room.data.currentDanger = 0;
        room.data.diceToRoll = 13;

        var stateMsg = new OSCMessageOut("/game_state");
        stateMsg.AddInt(room.data.currentPlayerIndex);
        stateMsg.AddInt(room.Participants.Count);
        foreach (var p in room.Participants)
        {
            stateMsg.AddString(p.clientName);
            stateMsg.AddInt(p.currPoints);
        }
        BroadcastToRoom(room, stateMsg);

        var startMsg = new OSCMessageOut("/game_started");
        BroadcastToRoom(room, startMsg);
        Console.WriteLine($"[GAME] Started in '{room.roomName}' by {client.Name}");
        StartTurn(room);
    }

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
        Send(client.Connection, youMsg);
        RollDice(room);
    }

    private static void RollDice(RoomData room)
    {
        int[] results = new int[room.data.diceToRoll];
        Random rng = new Random();
        for (int i = 0; i < results.Length; i++) results[i] = rng.Next(0, 5);
        room.data.currentRoll = results;

        var diceMsg = new OSCMessageOut("/dice_rolled");
        diceMsg.AddInt(results.Length);
        foreach (int val in results) diceMsg.AddInt(val);
        BroadcastToRoom(room, diceMsg);
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
            roomList.AddInt(room.Participants.Count);
            roomList.AddInt(0);
        }
        Send(client.Connection, roomList);
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

    private static void OnSelectedDie(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
        if (!rooms.TryGetValue(client.CurrentRoom, out var room)) return;
        if (!room.GameStarted) return;

        int playerId = room.data.participantOrder[room.data.currentPlayerIndex];
        if (client.Id != playerId) { SendError(client.Connection, "Not your turn"); return; }

        int dieIndex = msg.ReadInt();
        if (dieIndex < 0 || dieIndex >= room.data.currentRoll.Length) { SendError(client.Connection, "Invalid die"); return; }

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
            Send(client.Connection, promptMsg);
        }
        else
        {
            if (room.data.currentPoints > 0)
                Send(client.Connection, new OSCMessageOut("/stake_prompt").AddBool(false).AddString("Cannot stake. Collect or risk bust?"));
            else
                EndTurn(room, busted: true);
        }
    }

    private static void OnStakeRollAnswer(OSCMessageIn msg, IPEndPoint sender)
    {
        var client = GetClientByEndpoint(sender);
        if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
        if (!rooms.TryGetValue(client.CurrentRoom, out var room)) return;
        if (!room.GameStarted) return;

        int playerId = room.data.participantOrder[room.data.currentPlayerIndex];
        if (client.Id != playerId) { SendError(client.Connection, "Not your turn"); return; }

        bool doStake = msg.ReadBool();
        if (doStake) RollDice(room);
        else EndTurn(room);
    }

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

        var stateMsg = new OSCMessageOut("/game_state");
        stateMsg.AddInt(room.data.currentPlayerIndex);
        stateMsg.AddInt(room.Participants.Count);
        foreach (var p in room.Participants)
        {
            stateMsg.AddString(p.clientName);
            stateMsg.AddInt(p.currPoints);
        }
        BroadcastToRoom(room, stateMsg);
        StartTurn(room);
    }
    #endregion

    #region Helper Methods (Send, Broadcast, Lookups)
    private static void Send(TcpNetworkConnection conn, OSCMessageOut msg)
    {
        try { conn.Send(msg.GetBytes()); }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { Console.WriteLine($"[SEND ERROR] {ex.Message}"); }
    }

    private static void SendError(TcpNetworkConnection conn, string message)
    {
        var errorMsg = new OSCMessageOut("/error").AddString(message);
        Send(conn, errorMsg);
        Console.WriteLine($"[ERROR] Sent to {conn.Remote}: {message}");
    }

    private static void BroadcastToAll(string textMessage)
    {
        var msg = new OSCMessageOut("/server_message").AddString(textMessage);
        foreach (var client in clients.Values)
            Send(client.Connection, msg);
        Console.WriteLine($"Broadcast to {clients.Count} clients: \"{textMessage}\"");
    }

    private static void BroadcastToRoom(RoomData room, OSCMessageOut msg)
    {
        foreach (var client in clients.Values)
            if (client.CurrentRoom == room.roomName)
                Send(client.Connection, msg);
    }

    private static ClientInfo? GetClientByEndpoint(IPEndPoint endpoint)
    {
        var conn = GetConnectionByEndpoint(endpoint);
        if (conn == null) return null;
        return connectionToId.TryGetValue(conn, out int id) && clients.TryGetValue(id, out var client) ? client : null;
    }

    private static TcpNetworkConnection? GetConnectionByEndpoint(IPEndPoint endpoint)
    {
        return connections.FirstOrDefault(c => c.Remote != null && c.Remote.Equals(endpoint));
    }

    private static ClientInfo? GetClientByConnection(TcpNetworkConnection conn)
    {
        return connectionToId.TryGetValue(conn, out int id) && clients.TryGetValue(id, out var client) ? client : null;
    }

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
        connectionToId.Remove(client.Connection);
        clients.Remove(client.Id);
        Console.WriteLine($"[DISCONNECT] {client.Name} (ID {client.Id}) disconnected");
    }
    #endregion

    #region Data Classes (unchanged)
    class ClientInfo
    {
        public int Id;
        public string Name = null!;
        public TcpNetworkConnection Connection = null!;
        public string? CurrentRoom;
    }

    class ClientRateInfo
    {
        public DateTime LastRequestTime;
        public int RequestCountInCurrentSecond;
        public int BanCount;
    }

    [Serializable]
    public class RoomData
    {
        public int ID;
        public string roomName;
        public string host;
        public int pointGoal;
        public List<Participant> Participants = new();
        public bool GameStarted;
        public GameData data;

        public RoomData(int pId, string pRoomName, string pHostName, int pPointGoal, object pCurrParticipants = null)
        {
            ID = pId;
            roomName = pRoomName;
            host = pHostName;
            pointGoal = pPointGoal;
            GameStarted = false;
            data = new GameData();
        }

        public bool AddParticipant(Participant pParticipant)
        {
            if (Participants.Contains(pParticipant)) return false;
            if (Participants.Count >= 4) return false;
            Participants.Add(pParticipant);
            return true;
        }

        public int CurrParticipants => Participants.Count;
    }

    public class GameData
    {
        public int id;
        public int diceToRoll;
        public int currentPoints;
        public int currentDefense;
        public int currentDanger;
        public List<int> participantOrder = new();
        public int currentPlayerIndex;
        public int[] currentRoll = Array.Empty<int>();

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
}