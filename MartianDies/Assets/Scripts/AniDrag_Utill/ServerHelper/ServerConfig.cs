using System.Diagnostics;
using UnityEngine;

[CreateAssetMenu(fileName = "NewServer", menuName = "Game/Server Config")]
public class ServerConfig : ScriptableObject
{
    public string serverName;
    public string executablePath;  
}