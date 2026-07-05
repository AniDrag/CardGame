using NetworkConnections;
using OSCTools;
using System;
using System.Linq;
using System.Net;

namespace CreeperDice_Net_Proj.Model
{
    /*
     * LobbyState
     *
     * Purpose:
     * This class controls the server-side lobby and room system.
     *
     * It handles:
     * - Creating rooms.
     * - Joining rooms.
     * - Leaving rooms.
     * - Closing hosted rooms.
     * - Listing available rooms.
     * - Starting the game from a room.
     *
     * Naming rule used:
     * - On prefix = receives an OSC message from a client.
     * - Send prefix = sends an OSC message to a client, room, or all clients.
     * - No On prefix = normal server logic, validation, or helper function.
     *
     * Important:
     * This is server-side authoritative logic.
     * The client can request lobby actions, but this class validates if the action is allowed.
     */
    public class LobbyState
    {
        #region Fields
        private readonly TcpServer _server;
        private const int MaxRoomNameLength = 20;

        #endregion

        #region Constructor

        public LobbyState(TcpServer server)
        {
            _server = server;
            RegisterHandlers();
        }

        #endregion

        #region Message Registration

        /*
         * What this does:
         * Registers all client-to-server lobby OSC messages.
         *
         * OSC received:
         *
         * Msg.C_CREATE_ROOM
         * Payload:
         * [0] string roomName
         * [1] int pointGoal
         *
         * Msg.C_JOIN_ROOM
         * Payload:
         * [0] string roomName
         *
         * Msg.C_LEAVE_ROOM
         * Payload:
         * No data.
         *
         * Msg.C_LIST_ROOMS
         * Payload:
         * No data.
         *
         * Msg.C_CLOSE_ROOM
         * Payload:
         * No data.
         *
         * Msg.C_START_GAME
         * Payload:
         * No data.
         */
        private void RegisterHandlers()
        {
            var dispatcher = _server.Dispatcher;

            dispatcher.AddListener(Msg.C_CREATE_ROOM, OnCreateRoom, OSCUtil.STRING, OSCUtil.INT);
            dispatcher.AddListener(Msg.C_JOIN_ROOM, OnJoinRoom, OSCUtil.STRING);
            dispatcher.AddListener(Msg.C_LEAVE_ROOM, OnLeaveRoom);
            dispatcher.AddListener(Msg.C_LIST_ROOMS, OnListRooms);
            dispatcher.AddListener(Msg.C_CLOSE_ROOM, OnCloseRoom);
            dispatcher.AddListener(Msg.C_START_GAME, OnStartGame);
        }

        #endregion

        #region Received OSC Messages

        /*
         * OSC RECEIVE: Msg.C_CREATE_ROOM
         *
         * Payload received:
         * [0] string roomName
         * [1] int pointGoal
         *
         * Example:
         * roomName = "Test Room"
         * pointGoal = 25
         *
         * What this does:
         * Validates the client and room data.
         * If valid, creates a room hosted by this client.
         *
         * Validation:
         * - Client must be registered.
         * - Client must not already be in a room.
         * - Room name must not be too long.
         * - Point goal must be between 10 and 80.
         * - Room name must not already exist.
         */
        private void OnCreateRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[Create Room] Started");

            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null)
            {
                _server.SendError(GetConnection(sender), "[Create Room] Not registered");
                return;
            }

            if (!ValidateClientNotInRoom(client, "[Create Room]"))
                return;

            string roomName = _server.ReadCappedString(msg, MaxRoomNameLength, "room name");

            if (roomName == null)
            {
                _server.SendError(client.Connection, "[Create Room] Room name too long");
                return;
            }

            int pointGoal = msg.ReadInt();

            if (!ValidatePointGoal(client, pointGoal))
                return;

            if (_server.TryGetRoom(roomName, out _))
            {
                _server.SendError(client.Connection, "[Create Room] Room already exists");
                return;
            }

            Console.WriteLine("[Create Room] Passed checks");

            RoomData room = CreateRoom(client, roomName, pointGoal);

            SendCreatedRoom(client, room);
            SendRoomUpdate(room);

