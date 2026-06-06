using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public class ServerManagerWindow : EditorWindow
{
    private List<ServerConfig> serverConfigs = new List<ServerConfig>();
    private Dictionary<ServerConfig, Process> runningProcesses = new Dictionary<ServerConfig, Process>();

    // New server creation fields – no port
    private string newNameField = "";
    private string newExePathField = "Tools/";

    [MenuItem("AniDrag API/Server helpers")]
    public static void ShowWindow()
    {
        GetWindow<ServerManagerWindow>("Servers");
    }

    [InitializeOnLoadMethod]
    private static void OnEditorLoad()
    {
        EditorApplication.quitting += () =>
        {
            var window = GetWindow<ServerManagerWindow>(false);
            if (window != null)
                window.StopAllServers();
        };
    }

    private void OnEnable()
    {
        LoadServerConfigs();
    }

    private void LoadServerConfigs()
    {
        serverConfigs.Clear();
        string[] guids = AssetDatabase.FindAssets("t:ServerConfig");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ServerConfig config = AssetDatabase.LoadAssetAtPath<ServerConfig>(path);
            if (config != null)
                serverConfigs.Add(config);
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Server Management", EditorStyles.boldLabel);

        // List existing servers – name only, no port
        foreach (var config in serverConfigs)
        {
            EditorGUILayout.BeginHorizontal();

            // Editable name
            EditorGUI.BeginChangeCheck();
            config.serverName = EditorGUILayout.TextField(config.serverName, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            bool isRunning = runningProcesses.ContainsKey(config) && !runningProcesses[config].HasExited;

            if (isRunning)
            {
                if (GUILayout.Button("Stop", GUILayout.Width(60))) StopServer(config);
            }
            else
            {
                if (GUILayout.Button("Start", GUILayout.Width(60))) StartServer(config);
            }

            GUI.enabled = !isRunning;
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                StopServer(config);
                string path = AssetDatabase.GetAssetPath(config);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                EditorGUILayout.EndHorizontal();
                LoadServerConfigs();
                Repaint();
                break;
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(20);
        GUILayout.Label("Create New Server", EditorStyles.boldLabel);

        // Name field
        newNameField = EditorGUILayout.TextField("Server Name", newNameField);

        // Executable path with browse button (no port field)
        EditorGUILayout.BeginHorizontal();
        newExePathField = EditorGUILayout.TextField("Executable Path", newExePathField);
        if (GUILayout.Button("Browse...", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("Select Server Executable", "", "exe");
            if (!string.IsNullOrEmpty(path))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../"));
                if (path.StartsWith(projectRoot))
                {
                    newExePathField = path.Substring(projectRoot.Length).Replace('\\', '/');
                }
                else
                {
                    newExePathField = path;
                    EditorUtility.DisplayDialog("Warning", "Absolute path stored – may not work on other machines.", "OK");
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Create Server Asset"))
        {
            if (!string.IsNullOrEmpty(newNameField) && !string.IsNullOrEmpty(newExePathField))
            {
                CreateServerAsset(newNameField, newExePathField);
                newNameField = "";
                newExePathField = "Tools/";
                LoadServerConfigs();
                Repaint();
            }
            else
            {
                EditorUtility.DisplayDialog("Invalid Input", "Please fill both fields.", "OK");
            }
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Stop All Servers")) StopAllServers();
        if (GUILayout.Button("Refresh List"))
        {
            LoadServerConfigs();
            Repaint();
        }
    }

    private void StartServer(ServerConfig config)
    {
        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../", config.executablePath));

        if (!File.Exists(fullPath))
        {
            UnityEngine.Debug.LogError($"Server executable not found at {fullPath}");
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fullPath,
            Arguments = "",                 // No port argument – your server uses hardcoded port
            UseShellExecute = true,         // Shows native console window
            CreateNoWindow = false,         // Ensures window is visible
        };

        Process process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
            runningProcesses[config] = process;
            UnityEngine.Debug.Log($"Started {config.serverName} (console window visible)");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to start server: {e.Message}");
        }
    }

    private void StopServer(ServerConfig config)
    {
        if (runningProcesses.TryGetValue(config, out Process process))
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    process.Dispose();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"Error stopping server: {e.Message}");
                }
            }
            runningProcesses.Remove(config);
            UnityEngine.Debug.Log($"Stopped {config.serverName}");
        }
    }

    private void StopAllServers()
    {
        foreach (var config in new List<ServerConfig>(runningProcesses.Keys))
        {
            StopServer(config);
        }
    }

    private void CreateServerAsset(string name, string exePath)
    {
        ServerConfig config = ScriptableObject.CreateInstance<ServerConfig>();
        config.serverName = name;
        config.executablePath = exePath;

        string folder = "Assets/ServerConfigs";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "ServerConfigs");

        string assetPath = $"{folder}/{name}.asset";
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        AssetDatabase.CreateAsset(config, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UnityEngine.Debug.Log($"Created server config asset at {assetPath}");
    }

    private void OnDestroy()
    {
        StopAllServers();
    }
}