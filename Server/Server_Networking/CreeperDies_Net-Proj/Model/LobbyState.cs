using NetworkConnections;
using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CreeperDies_Net_Proj.Model
{
    public class LobbyState
    {
        private readonly TcpServer _server;
        private const int MaxUsernameLength = 12;
        private const int MaxRoomNameLength = 20;

        public LobbyState(TcpServer server)
        {
            _server = server;
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            var d = _server.Dispatcher;
            d.AddListener("/register", OnRegister, OSCUtil.STRING);
            d.AddListener("/disconnect", OnDisconnect);
            d.AddListener("/create_room", OnCreateRoom, OSCUtil.STRING, OSCUtil.INT);
            d.AddListener("/join_room", OnJoinRoom, OSCUtil.STRING);
            d.AddListener("/leave_room", OnLeaveRoom);
            d.AddListener("/list_rooms", OnListRooms);
            d.AddListener("/close_room", OnCloseRoom);
            // start_game is handled by GameState, but we need to let GameState know about it.
            // We'll register a separate handler in GameState.
        }

        private void OnRegister(OSCMessageIn msg, IPEndPoint sender)
        {
            var conn = _server.GetConnectionByEndpoint(sender);
            if (conn == null) return;

            string username = ReadCappedString(msg, MaxUsernameLength, "username");
            if (username == null)
            {
                _server.SendError(conn, $"Username too long (max {MaxUsernameLength})");
                return;
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                _server.SendError(conn, "Username cannot be empty");
                return;
            }

            int id = _server.GetNextClientId();
            var client = new ClientInfo
            {
                Id = id,
                Name = username,
                Connection = conn,
                CurrentRoom = null
            };
            _server.RegisterClient(id, client);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Registered {username} with ID {id}");

            var reply = new OSCMessageOut("/registered").AddInt(id).AddString(username);
            _server.Send(conn, reply);
        }

        private void OnDisconnect(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (client != null) _server.RemoveClient(client);
        }

        private void OnCreateRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (client == null) { _server.SendError(GetConnection(sender), "Not registered"); return; }
            if (!string.IsNullOrEmpty(client.CurrentRoom)) { _server.SendError(client.Connection, "Already in a room"); return; }

            string roomName = ReadCappedString(msg, MaxRoomNameLength, "room name");
            if (roomName == null) { _server.SendError(client.Connection, "Room name too long"); return; }

            int pointGoal = msg.ReadInt();
            if (_server.TryGetRoom(roomName, out _)) { _server.SendError(client.Connection, "Room already exists"); return; }

            var roomData = new RoomData(roomName.GetHashCode(), roomName, client.Name, pointGoal);
            roomData.Participants.Add(new Participant(client.Id, client.Name, 0));
            _server.AddRoom(roomData);
            _server.UpdateClientRoom(client, roomName);

            var success = new OSCMessageOut("/room_update")
                .AddString(roomName).AddInt(1).AddInt(4)
                .AddString(client.Name).AddInt(pointGoal).AddBool(false);
            _server.Send(client.Connection, success);
            Console.WriteLine($"[ROOM] {client.Name} created room '{roomName}' (goal {pointGoal})");
        }

        private void OnJoinRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (client == null) { _server.SendError(GetConnection(sender), "Not registered"); return; }
            if (!string.IsNullOrEmpty(client.CurrentRoom)) { _server.SendError(client.Connection, "Already in a room"); return; }

            string roomName = ReadCappedString(msg, MaxRoomNameLength, "room name");
            if (roomName == null) { _server.SendError(client.Connection, "Room name too long"); return; }

            if (!_server.TryGetRoom(roomName, out var room)) { _server.SendError(client.Connection, "Room not found"); return; }
            if (room.GameStarted) { _server.SendError(client.Connection, "Game already started"); return; }
            if (room.Participants.Count >= 4) { _server.SendError(client.Connection, "Room full"); return; }

            room.Participants.Add(new Participant(client.Id, client.Name, 0));
            _server.UpdateClientRoom(client, roomName);

            var update = new OSCMessageOut("/room_update")
                .AddString(roomName).AddInt(room.Participants.Count).AddInt(4)
                .AddString(client.Name).AddInt(room.pointGoal).AddBool(false);
            _server.BroadcastToRoom(room, update);
            Console.WriteLine($"[ROOM] {client.Name} joined {roomName}");
        }

        private void OnLeaveRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;

            if (_server.TryGetRoom(client.CurrentRoom, out var room))
            {
                var p = room.Participants.Find(x => x.id == client.Id);
                if (p != null) room.Participants.Remove(p);
                _server.UpdateClientRoom(client, null);

                if (room.host == client.Name && room.Participants.Count > 0)
                    room.host = room.Participants[0].clientName;

                if (room.Participants.Count == 0)
                    _server.RemoveRoom(room.roomName);
                else
                {
                    var update = new OSCMessageOut("/room_update")
                        .AddString(room.roomName).AddInt(room.Participants.Count).AddInt(4)
                        .AddString(room.host).AddBool(room.GameStarted);
                    _server.BroadcastToRoom(room, update);
                }
                Console.WriteLine($"[ROOM] {client.Name} left room");
            }
        }

        private void OnListRooms(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (client == null) return;

            var roomList = new OSCMessageOut("/room_list").AddInt(_server.Rooms.Count);
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

        private void OnCloseRoom(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
            if (_server.RemoveRoom(client.CurrentRoom))
            {
                _server.UpdateClientRoom(client, null);
                Console.WriteLine($"[ROOM] {client.Name} closed room");
            }
        }

        private TcpNetworkConnection GetConnection(IPEndPoint ep)
        {
            return _server.GetConnectionByEndpoint(ep);
        }

        private string ReadCappedString(OSCMessageIn msg, int max, string field)
        {
            string val = msg.ReadString();
            if (val == null || val.Length > max)
            {
                Console.WriteLine($"Invalid {field} length (max {max}): {val?.Length ?? 0}");
                return null;
            }
            return val;
        }
    }
}
