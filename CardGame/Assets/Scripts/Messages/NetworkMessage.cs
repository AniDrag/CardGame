using System;
using System.Collections.Generic;

/*
 
 Server Menu:
    we are making a name and conneting to server.

Client -> Server: RequestConnect (with player name)
Server -> Client: ReplieConnected (succes bool -> true? scene manaher next scene)
 
Lobby / Room selection:
    - create room request.
        - Room name,
        - options (starting health, mana, special cards, etc.)
    - join room request (room id or name)
    - ready/unready toggle
    - disconnect from server.

Client -> Server: RequestCreateRoom (with options)
Server -> Client: ReplieRoomCreated (Succes bool), 
Server -> All Clients: ReplieRoomList (List<GameRoom>)

Client -> Server: RequestJoinRoom (room id)
Server -> Client: ReplieRoomJoined (success bool? see room data and can press ready.)
server -> all clients: ReplieRoomList (updated list with player counts)

Client -> Server: RequestReady (bool ready)
server -> clients: ReplieRoomJoined (bool success)
server -> all clients in room: ReplieRoomJoined (int readyCoutn)

server -> clients in room: StartGame (initial gamestate)
client -> server: StartGameReplie(success bool? scene switch to game)

Game State:
    - client sends actions (play card, attack, end turn, etc.)
    - server validates and updates game state, then broadcasts changes to clients.
    - server sends full game state at start, then deltas for changes.

client -> server: PlayCard(int handIDX, int[] pos(row, col))
server -> client: RepliePlayCard(bool succes? x : Error( Not your turn / cannot do action))
server -> clients in room: UpdateGameState(string changes)(deserialize on client correctly)

client -> server: requestDrawCard(int amount = 1)
server -> client: ReplieDrawCard(bool succes? int[cardID]) (deserialize on player what card we got.)
server -> opponent: BrodcastOponentCardDraw(int currOponentCardCount) (triggered every time we play a card or draw)

 
 
 
 
 
 
 
 
 
 
 
 
 */












#region BASE


[Serializable]
public abstract class NetworkMessage
{
    public string T;  // Type discriminator

    protected NetworkMessage() { }
    protected NetworkMessage(string type) { T = type; }
}

#endregion

#region ======================== MENU ========================
[Serializable]
public class RequestConnect : NetworkMessage // send by client to server to connect with a player name
{
    public string N;  // playerName

    public RequestConnect() : base("CONN") { }
    public RequestConnect(string playerName) : base("CONN") { N = playerName; }
}

[Serializable]
public class ReplieConnected : NetworkMessage // response from server to client confirming connection and providing client ID
{
    public int CID;  // clientId

    public ReplieConnected() : base("CONNED") { }
    public ReplieConnected(int clientId) : base("CONNED") { CID = clientId; }
}
#endregion

#region ======================== LOBBY / ROOM ========================
[Serializable]
public class RequestCreateRoom : NetworkMessage // client request to create a room with specific options
{
    public RoomOpt O;  // options

    public RequestCreateRoom() : base("CRRM") { }
    public RequestCreateRoom(RoomOpt options) : base("CRRM") { O = options; }
}

[Serializable]
public class ReplieRoomCreated : NetworkMessage // server response confirming room creation and providing room ID
{
    public int RID;  // roomId

    public ReplieRoomCreated() : base("RCRT") { }
    public ReplieRoomCreated(int roomId) : base("RCRT") { RID = roomId; }
}

[Serializable]
public class RequestJoinRoom : NetworkMessage // client request to join a specific room 
{
    public int RID;  // roomId
    public string N; // playerName

    public RequestJoinRoom() : base("JNRM") { }
    public RequestJoinRoom(int roomId, string playerName) : base("JNRM") { RID = roomId; N = playerName; }
}

[Serializable]
public class ReplieRoomJoined : NetworkMessage// replie
{
    public List<PlayerInfo> P;  // players

    public ReplieRoomJoined() : base("RJND") { }
    public ReplieRoomJoined(List<PlayerInfo> players) : base("RJND") { P = players; }

    [Serializable]
    public struct PlayerInfo
    {
        public string N;   // name
        public bool R;     // ready
    }
}

[Serializable]
public class ReplieRoomList : NetworkMessage// ecoed when creating a room list, and when joining lobby scene
{
    public List<RoomInfo> R;  // rooms

    public ReplieRoomList() : base("RMLS") { }
    public ReplieRoomList(List<RoomInfo> rooms) : base("RMLS") { R = rooms; }

    [Serializable]
    public struct RoomInfo
    {
        public int ID;       // roomId
        public string N;     // roomName (optional, can be auto-generated)
        public int PC;       // playerCount
        public RoomOpt O;    // options
    }
}

[Serializable]
public class RoomOpt
{
    public bool USC;   // useSpecialCard
    public bool UEFC;  // useEffectCard
    public int SH;     // startingHealth
    public int SM;     // startingMana
    public int MRT;    // manaRegenPerTurn
}

[Serializable]
public class RequestReady : NetworkMessage //
{
    public RequestReady() : base("RDY") { }
}

[Serializable]
public class RequestLeaveRoom : NetworkMessage
{
    public RequestLeaveRoom() : base("LVRM") { }
}
#endregion

#region ======================== GAME START & STATE ========================
[Serializable]
public class ReplieGameStarted : NetworkMessage
{
    public GameState State;  // initial full state

