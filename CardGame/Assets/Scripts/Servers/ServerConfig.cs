using System.Diagnostics;
using UnityEngine;

[CreateAssetMenu(fileName = "NewServer", menuName = "Game/Server Config")]
public class ServerConfig: ScriptableObject
{
    public string serverName;
    public int port;
    public string executablePath; // relative to project root or full path

    // We'll store the running process here (not serialized)
    [System.NonSerialized]
    public Process Process;
}