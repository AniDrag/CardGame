using Newtonsoft.Json;
using System;
using System.Text;
using UnityEngine;
public static class MessageSerializer
{
    /// <summary> Serialize any NetworkMessage to a byte array (UTF8 JSON) </summary>
    public static byte[] Serialize(NetworkMessage msg)
    {
        string json = JsonUtility.ToJson(msg);
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary> Deserialize a byte array back to a NetworkMessage (dynamic type based on T field) </summary>
    public static NetworkMessage DeserializeDynamic(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);

        // First, peek at the "T" field to know the concrete type
        var typeInfo = JsonUtility.FromJson<TypeHolder>(json);
        if (typeInfo == null || string.IsNullOrEmpty(typeInfo.T))
            return null;

        // Instantiate the correct message type using JsonUtility
        return typeInfo.T switch
        {
            "CONN"  => JsonUtility.FromJson<RequestConnect>(json),
            "CRRM"  => JsonUtility.FromJson<RequestCreateRoom>(json),
            "JNRM"  => JsonUtility.FromJson<RequestJoinRoom>(json),
            "RDY"   => JsonUtility.FromJson<RequestReady>(json),
            "LVRM"  => JsonUtility.FromJson<RequestLeaveRoom>(json),
            "PC"    => JsonUtility.FromJson<RequestPlayCard>(json),
            "ATK"   => JsonUtility.FromJson<RequestAttack>(json),
            "ET"    => JsonUtility.FromJson<RequestEndTurn>(json),
            "CONC"  => JsonUtility.FromJson<RequestConcede>(json),
            "REMT" => JsonUtility.FromJson<RequestRematch>(json),
            "CHAT" => JsonUtility.FromJson<RequestChat>(json),
            //"ET"    => JsonUtility.FromJson<RequestEndTurn>(json),
            //"ET"    => JsonUtility.FromJson<RequestEndTurn>(json),
            _ => null
        };
    }

    // Helper class only used to read the "T" discriminator
    [Serializable]
    private class TypeHolder
    {
        public string T;
    }

    public enum ServerState
    {
        Menu,
        Lobby,
        Game,
    }
}