    public ReplieGameStarted() : base("GSTR") { }
    public ReplieGameStarted(GameState state) : base("GSTR") { State = state; }
}

[Serializable]
public class ReplieGameStateDelta : NetworkMessage
{
    public List<StateChange> C;  // changes

    public ReplieGameStateDelta() : base("GSD") { }
    public ReplieGameStateDelta(List<StateChange> changes) : base("GSD") { C = changes; }

    [Serializable]
    public struct StateChange
    {
        public string P;   // path (e.g., "p.H", "board.0.H")
        public int V;      // new value
        // For non-int changes, use object or separate field; but keep simple
    }
}

[Serializable]
public class ReplieGameOver : NetworkMessage
{
    public int W;   // winner: 0 = player1, 1 = player2, -1 = draw

    public ReplieGameOver() : base("GOVR") { }
    public ReplieGameOver(int winner) : base("GOVR") { W = winner; }
}
#endregion

#region ======================== GAME ACTIONS ========================
[Serializable]
public class RequestPlayCard : NetworkMessage
{
    public int HI;   // handIndex
    public int? TS;  // targetSlot (nullable, for spells/placement)

    public RequestPlayCard() : base("PC") { }
    public RequestPlayCard(int handIndex, int? targetSlot = null) : base("PC") { HI = handIndex; TS = targetSlot; }
}

[Serializable]
public class RequestAttack : NetworkMessage
{
    public int AS;   // attackerSlot
    public int TS;   // targetSlot

    public RequestAttack() : base("ATK") { }
    public RequestAttack(int attackerSlot, int targetSlot) : base("ATK") { AS = attackerSlot; TS = targetSlot; }
}

[Serializable]
public class ActivateAbility : NetworkMessage // questionable
{
    public int SI;   // sourceSlot (which monster/hero)
    public int AI;   // abilityIndex (if a unit has multiple)
    public List<int> Targets;  // target slots or IDs

    public ActivateAbility() : base("AB") { }
    public ActivateAbility(int sourceSlot, int abilityIndex, List<int> targets) : base("AB")
    {
        SI = sourceSlot;
        AI = abilityIndex;
        Targets = targets;
    }
}

[Serializable]
public class TargetSelection : NetworkMessage
{
    public int CID;       // cardInstanceId (from the effect that triggered)
    public List<int> Targets;  // chosen targets (slots / player indices)

    public TargetSelection() : base("TARG") { }
    public TargetSelection(int cardInstanceId, List<int> targets) : base("TARG")
    {
        CID = cardInstanceId;
        Targets = targets;
    }
}

[Serializable]
public class Discard : NetworkMessage
{
    public int HI;   // handIndex

    public Discard() : base("DISC") { }
    public Discard(int handIndex) : base("DISC") { HI = handIndex; }
}

[Serializable]
public class RequestEndTurn : NetworkMessage
{
    public RequestEndTurn() : base("ET") { }
}

[Serializable]
public class RequestConcede : NetworkMessage
{
    public RequestConcede() : base("CONC") { }
}

[Serializable]
public class Mulligan : NetworkMessage
{
    public List<int> KeepIndices;  // indices of cards to keep (others are redrawn)

    public Mulligan() : base("MULL") { }
    public Mulligan(List<int> keepIndices) : base("MULL") { KeepIndices = keepIndices; }
}

[Serializable]
public class RequestRematch : NetworkMessage
{
    public bool Accept;  // true = accept rematch

    public RequestRematch() : base("REMT") { }
    public RequestRematch(bool accept) : base("REMT") { Accept = accept; }
}
#endregion

#region ======================== SYSTEM & UTILITY ========================
[Serializable]
public class ReplieError : NetworkMessage
{
    public string Msg;

    public ReplieError() : base("ERR") { }
    public ReplieError(string message) : base("ERR") { Msg = message; }
}

[Serializable]
public class RequestChat : NetworkMessage
{
    public string N;   // playerName (or empty for system)
    public string Msg;

    public RequestChat() : base("CHAT") { }
    public RequestChat(string playerName, string message) : base("CHAT") { N = playerName; Msg = message; }
}

[Serializable]
public class Emote : NetworkMessage
{
    public int EID;   // emoteId (predefined list)

    public Emote() : base("EM") { }
    public Emote(int emoteId) : base("EM") { EID = emoteId; }
}

[Serializable]
public class Ping : NetworkMessage
{
    public long Time;  // client timestamp

    public Ping() : base("PING") { }
    public Ping(long time) : base("PING") { Time = time; }
}

[Serializable]
public class Pong : NetworkMessage
{
    public long Time;  // echo back the ping timestamp

    public Pong() : base("PONG") { }
    public Pong(long time) : base("PONG") { Time = time; }
}

[Serializable]
public class RequestReconnect : NetworkMessage
{
    public int CID;    // clientId (if known)
    public int RID;    // roomId (if in a game)

    public RequestReconnect() : base("REC") { }
    public RequestReconnect(int clientId, int roomId) : base("REC") { CID = clientId; RID = roomId; }
}

[Serializable]
public class Ack : NetworkMessage
{
    public int SEQ;    // sequence number of acknowledged message

    public Ack() : base("ACK") { }
    public Ack(int seq) : base("ACK") { SEQ = seq; }
}
#endregion