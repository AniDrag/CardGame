using NetworkConnections;
using OSCTools;
using System;
using System.Linq;
using System.Net;

namespace CreeperDice_Net_Proj.Model
{
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

        #region Received Messages

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

        private void OnListRooms(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[List Rooms] Called");

            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null)
                return;

            SendRoomList(client);
        }

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

        #region Sending Messages

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

        private void SendRoomClosed(RoomData room)
        {
            var msg = new OSCMessageOut(Msg.S_CLOSED_ROOM)
                .AddString(room.roomName);

            _server.BroadcastToAll(msg);
        }

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