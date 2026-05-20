using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CreeperDice_Net_Proj.Model
{
    public class ConsoleCommandHandler
    {
        private readonly TcpServer _server;
        private Thread _thread;
        private volatile bool _running = true;

        public ConsoleCommandHandler(TcpServer server)
        {
            _server = server;
        }

        public void Start()
        {
            _thread = new Thread(Run);
            _thread.IsBackground = true;
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            _thread?.Join(1000);
        }

        private void Run()
        {
            while (_running)
            {
                string input = Console.ReadLine();
                if (string.IsNullOrEmpty(input)) continue;

                if (!input.StartsWith("/"))
                {
                    Console.WriteLine("Commands start with '/'. Type /help");
                    continue;
                }

                string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                ExecuteCommand(parts[0].ToLower(), parts);
            }
        }

        private void ExecuteCommand(string cmd, string[] parts)
        {
            lock (_server.SyncRoot)
            {
                switch (cmd)
                {
                    case "/help":
                        ShowHelp();
                        break;

                    // Client commands
                    case "/kickuser":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int kickId))
                            Console.WriteLine(_server.KickUser(kickId) ? "User kicked." : "User not found.");
                        else Console.WriteLine("Usage: /kickUser <id>");
                        break;

                    case "/selectuser":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int selId))
                            Console.WriteLine(_server.SelectUser(selId) ? "User selected." : "User not found.");
                        else Console.WriteLine("Usage: /selectUser <id>");
                        break;

                    case "/finduser":
                        if (parts.Length > 1)
                        {
                            var found = _server.FindPlayerByName(parts[1]);
                            Console.WriteLine(found != null ? $"Found: ID {found.Id} Name {found.Name}" : "User not found.");
                        }
                        else Console.WriteLine("Usage: /findUser <name>");
                        break;

                    case "/finduserid":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int findId))
                        {
                            var found = _server.FindPlayerById(findId);
                            Console.WriteLine(found != null ? $"Found: ID {found.Id} Name {found.Name}" : "User not found.");
                        }
                        else Console.WriteLine("Usage: /findUserID <id>");
                        break;

                    case "/allusers":
                        Console.WriteLine(_server.GetAllPlayersInfo());
                        break;

                    case "/createuser":
                        if (parts.Length > 1)
                            Console.WriteLine(_server.CreateFakeUser(parts[1]) ? "Fake user created." : "Creation failed.");
                        else Console.WriteLine("Usage: /createUser <name>");
                        break;

                    // User selected commands
                    case "/sendmsg":
                        if (_server.GetSelectedUser() == null)
                            Console.WriteLine("No user selected. Use /selectUser first.");
                        else if (parts.Length > 1)
                            Console.WriteLine(_server.SendPrivateMessage(_server.GetSelectedUser().Id, string.Join(" ", parts, 1)) ? "Message sent." : "Failed.");
                        else Console.WriteLine("Usage: /sendMsg <message>");
                        break;

                    case "/changename":
                        if (_server.GetSelectedUser() == null)
                            Console.WriteLine("No user selected. Use /selectUser first.");
                        else if (parts.Length > 1)
                            Console.WriteLine(_server.ChangeUserName(_server.GetSelectedUser().Id, parts[1]) ? "Name changed." : "Invalid name.");
                        else Console.WriteLine("Usage: /changeName <newName>");
                        break;

                    // Messaging
                    case "/broadcast":
                        if (parts.Length > 1)
                            _server.BroadcastToAll(string.Join(" ", parts, 1));
                        else Console.WriteLine("Usage: /broadcast <message>");
                        break;

                    case "/broadcastroom":
                        if (parts.Length > 2)
                        {
                            string roomName = parts[1];
                            string msg = string.Join(" ", parts, 2);
                            _server.BroadcastToRoom(roomName, msg);
                            Console.WriteLine($"Broadcast to room '{roomName}'.");
                        }
                        else Console.WriteLine("Usage: /broadcastRoom <roomName> <message>");
                        break;

                    case "/send":
                        HandleSendCommand(parts);
                        break;

                    // Lobby commands
                    case "/allrooms":
                        Console.WriteLine(_server.GetAllRoomsInfo());
                        break;

                    case "/findroom":
                        if (parts.Length > 1)
                            Console.WriteLine(_server.FindRoom(parts[1]) ? "Room exists." : "Room not found.");
                        else Console.WriteLine("Usage: /findRoom <name>");
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
                        else Console.WriteLine("Usage: /createRoom <name> <points> (must have a selected user as host)");
                        break;

                    case "/closeroom":
                        if (parts.Length > 1)
                            Console.WriteLine(_server.CloseRoom(parts[1]) ? "Room closed." : "Room not found.");
                        else Console.WriteLine("Usage: /closeRoom <name>");
                        break;

                    case "/startroom":
                        if (parts.Length > 1)
                            Console.WriteLine(_server.StartRoom(parts[1]) ? "Room started." : "Room not found or already started.");
                        else Console.WriteLine("Usage: /startRoom <name>");
                        break;
                    case "/joinroom":
                        if (parts.Length > 1)
                        {
                            string roomName = parts[1];
                            var selected = _server.GetSelectedUser();
                            if (selected == null) Console.WriteLine("No user selected. Use /selectUser first.");
                            else
                            {
                                // Simulate join request
                                var fakeMsg = new OSCMessageOut(Msg.C_JOIN_ROOM).AddString(roomName);
                                var fakeEndpoint = new IPEndPoint(IPAddress.Loopback, 12345);
                                _server.Dispatcher.HandlePacket(fakeMsg.GetBytes(), fakeEndpoint);
                                Console.WriteLine($"Sent join request for {selected.Name} to room {roomName}");
                            }
                        }
                        else Console.WriteLine("Usage: /joinroom <roomName>");
                        break;

                    // Game commands (simple stubs – you can expand later)
                    case "/stakesroll":
                        Console.WriteLine("Stake roll command not fully implemented in console.");
                        break;
                    case "/userturn":
                        Console.WriteLine("Not implemented in console.");
                        break;
                    case "/showallpoints":
                        Console.WriteLine("Not implemented in console.");
                        break;
                    case "/showpoints":
                        Console.WriteLine("Not implemented in console.");
                        break;

                    case "/shutdown":
                        ShutdownServer();
                        break;

                    default:
                        Console.WriteLine("Unknown command. Type /help");
                        break;
                }
            }
        }

        private void HandleSendCommand(string[] parts)
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
                _server.Dispatcher.HandlePacket(data, fakeSender);
                Console.WriteLine($"[CONSOLE] Sent synthetic: {address}");
            }
            catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
        }

        private void ShowHelp()
        {
            Console.WriteLine("\n=== Server Console Commands ===");
            Console.WriteLine("/help                        - Show this help");
            Console.WriteLine("Client Commands");
            Console.WriteLine("/kickUser <id>               - Kick a client by ID");
            Console.WriteLine("/selectUser <id>             - Select a client for additional commands");
            Console.WriteLine("/findUser <name>             - Find a user by name");
            Console.WriteLine("/findUserID <id>             - Find a user by ID");
            Console.WriteLine("/allUsers                    - Show all connected users");
            Console.WriteLine("/createUser <name>           - Create a fake user for testing");
            Console.WriteLine("\nUser Selected commands (after /selectUser)");
            Console.WriteLine("/sendMsg <msg>               - Send a private message to the selected user");
            Console.WriteLine("/changeName <name>           - Change the selected user's name");
            Console.WriteLine("\nMessaging");
            Console.WriteLine("/broadcast <msg>             - Send message to all users");
            Console.WriteLine("/broadcastRoom <name> <msg>  - Broadcast to a specific room");
            Console.WriteLine("/send <addr> [params]        - Manually trigger an OSC message");
            Console.WriteLine("\nLobby Commands");
            Console.WriteLine("/allRooms                    - List all rooms");
            Console.WriteLine("/findRoom <name>             - Check if a room exists");
            Console.WriteLine("/createRoom <name> <points>  - Create a room (requires selected host)");
            Console.WriteLine("/joinroom <roomName>         - Join a room (requires selected host)");
            Console.WriteLine("/closeRoom <name>            - Close a room and kick players");
            Console.WriteLine("/startRoom <name>            - Start a room (sets GameStarted true)");
            Console.WriteLine("\nGame Commands (placeholders)");
            Console.WriteLine("/stakesRoll <Y/N>            - Respond to stake roll prompt");
            Console.WriteLine("/userTurn                    - Show whose turn it is");
            Console.WriteLine("/showAllPoints               - Show all players' points");
            Console.WriteLine("/showPoints                  - Show your points");
            Console.WriteLine("\n/shutdown                   - Gracefully shut down the server");
            Console.WriteLine("===============================\n");
        }

        private void ShutdownServer()
        {
            Console.WriteLine("Shutting down server...");
            _server.BroadcastToAll("Server is shutting down.");
            var shutdownMsg = new OSCMessageOut("/shutdown").AddString("Server is shutting down");
            lock (_server.SyncRoot)
            {
                foreach (var client in _server.Clients.Values)
                    _server.Send(client.Connection, shutdownMsg);
            }
            _server.Stop();
            Environment.Exit(0);
        }
    }
}