            Console.WriteLine($"[ROOM] {client.Name} created '{roomName}' with goal {pointGoal}");
        }

        /*
         * OSC RECEIVE: Msg.C_JOIN_ROOM
         *
         * Payload received:
         * [0] string roomName
         *
         * Example:
         * roomName = "Test Room"
         *
         * What this does:
         * Lets a registered client join an existing room.
         *
         * Validation:
         * - Client must be registered.
         * - Client must not already be in a room.
         * - Room name must not be too long.
         * - Room must exist.
         * - Room must not already be in game.
         * - Room must not be full.
         */
        private void OnJoinRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[Join Room] Started");

            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null)
            {
                _server.SendError(GetConnection(sender), "[Join Room] Not registered");
                return;
            }

            if (!ValidateClientNotInRoom(client, "[Join Room]"))
                return;

            string roomName = _server.ReadCappedString(msg, MaxRoomNameLength, "room name");

            if (roomName == null)
            {
                _server.SendError(client.Connection, "[Join Room] Room name too long");
                return;
            }

            if (!_server.TryGetRoom(roomName, out RoomData room))
            {
                _server.SendError(client.Connection, "[Join Room] Room not found");
                return;
            }

            if (!ValidateRoomJoinable(client, room))
                return;

            Console.WriteLine("[Join Room] Passed checks");

            AddClientToRoom(client, room);

            SendJoinedRoom(client, room);
            SendRoomUpdate(room);

            Console.WriteLine($"[ROOM] {client.Name} joined '{roomName}'");
        }

        /*
         * OSC RECEIVE: Msg.C_LEAVE_ROOM
         *
         * Payload received:
         * No data.
         *
         * What this does:
         * Removes the client from their current room.
         *
         * If the room becomes empty:
         * The room is closed.
         *
         * If the host leaves but players remain:
         * A new host is assigned.
         */
        private void OnLeaveRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[Leave Room] Started");

            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null || string.IsNullOrEmpty(client.CurrentRoom))
                return;

            if (!_server.TryGetRoom(client.CurrentRoom, out RoomData room))
                return;

            RemoveClientFromRoom(client, room);

            if (room.Participants.Count == 0)
            {
                CloseRoom(room);
                return;
            }

            ReassignHostIfNeeded(client, room);

            SendRoomUpdate(room);

            Console.WriteLine($"[ROOM] {client.Name} left '{room.roomName}'");
        }

        /*
         * OSC RECEIVE: Msg.C_LIST_ROOMS
         *
         * Payload received:
         * No data.
         *
         * What this does:
         * Sends the current room list to the requesting client.
         */
        private void OnListRooms(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[List Rooms] Called");

            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null)
                return;

            SendRoomList(client);
        }

        /*
         * OSC RECEIVE: Msg.C_CLOSE_ROOM
         *
         * Payload received:
         * No data.
         *
         * What this does:
         * Allows the host to close their current room.
         *
         * Validation:
         * - Client must exist.
         * - Client must be in a room.
         * - Room must exist.
         * - Client must be the host.
         */
        private void OnCloseRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[Close Room] Called");

            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null || string.IsNullOrEmpty(client.CurrentRoom))
                return;

            if (!_server.TryGetRoom(client.CurrentRoom, out RoomData room))
            {
                _server.SendError(client.Connection, "[Close Room] Room does not exist");
                return;
            }

            if (room.host != client.Name)
            {
                _server.SendError(client.Connection, "[Close Room] Only host can close the room");
                return;
            }

            CloseRoom(room);

            Console.WriteLine($"[ROOM] {client.Name} closed '{room.roomName}'");
        }

        /*
         * OSC RECEIVE: Msg.C_START_GAME
         *
         * Payload received:
         * No data.
         *
         * What this does:
         * Starts the game loading process for the room.
         *
         * Validation:
         * - Client must exist.
         * - Client must be in a room.
         * - Room must exist.
         * - Client must be the host.
         * - Game must not already be started.
         *
         * Important:
         * This does not directly start the game logic.
         * It marks room.GameStarted as true and sends Msg.S_GAME_STARTED.
         * Clients then load the game scene.
         * GameState starts the actual game after all clients send Msg.C_GAME_SCENE_READY.
         */
        private void OnStartGame(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[Start Game] Started");

            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null || string.IsNullOrEmpty(client.CurrentRoom))
                return;

            if (!_server.TryGetRoom(client.CurrentRoom, out RoomData room))
                return;

            if (room.host != client.Name)
            {
                _server.SendError(client.Connection, "[Start Game] Only host can start");
                return;
            }

            if (room.GameStarted)
                return;

            Console.WriteLine("[Start Game] Passed checks");

            room.GameStarted = true;

            SendGameStarted(room);
            SendRoomUpdate(room);

            Console.WriteLine("[Start Game] Waiting for clients to load game scene.");
            //_server.game.StartGameForRoom(room);
        }

        #endregion

        #region Room Logic

        /*
         * What this does:
         * Creates a new room object and adds the host as the first participant.
         *
         * Data received:
         * hostClient = client creating the room.
         * roomName = name of the new room.
         * pointGoal = score needed to win.
         *
         * Returns:
         * The created RoomData.
         */
        private RoomData CreateRoom(ClientInfo hostClient, string roomName, int pointGoal)
        {
            RoomData room = new RoomData(roomName.GetHashCode(), roomName, hostClient.Name, pointGoal);

            room.Participants.Add(new Participant(hostClient.Id, hostClient.Name));

            _server.AddRoom(room);
            _server.UpdateClientRoom(hostClient, roomName);

            return room;
        }
        private void AddClientToRoom(ClientInfo client, RoomData room)
        {
            room.Participants.Add(new Participant(client.Id, client.Name, 0));
            _server.UpdateClientRoom(client, room.roomName);
        }
        private void RemoveClientFromRoom(ClientInfo client, RoomData room)
        {
            Participant participant = room.Participants.FirstOrDefault(p => p.id == client.Id);

            if (participant != null)
                room.Participants.Remove(participant);

            _server.UpdateClientRoom(client, null);
        }

        /*
         * What this does:
         * If the leaving client was the room host,
         * assigns the first remaining participant as the new host.
         *
         * If the leaving client was not the host:
         * Nothing changes.
         */
        private void ReassignHostIfNeeded(ClientInfo leavingClient, RoomData room)
        {
            if (room.host != leavingClient.Name)
                return;

            if (room.Participants.Count <= 0)
                return;

            Participant newHost = room.Participants[0];
            room.host = newHost.clientName;

            _server.BroadcastToRoom(room.roomName, $"New host is: {room.host}");
        }

        /*
         * What this does:
         * Fully closes a room.
         *
         * Flow:
         * 1. Clear CurrentRoom on every participant.
         * 2. Tell all clients the room was closed.
         * 3. Remove the room from the server room list.
         */
        private void CloseRoom(RoomData room)
        {
            ClearParticipantRoomRefs(room);

            SendRoomClosed(room);

            _server.RemoveRoom(room.roomName);
        }
        private void ClearParticipantRoomRefs(RoomData room)
        {
            foreach (Participant participant in room.Participants)
            {
                ClientInfo client = _server.FindPlayerById(participant.id);

                if (client != null)
                    _server.UpdateClientRoom(client, null);
            }
        }

        #endregion

        #region Sending OSC Messages

        /*
         * OSC SEND: Msg.S_CREATED_ROOM
         *
         * Sent to:
         * The client who created the room.
         *
         * Payload sent:
         * [0] string roomName
         * [1] int participantCount
         * [2] string hostName
         * [3] int pointGoal
         * [4] bool gameStarted
         *
         * What this tells the client:
         * The room was created successfully and this client is now inside it.
         */
        private void SendCreatedRoom(ClientInfo client, RoomData room)
        {
            var msg = new OSCMessageOut(Msg.S_CREATED_ROOM)
                .AddString(room.roomName)
                .AddInt(room.Participants.Count)
                .AddString(room.host)
                .AddInt(room.pointGoal)
                .AddBool(room.GameStarted);

            _server.Send(client.Connection, msg);
        }

        /*
         * OSC SEND: Msg.S_JOINED
         *
         * Sent to:
         * The client who joined the room.
         *
         * Payload sent:
         * [0] string roomName
         * [1] int participantCount
         * [2] string hostName
         * [3] int pointGoal
         * [4] bool gameStarted
         *
         * What this tells the client:
         * Join room succeeded and this client is now inside the room.
         */
        private void SendJoinedRoom(ClientInfo client, RoomData room)
        {
            var msg = new OSCMessageOut(Msg.S_JOINED)
                .AddString(room.roomName)
                .AddInt(room.Participants.Count)
                .AddString(room.host)
                .AddInt(room.pointGoal)
                .AddBool(room.GameStarted);

            _server.Send(client.Connection, msg);
        }

        /*
         * OSC SEND: Msg.S_ROOM_UPDATE
         *
         * Sent to:
         * All connected clients.
         *
         * Payload sent:
         * [0] string roomName
         * [1] int participantCount
         * [2] string hostName
         * [3] int pointGoal
         * [4] bool gameStarted
         *
         * What this tells clients:
         * A room was created, changed, started, or had participants changed.
         */
        private void SendRoomUpdate(RoomData room)
        {
            var msg = new OSCMessageOut(Msg.S_ROOM_UPDATE)
                .AddString(room.roomName)
                .AddInt(room.Participants.Count)
                .AddString(room.host)
                .AddInt(room.pointGoal)
                .AddBool(room.GameStarted);

            _server.BroadcastToAll(msg);
        }

        /*
         * OSC SEND: Msg.S_ROOM_LIST
         *
         * Sent to:
         * One requesting client.
         *
         * Payload sent:
         * [0] int roomCount
         * Then repeated roomCount times:
         *     string roomName
         *     int pointGoal
         *     string hostName
         *     int participantCount
         *     int gameStartedState
         *
         * gameStartedState:
         * 0 = room is still in lobby.
         * 1 = room game already started.
         */
        private void SendRoomList(ClientInfo client)
        {
            var msg = new OSCMessageOut(Msg.S_ROOM_LIST)
                .AddInt(_server.Rooms.Count);

            foreach (RoomData room in _server.Rooms.Values)
            {
                msg.AddString(room.roomName);
                msg.AddInt(room.pointGoal);
                msg.AddString(room.host);
                msg.AddInt(room.Participants.Count);
                msg.AddInt(room.GameStarted ? 1 : 0);
            }

            _server.Send(client.Connection, msg);
        }

        /*
         * OSC SEND: Msg.S_CLOSED_ROOM
         *
         * Sent to:
         * All connected clients.
         *
         * Payload sent:
         * [0] string roomName
         *
         * What this tells clients:
         * The room was closed and should be removed from room lists.
         */
        private void SendRoomClosed(RoomData room)
        {
            var msg = new OSCMessageOut(Msg.S_CLOSED_ROOM)
                .AddString(room.roomName);

            _server.BroadcastToAll(msg);
        }

        /*
         * OSC SEND: Msg.S_GAME_STARTED
         *
         * Sent to:
         * All clients inside the room.
         *
         * Payload sent:
         * No data.
         *
         * What this tells clients:
         * Load the game scene.
         *
         * Important:
         * After loading, each client should send Msg.C_GAME_SCENE_READY.
         */
        private void SendGameStarted(RoomData room)
        {
            var msg = new OSCMessageOut(Msg.S_GAME_STARTED);

            _server.BroadcastToRoom(room.roomName, msg);
        }

        #endregion

        #region Validation

        private bool ValidateClientNotInRoom(ClientInfo client, string context)
        {
            if (string.IsNullOrEmpty(client.CurrentRoom))
                return true;

            _server.SendError(client.Connection, $"{context} Already in a room");
            return false;
        }
        private bool ValidatePointGoal(ClientInfo client, int pointGoal)
        {
            if (pointGoal >= 10 && pointGoal <= 80)
                return true;

            _server.SendError(client.Connection, "[Create Room] Goal must be between 10 and 80");
            return false;
        }

        private bool ValidateRoomJoinable(ClientInfo client, RoomData room)
        {
            if (room.GameStarted)
            {
                _server.SendError(client.Connection, "[Join Room] Game already started");
                return false;
            }

            if (room.Participants.Count >= 4)
            {
                _server.SendError(client.Connection, "[Join Room] Room full");
                return false;
            }

            return true;
        }

        #endregion

        #region Helpers
         
        private TcpNetworkConnection GetConnection(IPEndPoint endpoint)
        {
            return _server.GetConnectionByEndpoint(endpoint);
        }

        #endregion
    }
}