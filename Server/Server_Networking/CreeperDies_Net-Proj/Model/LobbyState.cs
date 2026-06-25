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
/*
Q & A session – LobbyState

Q1: What is the primary responsibility of LobbyState?
A1: LobbyState handles all server-side logic related to the lobby phase: creating rooms, joining, leaving, 
    listing rooms, closing rooms, and starting the game. It acts as a state machine that processes client 
    requests (OSC messages) and updates the server’s room and client data accordingly. It delegates game 
    logic to GameState after the game starts.

Q2: Why separate LobbyState from TcpServer instead of putting all logic in TcpServer?
A2: Separation of concerns. TcpServer handles networking (connections, sending/receiving), client management, 
    and shared state. LobbyState handles only lobby-specific business logic. This keeps TcpServer from becoming 
    bloated and makes the code more modular, testable, and maintainable. It also allows us to have other state 
    classes (like GameState) for different phases.

Q3: Why inject TcpServer via the constructor and not use a static reference?
A3: Dependency injection makes the class explicitly depend on TcpServer. It improves testability (we can mock 
    TcpServer in unit tests) and clarifies that LobbyState needs server services (client lookup, sending messages, 
    accessing rooms). It also avoids global state and makes the lifecycle clear.

Q4: How does LobbyState register its OSC handlers, and why not in the constructor directly?
A4: In the constructor, it calls RegisterHandlers() which subscribes to the OSC dispatcher for messages like 
    C_CREATE_ROOM, C_JOIN_ROOM, etc. This decouples handler registration from the rest of the setup. Doing it in 
    the constructor ensures handlers are ready as soon as the LobbyState is created. The dispatcher is obtained 
    from _server.Dispatcher.

Q5: Why does OnCreateRoom use _server.ReadCappedString() instead of msg.ReadString() directly?
A5: To enforce a maximum length (MaxRoomNameLength = 20). This prevents overly long room names from being stored 
    or causing issues. It also centralises validation – the helper method checks the length and returns null if 
    invalid, and we send an error back to the client. This is a basic security measure against abuse.

Q6: Why does OnCreateRoom send S_CREATED_ROOM via BroadcastToAll instead of just to the host?
A6: S_CREATED_ROOM is a private message to the host (it's marked as "N (private)" in Msg). However, the code 
    broadcasts it to all clients. This seems like a bug or design inconsistency. The broadcast is likely intended 
    to update the room list for all clients, but S_CREATED_ROOM is not the correct message for that. A better 
    approach would be to send S_CREATED_ROOM privately to the host and then broadcast S_ROOM_UPDATE to all clients 
    to add the new room to the lobby list. The current code might cause unexpected behaviour. The method also 
    does not send S_ROOM_UPDATE, so the new room may not appear in other clients' lists until they refresh. 
    This is a potential issue to discuss.

Q7: Why does OnCreateRoom call _server.BroadcastToAll(confirmMsg) instead of _server.Send(client.Connection, confirmMsg)?
A7: As noted, the protocol expects S_CREATED_ROOM to be private. Broadcasting it means every client receives a 
    "created room" message, but the message contains only the room name and host. It does not contain all the 
    fields needed to add the room to the list (e.g., host, goal). This seems incomplete. Ideally, we would send 
    S_CREATED_ROOM only to the creator, and then broadcast S_ROOM_UPDATE to all clients to synchronise the room 
    list. The current design is problematic.

Q8: How does OnJoinRoom ensure that the joining client is not already in a room?
A8: It checks client.CurrentRoom; if it's not null or empty, it sends an error and returns. This prevents a client 
    from trying to join while in another room, avoiding inconsistent states.

Q9: Why does OnJoinRoom send two separate messages: S_JOINED to the joiner and S_ROOM_UPDATE to all clients?
A9: S_JOINED is a private message that contains full room details (participant count, host, goal, gameStarted) 
    to show the waiting view on the client. S_ROOM_UPDATE is broadcast to all clients (including the joiner) to 
    update the room list with the new participant count and host. This separation is correct: one message is for 
    the joining client's UI transition, the other updates the global lobby state.

Q10: How does OnLeaveRoom handle the case where the host leaves?
A10: It checks if room.host == client.Name and if there are other participants. If so, it assigns the first 
    participant as the new host (room.host = room.Participants[0].clientName). It also broadcasts a server message 
    saying "Client left game new client is: ..." and sends S_ROOM_UPDATE to all clients. If the room becomes 
    empty, it calls CleareParticipantRefs (which clears participants' CurrentRoom and sends S_CLOSED_ROOM) and 
    removes the room.

Q11: Why does OnLeaveRoom call CleareParticipantRefs when the room becomes empty, and what does it do?
A11: CleareParticipantRefs iterates over all participants and sets their CurrentRoom to null, then broadcasts 
    S_CLOSED_ROOM to all clients. This ensures that any clients that might still have a reference to the room 
    (e.g., due to lag) are cleaned up, and the UI updates correctly. It also removes the room from the server's 
    room dictionary.

Q12: How is room state consistency maintained across multiple clients when a participant joins or leaves?
A12: The server uses S_ROOM_UPDATE broadcasts to all clients after every change (join, leave, host change). 
    This ensures all clients have an up-to-date room list. The broadcasts are sent synchronously in the 
    handler methods, so there is no race condition within a single request. The use of locks (_server.SyncRoot) 
    in the underlying TcpServer ensures thread safety.

Q13: Why does OnListRooms include an unused "state" integer (always 0)?
A13: The protocol (Msg.S_ROOM_LIST) expects room state as the fifth parameter: (name, goal, host, playerCount, state). 
    Currently, the state is not used, but the field is included for future extension (e.g., to indicate if a room 
    is in-game or waiting). The code always sends 0 to maintain protocol compatibility.

Q14: How does OnCloseRoom ensure only the host can close a room?
A14: It checks if the client is in a room and gets the room. It does not explicitly verify that the client is the 
    host; it just calls CleareParticipantRefs and removes the room. This is a potential security issue – any 
    participant could close the room. The server should check `room.host == client.Name` before allowing closure. 
    The current code only sends an error if the room doesn't exist, but not if the client is not the host.

Q15: Why is there a separate `RoomChangeMessage` helper method?
A15: It centralises the creation and broadcasting of S_ROOM_UPDATE messages. This avoids duplicating the same 
    code in OnJoinRoom, OnLeaveRoom, and OnStartGame. If the format of S_ROOM_UPDATE changes, we only need to 
    modify one method.

Q16: How does OnStartGame hand off to GameState, and why?
A16: After setting room.GameStarted = true, broadcasting S_GAME_STARTED and S_ROOM_UPDATE, it calls 
    _gameState.StartGameForRoom(room). This transfers control to the GameState class, which manages the actual 
    game logic (turns, dice, scoring). This separation keeps lobby logic clean and allows the game logic to be 
    maintained independently.

Q17: Why does OnStartGame not validate that the game hasn't started already?
A17: It does check `if (room.GameStarted) return;`, so it prevents multiple start attempts.

Q18: How does the server handle messages if the client is not registered (client == null)?
A18: All handlers check `if (client == null)` and return an error using `_server.SendError(GetConnection(sender), 
    "Not registered")`. This protects against unauthenticated clients sending requests.

Q19: Why is there no async/await in LobbyState methods, even though network operations are involved?
A19: LobbyState methods are synchronous because the TCP server runs on a single update thread. The network 
    operations (sending messages) are non-blocking and fire-and-forget. There is no need for async/await in this 
    context. All operations are processed sequentially within the Update loop, ensuring thread safety and 
    predictable order.

Q20: How does the server ensure that room names are unique?
A20: In OnCreateRoom, it checks `_server.TryGetRoom(roomName, out _)` before creating the room. If the room exists, 
    it sends an error. This prevents duplicate room names.

Q21: What are the limitations of the current LobbyState design?
A21: Several limitations:
      - OnCreateRoom broadcasts S_CREATED_ROOM instead of sending it privately, which may break the protocol.
      - OnCloseRoom does not verify the client is the host.
      - The "state" field in S_ROOM_LIST is unused but required; this could be improved.
      - No explicit handling of game state transitions (e.g., if game ends, clients return to lobby).
      - No persistence; rooms are lost on server restart.
      - Room participants are stored as a list; lookups by ID could be O(n). Better to use a dictionary.

Q22: How could you improve the code to fix the OnCreateRoom broadcasting issue?
A22: Change OnCreateRoom to send S_CREATED_ROOM privately to the creator, then broadcast S_ROOM_UPDATE to all 
    clients with the new room details. This aligns with the protocol and keeps the room list synchronised.

Q23: Why use `_server.GetClientByEndpoint(sender)` instead of passing the client ID in the message?
A23: The OSC message does not contain the client ID; it's sent via TCP connection. The server uses the remote 
    endpoint to identify the connection and look up the client. This is the standard pattern for TCP servers; 
    the endpoint uniquely identifies the socket.

Q24: How does the server handle multiple clients sending requests simultaneously?
A24: The server's Update loop runs on a single thread, processing one connection at a time. All handler methods 
    are called sequentially within that loop. However, the underlying TcpNetworkConnection may have its own 
    receive buffer, but the processing is serialised. The use of locks (`_server.SyncRoot`) in TcpServer when 
    modifying shared collections (rooms, clients) ensures thread safety if the dispatcher were ever to call 
    handlers from a different thread.

Q25: Why are the handler methods public, even though they are only used internally?
A25: They are public because they are referenced by the OSC dispatcher via delegates. However, they could be 
    private if we used reflection or lambda registration. Keeping them public is fine and allows for easier 
    testing and debugging.
*/ 