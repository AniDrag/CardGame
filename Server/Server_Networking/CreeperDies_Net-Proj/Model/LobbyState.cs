using NetworkConnections;
using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace CreeperDice_Net_Proj.Model
{
    public class LobbyState
    {

        private readonly TcpServer _server;
        private readonly GameState _gameState;
        private const int MaxRoomNameLength = 20;

        public LobbyState(TcpServer server)
        {
            _server = server;
            _gameState = _server.game;
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            var d = _server.Dispatcher;
            d.AddListener(Msg.C_CREATE_ROOM, OnCreateRoom, OSCUtil.STRING, OSCUtil.INT);
            d.AddListener(Msg.C_JOIN_ROOM, OnJoinRoom, OSCUtil.STRING);
            d.AddListener(Msg.C_LEAVE_ROOM, OnLeaveRoom);
            d.AddListener(Msg.C_LIST_ROOMS, OnListRooms);
            d.AddListener(Msg.C_CLOSE_ROOM, OnCloseRoom);
            d.AddListener(Msg.C_START_GAME, OnStartGame);
        }
        /// <summary>
        /// DONE
        /// Recives Create room request. Sends back a replie crateRoom_ with the created room info.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="sender"></param>
        public void OnCreateRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);

            Console.WriteLine("[ Creating room ] Started");

            if (client == null)
            {
                _server.SendError(GetConnection(sender), "[ Creating room ] Not registered");
                return;
            }
            if (!string.IsNullOrEmpty(client.CurrentRoom))
            {
                _server.SendError(client.Connection, "[ Creating room ] Already in a room");
                return;
            }

            string roomName = _server.ReadCappedString(msg, MaxRoomNameLength, "room name");
            if (roomName == null)
            {
                _server.SendError(client.Connection, "[ Creating room ] Room name too long");
                return;
            }

            int pointGoal = msg.ReadInt();
            if (pointGoal < 10 || pointGoal > 80)
            {
                _server.SendError(client.Connection, "[ Creating room ] Goal must be 10-80");
                return;
            }

            if (_server.TryGetRoom(roomName, out _))
            {
                _server.SendError(client.Connection, "[ Creating room ] Room already exists");
                return;
            }

            Console.WriteLine("[ Creating room ] PASS Cheks");

            // Create room
            var room = new RoomData(roomName.GetHashCode(), roomName, client.Name, pointGoal);
            room.Participants.Add(new Participant(client.Id, client.Name));
            _server.AddRoom(room);
            _server.UpdateClientRoom(client, roomName);// sets room for the client

            var confirmMsg = new OSCMessageOut(Msg.S_CREATED_ROOM)
                .AddString(roomName)
                .AddInt(room.Participants.Count)
                .AddString(client.Name)
                .AddInt(pointGoal)
                .AddBool(false);
            Console.WriteLine("[ Creating room ] Broadcasting");
            _server.BroadcastToAll(confirmMsg);

            Console.WriteLine($"[ROOM] {client.Name} created '{roomName}' (goal {pointGoal})");
            Console.WriteLine($"Sending S_CREATED_ROOM: room={roomName}, participants={room.Participants.Count}, host={client.Name}, goal={pointGoal}, started=false");
        }
        /// <summary>
        /// DONE
        /// Sends the confirmation of joining to client that is joining and sends a participant update to the room
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="sender"></param>
        private void OnJoinRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[ Join room ] Started");
            var client = _server.GetClientByEndpoint(sender);
            if (client == null)
            {
                _server.SendError(GetConnection(sender), "Not registered");
                return;
            }
            if (!string.IsNullOrEmpty(client.CurrentRoom))
            {
                _server.SendError(client.Connection, "Already in a room");
                return;
            }

            string roomName = _server.ReadCappedString(msg, MaxRoomNameLength, "room name");
            if (roomName == null)
            {
                _server.SendError(client.Connection, "Room name too long");
                return;
            }

            if (!_server.TryGetRoom(roomName, out var room))
            {
                _server.SendError(client.Connection, "Room not found");
                return;
            }
            if (room.GameStarted)
            {
                _server.SendError(client.Connection, "Game already started");
                return;
            }
            if (room.Participants.Count >= 4)
            {
                _server.SendError(client.Connection, "Room full");
                return;
            }

            Console.WriteLine("[ Join room ] PASSED checks");
            // Add participant
            room.Participants.Add(new Participant(client.Id, client.Name, 0));
            _server.UpdateClientRoom(client, roomName);

            // 1. Send S_JOINED only to the joining client (with all room details)
            var joinedMsg = new OSCMessageOut(Msg.S_JOINED)
                .AddString(roomName)
                .AddInt(room.Participants.Count)          // current participant count after join
                .AddString(room.host)                     // host name
                .AddInt(room.pointGoal)                   // point goal
                .AddBool(room.GameStarted);               // game started flag
            _server.Send(client.Connection, joinedMsg);
            Console.WriteLine("[ Join room ] Sent S_JOINED to " + client.Name);

            // 2. Broadcast S_ROOM_UPDATE to ALL clients (including the new joiner)
            var updateMsg = new OSCMessageOut(Msg.S_ROOM_UPDATE)
                .AddString(roomName)
                .AddInt(room.Participants.Count)
                .AddString(room.host)
                .AddInt(room.pointGoal)
                .AddBool(room.GameStarted);
            _server.BroadcastToAll(updateMsg);
            Console.WriteLine("[ Join room ] Broadcasted S_ROOM_UPDATE");

            Console.WriteLine($"[ROOM] {client.Name} joined {roomName}");
        }

        /// <summary>
        /// DONE
        /// Adds or removes participants, Reasigns Host
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="sender"></param>
        private void OnLeaveRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[ Leave room ] Started");
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

            if (!_server.TryGetRoom(client.CurrentRoom, out var room)) return;

            var participant = room.Participants.FirstOrDefault(p => p.id == client.Id);
            if (participant != null) room.Participants.Remove(participant);

            Console.WriteLine("[ Leave room ] PASSED Checks");
            _server.UpdateClientRoom(client, null);

            // If host left, assign new host
            if (room.host == client.Name && room.Participants.Count > 0)
            {
                room.host = room.Participants[0].clientName;
                _server.BroadcastToRoom(room.roomName, "CLient left Game new client is: " + room.host);
            }

            // If room empty, remove it
            if (room.Participants.Count == 0)
            {
                Console.WriteLine("[ Leave room ] Last participant left, Closed room.");
                CleareParticipantRefs(room);
            }
            else
            {
                Console.WriteLine("[ Leave room ] Broadcasting MSG Room Update");
                RoomChangeMessage(room);
            }
            Console.WriteLine($"[ROOM] {client.Name} left {room.roomName}");
        }

        void RoomChangeMessage(RoomData room)
        {
            var updateMsg = new OSCMessageOut(Msg.S_ROOM_UPDATE)
                    .AddString(room.roomName)
                    .AddInt(room.Participants.Count)
                    .AddString(room.host)
                    .AddInt(room.pointGoal)
                    .AddBool(room.GameStarted);
            _server.BroadcastToAll(updateMsg);
        }
        /// <summary>
        /// DONE
        /// Called when Joining the lobbie view by cliets one call per client
        /// </summary>
        /// <param name="msg">Lsit of rooms [name,host,pointGoal,participantCout]</param>
        /// <param name="sender"></param>
        private void OnListRooms(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[ List room ] Called");
            var client = _server.GetClientByEndpoint(sender);
            if (client == null) return;

            var roomList = new OSCMessageOut(Msg.S_ROOM_LIST).AddInt(_server.Rooms.Count);
            foreach (var room in _server.Rooms.Values)
            {
                roomList.AddString(room.roomName);
                roomList.AddInt(room.pointGoal);
                roomList.AddString(room.host);
                roomList.AddInt(room.Participants.Count);
                roomList.AddInt(0);
            }
            _server.GetAllRoomsInfo();
            _server.Send(client.Connection, roomList);
        }
        /// <summary>
        /// DONE
        /// Broadcass aclosed room, cliet will take care of ui 
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="sender"></param>
        private void OnCloseRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[ Close room ] Called");
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

            if (_server.TryGetRoom(client.CurrentRoom, out var room))
            {
                CleareParticipantRefs(room);
                _server.RemoveRoom(room.roomName);
                Console.WriteLine($"[ROOM] {client.Name} closed room");
            }
            else
            {
                _server.SendError(client.Connection, "You do not own this room or it doesn't exist!");
            }
        }

        void CleareParticipantRefs(RoomData room)
        {
            foreach (var p in room.Participants)
            {
                var c = _server.FindPlayerById(p.id);
                if (c != null) _server.UpdateClientRoom(c, null);
            }

            Console.WriteLine("[ Close room ] SendingMSG");
            var closeRoom = new OSCMessageOut(Msg.S_CLOSED_ROOM)
                .AddString(room.roomName);
            _server.BroadcastToAll(closeRoom);

        }


        /// <summary>
        /// DONE
        /// Start game, Room broadcast to start game, global broadcast room is in game and removes it from list for them. aka UpdateRoom is called.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="sender"></param>
        private void OnStartGame(OSCMessageIn msg, IPEndPoint sender)
        {
            Console.WriteLine("[ Start Game ] Started");
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
            if (!_server.TryGetRoom(client.CurrentRoom, out var room)) return;
            if (room.host != client.Name)
            {
                _server.SendError(client.Connection, "Only host can start");
                return;
            }
            if (room.GameStarted) return;

            Console.WriteLine("[ Start Game ] PASS checks");
            room.GameStarted = true;

            Console.WriteLine("[ Start Game ] Broadcast to participants start game");
            // Tell all clients in the room to load the game scene
            var gameStartedMsg = new OSCMessageOut(Msg.S_GAME_STARTED);
            _server.BroadcastToRoom(room.roomName,gameStartedMsg);

            Console.WriteLine("[ Start Game ] general broadcast for room change");
            //rest of clients recive the change
            RoomChangeMessage(room);

            // Hand off to GameState for game logic
            _gameState.StartGameForRoom(room);
        }

        #region Helpers

        private TcpNetworkConnection GetConnection(IPEndPoint ep)
        {
            return _server.GetConnectionByEndpoint(ep);
        }
        #endregion
    }
}
