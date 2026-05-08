using UnityEngine;
using System.Collections.Generic;
using System;

public class DummyServer : MonoBehaviour
{
    public static DummyServer Instance { get; private set; }

    public readonly string ServerIpAddress = "127.0.0.1";
    #region Data Structures
    [Serializable]
    public class ClientData
    {
        public string clientName;
        public string connectionId;
        public bool isLookingForMatch;
        public DummyClient clientReference;
        public string currentRoomId; // Track which room client is in
    }

    [Serializable]
    public class GameRoom
    {
        public string roomId;
        public string roomName;
        public string hostConnectionId;
        public List<string> playerConnectionIds = new List<string>();
        public int maxPlayers = 4;
        // Room settings
        public int playerHealth = 30;
        public int mpRegenPerTurn = 2;
        public string cardTypeRestrictions = "None"; // e.g., "NoSpells", "OnlyBeasts"
        public string specialBuff = "None";

        public bool IsFull => playerConnectionIds.Count >= maxPlayers;
        public int CurrentPlayers => playerConnectionIds.Count;
    }
    #endregion


    private List<ClientData> clients = new List<ClientData>();
    private Dictionary<string, GameRoom> rooms = new Dictionary<string, GameRoom>();
    private int nextConnectionId = 1;
    private int nextRoomId = 1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #region MainMenu State Logic
    public void RegisterClient(string clientName, DummyClient clientRef)
    {
        string connectionId = "Client_" + nextConnectionId++;
        ClientData newClient = new ClientData
        {
            clientName = clientName,
            connectionId = connectionId,
            isLookingForMatch = false,
            clientReference = clientRef
        };

        clients.Add(newClient);
        Debug.Log($"[SERVER] {clientName} registered with ID: {connectionId}");

        clientRef.SetConnectionId(connectionId);
        clientRef.InvokeDelayed(() => clientRef.OnRegistrationComplete(true), 1f);
    }

    public void DisconnectClient(ClientData client)
    {
        if (clients.Contains(client))
        {
            clients.Remove(client);
            Debug.Log($"[SERVER] {client.clientName} disconnected");
        }
    }
    #endregion
    public void RequestRoomList(string connectionId, DummyClient clientRef)
    {
        ClientData client = GetClientByConnectionId(connectionId);
        if (client == null) return;

        List<object> roomList = new List<object>();
        foreach (var room in rooms.Values)
        {
            if (!room.IsFull) // Only send non-full rooms
            {
                roomList.Add(new
                {
                    roomId = room.roomId,
                    roomName = room.roomName,
                    currentPlayers = room.CurrentPlayers,
                    maxPlayers = room.maxPlayers,
                    playerHealth = room.playerHealth,
                    mpRegenPerTurn = room.mpRegenPerTurn,
                    cardTypeRestrictions = room.cardTypeRestrictions,
                    specialBuff = room.specialBuff
                });
            }
        }

        clientRef.OnRoomListReceived(roomList);
    }

    public void CreateRoom(string connectionId, string roomName, int playerHealth, int mpRegenPerTurn,
                           string cardTypeRestrictions, string specialBuff, DummyClient clientRef)
    {
        ClientData client = GetClientByConnectionId(connectionId);
        if (client == null)
        {
            clientRef.OnActionFailed("Client not found");
            return;
        }

        if (client.currentRoomId != null)
        {
            clientRef.OnActionFailed("You are already in a room. Leave first.");
            return;
        }

        string roomId = "Room_" + nextRoomId++;
        GameRoom newRoom = new GameRoom
        {
            roomId = roomId,
            roomName = roomName,
            hostConnectionId = connectionId,
            playerConnectionIds = new List<string> { connectionId },
            maxPlayers = 4,
            playerHealth = playerHealth,
            mpRegenPerTurn = mpRegenPerTurn,
            cardTypeRestrictions = cardTypeRestrictions,
            specialBuff = specialBuff
        };
        rooms[roomId] = newRoom;
        client.currentRoomId = roomId;

        Debug.Log($"[SERVER] {client.clientName} created room {roomName} (ID: {roomId})");
        //clientRef.OnRoomCreated(roomId, roomName);

        // Broadcast updated room list to all clients (optional)
        BroadcastRoomList();
    }

    public void JoinRoom(string connectionId, string roomId, DummyClient clientRef)
    {
        ClientData client = GetClientByConnectionId(connectionId);
        if (client == null)
        {
            clientRef.OnActionFailed("Client not found");
            return;
        }

        if (client.currentRoomId != null)
        {
            clientRef.OnActionFailed("You are already in a room. Leave first.");
            return;
        }

        if (!rooms.ContainsKey(roomId))
        {
            clientRef.OnActionFailed("Room does not exist");
            return;
        }

        GameRoom room = rooms[roomId];
        if (room.IsFull)
        {
            clientRef.OnActionFailed("Room is full");
            return;
        }

        room.playerConnectionIds.Add(connectionId);
        client.currentRoomId = roomId;

        Debug.Log($"[SERVER] {client.clientName} joined room {room.roomName}");
        //clientRef.OnRoomJoined(roomId, room.roomName);

        // Notify other players in the room (optional)
        BroadcastRoomList();
    }

    public void LeaveRoom(string connectionId, DummyClient clientRef)
    {
        ClientData client = GetClientByConnectionId(connectionId);
        if (client == null || client.currentRoomId == null) return;

        string roomId = client.currentRoomId;
        if (rooms.ContainsKey(roomId))
        {
            GameRoom room = rooms[roomId];
            room.playerConnectionIds.Remove(connectionId);

            // If room becomes empty, delete it
            if (room.playerConnectionIds.Count == 0)
            {
                rooms.Remove(roomId);
                Debug.Log($"[SERVER] Room {room.roomName} deleted (empty)");
            }
            else if (room.hostConnectionId == connectionId)
            {
                // Assign new host (first player in list)
                room.hostConnectionId = room.playerConnectionIds[0];
                Debug.Log($"[SERVER] New host for room {room.roomName}: {room.hostConnectionId}");
            }
        }
        client.currentRoomId = null;
        Debug.Log($"[SERVER] {client.clientName} left room");
        clientRef.OnRoomLeft();

        BroadcastRoomList();
    }

   //public void DisconnectClient(ClientData client)
   //{
   //    if (client.currentRoomId != null)
   //    {
   //        // Auto leave room before disconnecting
   //        LeaveRoom(client.connectionId, client.clientReference);
   //    }
   //
   //    if (clients.Contains(client))
   //    {
   //        clients.Remove(client);
   //        Debug.Log($"[SERVER] {client.clientName} disconnected");
   //    }
   //}

    private void BroadcastRoomList()
    {
        foreach (var client in clients)
        {
            RequestRoomList(client.connectionId, client.clientReference);
        }
    }
    #region Matchmaking State Logic


    #endregion

    public ClientData GetClientByConnectionId(string connectionId)
    {
        return clients.Find(c => c.connectionId == connectionId);
    }
}