using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace CreeperDice_Net_Proj.Model
{
    /*
     * ConsoleCommandHandler
     *
     * Purpose:
     * This class runs server console commands on a separate background thread.
     *
     * It is used for:
     * - Listing players and rooms.
     * - Selecting a player.
     * - Kicking players.
     * - Sending server messages.
     * - Creating, joining, closing, and starting rooms from the console.
     * - Testing game actions from the console.
     *
     * Naming rule:
     * This class does not directly receive OSC from clients.
     * It also does not use the On prefix.
     *
     * Some commands create synthetic OSC packets and pass them into the server dispatcher.
     * This is used to simulate what a selected client would send.
     */
    public class ConsoleCommandHandler
    {
        #region Fields

        /*
         * _server:
         * Reference to the running TcpServer.
         *
         * Used to:
         * - Access players.
         * - Access rooms.
         * - Send messages.
         * - Dispatch test OSC packets.
         */
        private readonly TcpServer _server;

        /*
         * _thread:
         * Background thread that reads console input.
         *
         * This lets the server continue updating while the console waits for commands.
         */
        private Thread _thread;

        /*
         * _running:
         * Controls if the console command loop should keep running.
         *
         * volatile:
         * Makes sure changes are visible between threads.
         */
        private volatile bool _running = true;

        #endregion

        #region Constructor

        /*
         * Constructor.
         *
         * Data received:
         * server = the active TcpServer.
         */
        public ConsoleCommandHandler(TcpServer server)
        {
            _server = server;
        }

        #endregion

        #region Thread Control

        /*
         * What this does:
         * Starts the console command thread.
         */
        public void Start()
        {
            _thread = new Thread(Run);
            _thread.IsBackground = true;
            _thread.Start();

            Console.WriteLine("Commands start with '/'. Type /help");
        }

        /*
         * What this does:
         * Stops the console command thread.
         */
        public void Stop()
        {
            _running = false;
            _thread?.Join(1000);
        }

        /*
         * What this does:
         * Main console input loop.
         *
         * Flow:
         * 1. Read a line from the console.
         * 2. Ignore empty input.
         * 3. Require commands to start with /.
         * 4. Split the command into parts.
         * 5. Execute the command.
         */
        private void Run()
        {
            while (_running)
            {
                string input = Console.ReadLine();

                if (string.IsNullOrEmpty(input))
                    continue;

                if (!input.StartsWith("/"))
                {
                    Console.WriteLine("Commands start with '/'. Type /help");
                    continue;
                }

                string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                ExecuteCommand(parts[0].ToLower(), parts);
            }
        }

        #endregion

        #region Command Execution

        /*
         * What this does:
         * Routes a typed command to the correct command function.
         *
         * cmd:
         * The first word of the command, lower case.
         *
         * parts:
         * All command parts split by spaces.
         */
        private void ExecuteCommand(string cmd, string[] parts)
        {
            lock (_server.SyncRoot)
            {
                switch (cmd)
                {
                    case "/help":
                        ShowHelp();
                        break;

                    #region Client Commands

                    case "/kickuser":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int kickId))
                            Console.WriteLine(_server.KickUser(kickId) ? "User kicked." : "User not found.");
                        else
                            Console.WriteLine("Usage: /kickUser <id>");
                        break;

                    case "/selectuser":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int selId))
                            Console.WriteLine(_server.SelectUser(selId) ? "User selected." : "User not found.");
                        else
                            Console.WriteLine("Usage: /selectUser <id>");
                        break;

                    case "/finduser":
                        if (parts.Length > 1)
                        {
                            var found = _server.FindPlayerByName(parts[1]);
                            Console.WriteLine(found != null ? $"Found: ID {found.Id} Name {found.Name}" : "User not found.");
                        }
                        else
                        {
                            Console.WriteLine("Usage: /findUser <name>");
                        }
                        break;

                    case "/finduserid":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int findId))
                        {
                            var found = _server.FindPlayerById(findId);
                            Console.WriteLine(found != null ? $"Found: ID {found.Id} Name {found.Name}" : "User not found.");
                        }
                        else
                        {
                            Console.WriteLine("Usage: /findUserID <id>");
                        }
                        break;

                    case "/allusers":
                        Console.WriteLine(_server.GetAllPlayersInfo());
                        break;

                    case "/createuser":
                        if (parts.Length > 1)
                            Console.WriteLine(_server.CreateFakeUser(parts[1]) ? "Fake user created." : "Creation failed.");
                        else
                            Console.WriteLine("Usage: /createUser <name>");
                        break;

                    #endregion

                    #region Selected User Commands

                    case "/sendmsg":
                        if (_server.GetSelectedUser() == null)
                            Console.WriteLine("No user selected. Use /selectUser first.");
                        else if (parts.Length > 1)
                            Console.WriteLine(_server.SendPrivateMessage(_server.GetSelectedUser().Id, GetRemainingText(parts, 1)) ? "Message sent." : "Failed.");
                        else
                            Console.WriteLine("Usage: /sendMsg <message>");
                        break;

                    case "/changename":
                        if (_server.GetSelectedUser() == null)
                            Console.WriteLine("No user selected. Use /selectUser first.");
                        else if (parts.Length > 1)
                            Console.WriteLine(_server.ChangeUserName(_server.GetSelectedUser().Id, parts[1]) ? "Name changed." : "Invalid name.");
                        else
                            Console.WriteLine("Usage: /changeName <newName>");
                        break;

                    #endregion

                    #region Messaging Commands

                    case "/broadcast":
                        if (parts.Length > 1)
                        {
                            _server.BroadcastToAll(GetRemainingText(parts, 1));
                            Console.WriteLine("Broadcast sent.");
                        }
                        else
                        {
                            Console.WriteLine("Usage: /broadcast <message>");
                        }
                        break;

                    case "/broadcastroom":
                        if (parts.Length > 2)
                        {
                            string roomName = parts[1];
                            string message = GetRemainingText(parts, 2);

                            _server.BroadcastToRoom(roomName, message);
                            Console.WriteLine($"Broadcast to room '{roomName}'.");
                        }
                        else
                        {
                            Console.WriteLine("Usage: /broadcastRoom <roomName> <message>");
                        }
                        break;

                    case "/send":
                        SendManualOscCommand(parts);
                        break;

                    #endregion

                    #region Lobby Commands

                    case "/allrooms":
                        Console.WriteLine(_server.GetAllRoomsInfo());
                        break;

                    case "/findroom":
                        if (parts.Length > 1)
                            Console.WriteLine(_server.FindRoom(parts[1]) ? "Room exists." : "Room not found.");
                        else
                            Console.WriteLine("Usage: /findRoom <name>");
                        break;

                    case "/createroom":
                        if (parts.Length > 2 && int.TryParse(parts[2], out int points))
                        {
                            int hostId = _server.GetSelectedUser()?.Id ?? -1;

                            if (hostId == -1)
                                Console.WriteLine("No user selected as host. Use /selectUser first.");
                            else
                                Console.WriteLine(_server.CreateRoomViaConsole(parts[1], points, hostId) ? "Room created." : "Room already exists or invalid host.");
                        }
                        else
                        {
                            Console.WriteLine("Usage: /createRoom <name> <points> (must have a selected user as host)");
                        }
                        break;

                    case "/joinroom":
                        JoinRoomCommand(parts);
                        break;

                    case "/closeroom":
                        if (parts.Length > 1)
                            Console.WriteLine(_server.CloseRoom(parts[1]) ? "Room closed." : "Room not found.");
                        else
                            Console.WriteLine("Usage: /closeRoom <name>");
                        break;

                    case "/startroom":
                        if (parts.Length > 1)
                            Console.WriteLine(_server.StartRoom(parts[1]) ? "Room started." : "Room not found or already started.");
                        else
                            Console.WriteLine("Usage: /startRoom <name>");
                        break;

                    #endregion

                    #region Game Commands

                    case "/selectdice":
                        SelectDiceCommand(parts);
                        break;

                    case "/stakesroll":
                        StakeRollCommand(parts);
                        break;

                    case "/userturn":
                        ShowUserTurnCommand();
                        break;

                    case "/showallpoints":
                        ShowAllPointsCommand();
                        break;

                    case "/showpoints":
                        ShowPointsCommand();
                        break;

                    case "/rematch":
                        RematchCommand();
                        break;

                    case "/leavegame":
                        LeaveGameCommand();
                        break;

                    #endregion

                    case "/shutdown":
                        ShutdownServer();
                        break;

                    default:
                        Console.WriteLine("Unknown command. Type /help");
                        break;
                }
            }
        }

        #endregion

        #region Lobby Command Helpers

        /*
         * Console command:
         * /joinRoom <roomName>
         *
         * What this does:
         * Adds the selected user to a room from the console.
         *
         * This is direct console logic, not OSC.
         *
         * Why this was changed:
         * The old version used a fake endpoint.
         * That means the server could not identify the selected user.
         */
        private void JoinRoomCommand(string[] parts)
        {
            if (parts.Length <= 1)
            {
                Console.WriteLine("Usage: /joinRoom <roomName>");
                return;
            }

            ClientInfo selected = _server.GetSelectedUser();

            if (selected == null)
            {
                Console.WriteLine("No user selected. Use /selectUser first.");
                return;
            }

            if (!string.IsNullOrEmpty(selected.CurrentRoom))
            {
                Console.WriteLine("Selected user is already in a room.");
                return;
            }

            string roomName = parts[1];

            if (!_server.TryGetRoom(roomName, out RoomData room))
            {
                Console.WriteLine("Room not found.");
                return;
            }

            if (room.GameStarted)
            {
                Console.WriteLine("Room game already started.");
                return;
            }

            if (room.Participants.Count >= 4)
            {
                Console.WriteLine("Room is full.");
                return;
            }

            room.Participants.Add(new Participant(selected.Id, selected.Name, 0));
            _server.UpdateClientRoom(selected, room.roomName);

            SendJoinedRoomToClient(selected, room);
            SendRoomUpdateToAll(room);

            Console.WriteLine($"{selected.Name} joined room {room.roomName}.");
        }

        #endregion

        #region Game Command Helpers

        /*
         * Console command:
         * /selectDice <diceType>
         *
         * What this does:
         * Simulates the selected user sending Msg.C_SELECT_DICE.
         *
         * Dice types:
         * 0 = Human
         * 1 = Cow
         * 2 = Chicken
         * 3 = Tank
         * 4 = UFO
         */
        private void SelectDiceCommand(string[] parts)
        {
            if (parts.Length <= 1 || !int.TryParse(parts[1], out int diceType))
            {
                Console.WriteLine("Usage: /selectDice <diceType>");
                Console.WriteLine("Dice types: 0 Human, 1 Cow, 2 Chicken, 3 Tank, 4 UFO");
                return;
            }

            var msg = new OSCMessageOut(Msg.C_SELECT_DICE)
                .AddInt(diceType);

            DispatchAsSelectedUser(msg, $"select dice {diceType}");
        }

        /*
         * Console command:
         * /stakeRoll <Y/N>
         *
         * What this does:
         * Simulates the selected user answering the stake prompt.
         *
         * Y:
         * Continue rolling / double danger.
         *
         * N:
         * Bank points.
         */
        private void StakeRollCommand(string[] parts)
        {
            if (parts.Length <= 1 || !TryParseYesNo(parts[1], out bool doStakeRoll))
            {
                Console.WriteLine("Usage: /stakeRoll <Y/N>");
                Console.WriteLine("Y = double danger / continue rolling");
                Console.WriteLine("N = bank points");
                return;
            }

            var msg = new OSCMessageOut(Msg.C_STAKE_ANSWER)
                .AddBool(doStakeRoll);

            DispatchAsSelectedUser(msg, doStakeRoll ? "stake roll YES" : "stake roll NO");
        }

        /*
         * Console command:
         * /userTurn
         *
         * What this does:
         * Shows whose turn it is in the selected user's current room.
         */
        private void ShowUserTurnCommand()
        {
            if (!TryGetSelectedUserRoom(out RoomData room))
                return;

            if (room.data == null || room.data.ParticipantOrder == null || room.data.ParticipantOrder.Count == 0)
            {
                Console.WriteLine("Game data or turn order is missing.");
                return;
            }

            int currentPlayerId = room.data.ParticipantOrder[room.data.CurrentPlayerIndex];
            Participant currentPlayer = room.Participants.FirstOrDefault(p => p.id == currentPlayerId);

            if (currentPlayer == null)
            {
                Console.WriteLine($"Current player ID {currentPlayerId} is not in room participants.");
                return;
            }

            Console.WriteLine($"Current turn: {currentPlayer.clientName} (ID {currentPlayer.id})");
            Console.WriteLine($"Phase: {room.data.Phase}");
        }

        /*
         * Console command:
         * /showAllPoints
         *
         * What this does:
         * Shows all player scores in the selected user's current room.
         */
        private void ShowAllPointsCommand()
        {
            if (!TryGetSelectedUserRoom(out RoomData room))
                return;

            Console.WriteLine($"=== Points in room {room.roomName} ===");

            foreach (Participant participant in room.Participants)
                Console.WriteLine($"- {participant.clientName}: {participant.currPoints}/{room.pointGoal}");
        }

        /*
         * Console command:
         * /showPoints
         *
         * What this does:
         * Shows the selected user's score in their current room.
         */
        private void ShowPointsCommand()
        {
            ClientInfo selected = _server.GetSelectedUser();

            if (selected == null)
            {
                Console.WriteLine("No user selected. Use /selectUser first.");
                return;
            }

            if (!TryGetSelectedUserRoom(out RoomData room))
                return;

            Participant participant = room.Participants.FirstOrDefault(p => p.id == selected.Id);

            if (participant == null)
            {
                Console.WriteLine("Selected user is not in the room participant list.");
                return;
            }

            Console.WriteLine($"{participant.clientName}: {participant.currPoints}/{room.pointGoal}");
        }

        /*
         * Console command:
         * /rematch
         *
         * What this does:
         * Simulates the selected user sending Msg.C_REMATCH_REQUEST.
         */
        private void RematchCommand()
        {
            var msg = new OSCMessageOut(Msg.C_REMATCH_REQUEST);

            DispatchAsSelectedUser(msg, "rematch request");
        }

        /*
         * Console command:
         * /leaveGame
         *
         * What this does:
         * Simulates the selected user sending Msg.C_LEAVE_GAME.
         */
        private void LeaveGameCommand()
        {
            var msg = new OSCMessageOut(Msg.C_LEAVE_GAME);

            DispatchAsSelectedUser(msg, "leave game");
        }

        #endregion

        #region Manual OSC Command

        /*
         * Console command:
         * /send <osc_address> [param1] ...
         *
         * What this does:
         * Creates a synthetic OSC message and sends it into the server dispatcher.
         *
         * If a real selected user exists:
         * The message is sent as that selected user's endpoint.
         *
         * If no real selected user exists:
         * A fake loopback endpoint is used.
         *
         * Important:
         * Commands that require a registered client should use /selectUser first.
         */
        private void SendManualOscCommand(string[] parts)
        {
            if (parts.Length < 2)
            {
                Console.WriteLine("Usage: /send <osc_address> [param1] ...");
                return;
            }

            string address = parts[1];

            if (!address.StartsWith("/"))
            {
                Console.WriteLine("Invalid OSC address.");
                return;
            }

            try
            {
                var msgOut = new OSCMessageOut(address);

                for (int i = 2; i < parts.Length; i++)
                    AddAutoTypedValue(msgOut, parts[i]);

                IPEndPoint sender = GetSelectedUserEndpointOrFake();

                _server.Dispatcher.HandlePacket(msgOut.GetBytes(), sender);

                Console.WriteLine($"[CONSOLE] Sent synthetic OSC: {address}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        /*
         * What this does:
         * Adds command parameters to an OSC message.
         *
         * Supported auto types:
         * - int
         * - bool
         * - string
         */
        private void AddAutoTypedValue(OSCMessageOut msgOut, string value)
        {
            if (int.TryParse(value, out int intValue))
            {
                msgOut.AddInt(intValue);
                return;
            }

            if (bool.TryParse(value, out bool boolValue))
            {
                msgOut.AddBool(boolValue);
                return;
            }

            msgOut.AddString(value);
        }

        #endregion

        #region Sending OSC Messages

        /*
         * OSC SEND: Msg.S_JOINED
         *
         * Payload sent:
         * [0] string roomName
         * [1] int participantCount
         * [2] string hostName
         * [3] int pointGoal
         * [4] bool gameStarted
         *
         * Used by:
         * /joinRoom console command.
         */
        private void SendJoinedRoomToClient(ClientInfo client, RoomData room)
        {
            var msg = new OSCMessageOut(Msg.S_JOINED)
                .AddString(room.roomName)
                .AddInt(room.Participants.Count)
                .AddString(room.host)
                .AddInt(room.pointGoal)
                .AddBool(room.GameStarted);

            _server.SendToClient(client, msg);
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
         * Used by:
         * /joinRoom console command.
         */
        private void SendRoomUpdateToAll(RoomData room)
        {
            var msg = new OSCMessageOut(Msg.S_ROOM_UPDATE)
                .AddString(room.roomName)
                .AddInt(room.Participants.Count)
                .AddString(room.host)
                .AddInt(room.pointGoal)
                .AddBool(room.GameStarted);

            _server.SendToAll(msg);
        }

        #endregion

        #region Helpers

        /*
         * What this does:
         * Gets all text after a certain command part index.
         *
         * Example:
         * /broadcast hello there
         *
         * startIndex = 1
         * result = "hello there"
         */
        private string GetRemainingText(string[] parts, int startIndex)
        {
            return string.Join(" ", parts.Skip(startIndex));
        }

        /*
         * What this does:
         * Parses yes/no style input into a bool.
         *
         * Accepted true values:
         * y, yes, true, 1
         *
         * Accepted false values:
         * n, no, false, 0
         */
        private bool TryParseYesNo(string value, out bool result)
        {
            result = false;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string lower = value.ToLower();

            if (lower == "y" || lower == "yes" || lower == "true" || lower == "1")
            {
                result = true;
                return true;
            }

            if (lower == "n" || lower == "no" || lower == "false" || lower == "0")
            {
                result = false;
                return true;
            }

            return false;
        }

        /*
         * What this does:
         * Gets the selected user's current room.
         *
         * Returns:
         * true if a selected user and room were found.
         * false if not.
         */
        private bool TryGetSelectedUserRoom(out RoomData room)
        {
            room = null;

            ClientInfo selected = _server.GetSelectedUser();

            if (selected == null)
            {
                Console.WriteLine("No user selected. Use /selectUser first.");
                return false;
            }

            if (string.IsNullOrEmpty(selected.CurrentRoom))
            {
                Console.WriteLine("Selected user is not in a room.");
                return false;
            }

            if (!_server.TryGetRoom(selected.CurrentRoom, out room))
            {
                Console.WriteLine("Selected user's room was not found.");
                return false;
            }

            return true;
        }

        /*
         * What this does:
         * Sends a synthetic client OSC message as the selected user.
         *
         * Important:
         * This only works for real connected users.
         * Fake users do not have a real endpoint, so they cannot be used for dispatcher-based game commands.
         */
        private bool DispatchAsSelectedUser(OSCMessageOut msg, string actionName)
        {
            ClientInfo selected = _server.GetSelectedUser();

            if (selected == null)
            {
                Console.WriteLine("No user selected. Use /selectUser first.");
                return false;
            }

            if (selected.Connection == null || selected.Connection.Remote == null)
            {
                Console.WriteLine("Selected user has no real remote endpoint. This command cannot be used with fake users.");
                return false;
            }

            _server.Dispatcher.HandlePacket(msg.GetBytes(), selected.Connection.Remote);

            Console.WriteLine($"[CONSOLE] Sent {actionName} as {selected.Name}.");

            return true;
        }

        /*
         * What this does:
         * Returns selected user's endpoint if possible.
         * Otherwise returns a fake loopback endpoint.
         */
        private IPEndPoint GetSelectedUserEndpointOrFake()
        {
            ClientInfo selected = _server.GetSelectedUser();

            if (selected != null && selected.Connection != null && selected.Connection.Remote != null)
                return selected.Connection.Remote;

            return new IPEndPoint(IPAddress.Loopback, new Random().Next(10000, 60000));
        }

        #endregion

        #region Help And Shutdown

        /*
         * What this does:
         * Prints all available console commands.
         */
        private void ShowHelp()
        {
            Console.WriteLine("\n=== Server Console Commands ===");
            Console.WriteLine("/help                        - Show this help");

            Console.WriteLine("\nClient Commands");
            Console.WriteLine("/kickUser <id>               - Kick a client by ID");
            Console.WriteLine("/selectUser <id>             - Select a client for selected-user commands");
            Console.WriteLine("/findUser <name>             - Find a user by name");
            Console.WriteLine("/findUserID <id>             - Find a user by ID");
            Console.WriteLine("/allUsers                    - Show all connected users");
            Console.WriteLine("/createUser <name>           - Create a fake user for testing");

            Console.WriteLine("\nSelected User Commands");
            Console.WriteLine("/sendMsg <msg>               - Send a private message to the selected user");
            Console.WriteLine("/changeName <name>           - Change the selected user's name");

            Console.WriteLine("\nMessaging");
            Console.WriteLine("/broadcast <msg>             - Send message to all users");
            Console.WriteLine("/broadcastRoom <name> <msg>  - Broadcast to a specific room");
            Console.WriteLine("/send <addr> [params]        - Manually trigger an OSC message");

            Console.WriteLine("\nLobby Commands");
            Console.WriteLine("/allRooms                    - List all rooms");
            Console.WriteLine("/findRoom <name>             - Check if a room exists");
            Console.WriteLine("/createRoom <name> <points>  - Create a room, requires selected user as host");
            Console.WriteLine("/joinRoom <roomName>         - Join selected user to a room");
            Console.WriteLine("/closeRoom <name>            - Close a room");
            Console.WriteLine("/startRoom <name>            - Start a room");

            Console.WriteLine("\nGame Commands");
            Console.WriteLine("/selectDice <diceType>       - Selected user selects dice type 0-4");
            Console.WriteLine("/stakeRoll <Y/N>             - Selected user answers stake prompt");
            Console.WriteLine("/userTurn                    - Show whose turn it is in selected user's room");
            Console.WriteLine("/showAllPoints               - Show all player points in selected user's room");
            Console.WriteLine("/showPoints                  - Show selected user's points");
            Console.WriteLine("/rematch                     - Selected user requests rematch");
            Console.WriteLine("/leaveGame                   - Selected user leaves game");

            Console.WriteLine("\n/shutdown                    - Gracefully shut down the server");
            Console.WriteLine("===============================\n");
        }

        /*
         * What this does:
         * Gracefully shuts down the server.
         *
         * Flow:
         * 1. Broadcast a shutdown text message.
         * 2. Send shutdown OSC message to clients.
         * 3. Stop server.
         * 4. Exit application.
         */
        private void ShutdownServer()
        {
            Console.WriteLine("Shutting down server...");

            _server.BroadcastToAll("Server is shutting down.");

            var shutdownMsg = new OSCMessageOut("/shutdown")
                .AddString("Server is shutting down");

            lock (_server.SyncRoot)
            {
                foreach (var client in _server.Clients.Values)
                    _server.Send(client.Connection, shutdownMsg);
            }

            _server.Stop();

            Environment.Exit(0);
        }

        #endregion
    }
}