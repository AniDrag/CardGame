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
        private void OnCreateRoom(OSCMessageIn msg, IPEndPoint sender)
        {
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

            int pointGoal = msg.ReadInt();
            if (pointGoal < 10 || pointGoal > 80)
            {
                _server.SendError(client.Connection, "Goal must be 10-80");
                return;
            }

            if (_server.TryGetRoom(roomName, out _))
            {
                _server.SendError(client.Connection, "Room already exists");
                return;
            }

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
            _server.BroadcastToAll(confirmMsg);

            Console.WriteLine($"[ROOM] {client.Name} created '{roomName}' (goal {pointGoal})");
        }
        /// <summary>
        /// DONE
        /// Sends the confirmation of joining to client that is joining and sends a participant update to the room
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="sender"></param>
        private void OnJoinRoom(OSCMessageIn msg, IPEndPoint sender)
        {
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

            // Add participant
            room.Participants.Add(new Participant(client.Id, client.Name, 0));
            _server.UpdateClientRoom(client, roomName);

            var updateMsg = new OSCMessageOut(Msg.S_ROOM_UPDATE)
                .AddString(roomName)
                .AddInt(room.Participants.Count)
                .AddString(room.host)
                .AddInt(room.pointGoal)
                .AddBool(false);
            _server.BroadcastToAll(updateMsg);

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
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

            if (!_server.TryGetRoom(client.CurrentRoom, out var room)) return;

            var participant = room.Participants.FirstOrDefault(p => p.id == client.Id);
            if (participant != null) room.Participants.Remove(participant);
            _server.UpdateClientRoom(client, null);

            // If host left, assign new host
            if (room.host == client.Name && room.Participants.Count > 0)
                room.host = room.Participants[0].clientName;

            // If room empty, remove it
            if (room.Participants.Count == 0)
            {
                _server.RemoveRoom(room.roomName);
                // Broadcast update with participantCount=0 to remove from lists
                var removeMsg = new OSCMessageOut(Msg.S_CLOSED_ROOM)
                    .AddString(room.roomName);
                _server.BroadcastToAll(removeMsg);
            }
            else
            {
                var updateMsg = new OSCMessageOut(Msg.S_ROOM_UPDATE)
                    .AddString(room.roomName)
                    .AddInt(room.Participants.Count)
                    .AddString(room.host)
                    .AddInt(room.pointGoal)
                    .AddBool(room.GameStarted);
                _server.BroadcastToAll(updateMsg);
            }
            Console.WriteLine($"[ROOM] {client.Name} left {room.roomName}");
        }

        /// <summary>
        /// DONE
        /// Called when Joining the lobbie view by cliets one call per client
        /// </summary>
        /// <param name="msg">Lsit of rooms [name,host,pointGoal,participantCout]</param>
        /// <param name="sender"></param>
        private void OnListRooms(OSCMessageIn msg, IPEndPoint sender)
        {
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
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

            if (_server.TryGetRoom(client.CurrentRoom, out var room))
            {
                // Clear CurrentRoom for all participants in the room
                foreach (var p in room.Participants)
                {
                    var c = _server.FindPlayerById(p.id);
                    if (c != null) _server.UpdateClientRoom(c, null);
                }


                var closeRoom = new OSCMessageOut(Msg.S_CLOSED_ROOM)
                    .AddString(room.roomName);

                _server.BroadcastToAll(closeRoom);
                _server.RemoveRoom(client.CurrentRoom);
                Console.WriteLine($"[ROOM] {client.Name} closed room");
            }
            _server.SendError(client.Connection, "You do not own this room and cant be closed, Or multiple presses!");
        }
        

        /// <summary>
        /// DONE
        /// Start game, Room broadcast to start game, global broadcast room is in game and removes it from list for them. aka UpdateRoom is called.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="sender"></param>
        private void OnStartGame(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
            if (!_server.TryGetRoom(client.CurrentRoom, out var room)) return;
            if (room.host != client.Name)
            {
                _server.SendError(client.Connection, "Only host can start");
                return;
            }
            if (room.GameStarted) return;

            room.GameStarted = true;

            // Tell all clients in the room to load the game scene
            var gameStartedMsg = new OSCMessageOut(Msg.S_GAME_STARTED).AddString(room.roomName);
            _server.BroadcastToAll(gameStartedMsg);

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
