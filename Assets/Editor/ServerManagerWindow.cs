using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public class ServerManagerWindow : EditorWindow
{
    // List of all server configs loaded from assets
    private List<ServerConfig> serverConfigs = new List<ServerConfig>();

    // Map from config to its running process
    private Dictionary<ServerConfig, Process> runningProcesses = new Dictionary<ServerConfig, Process>();

    // Temporary fields for creating a new server
    private string newNameField = "";
    private int newPortField = 50001;
    private string newExePathField = "Tools/";

    [MenuItem("Window/Servers")]
    public static void ShowWindow()
    {
        GetWindow<ServerManagerWindow>("Servers");
    }

    // Ensure all servers are stopped when Unity quits
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
        // Find all ScriptableObjects of type ServerConfigSO
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
        // === Header ===
        GUILayout.Label("Server Management", EditorStyles.boldLabel);

        // === List existing servers ===
        foreach (var config in serverConfigs)
        {
            EditorGUILayout.BeginHorizontal();

            // Editable name
            EditorGUI.BeginChangeCheck();
            config.serverName = EditorGUILayout.TextField(config.serverName, GUILayout.Width(150));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            // Editable port
            EditorGUI.BeginChangeCheck();
            config.port = EditorGUILayout.IntField(config.port, GUILayout.Width(80));
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

            // Delete button: disabled while running
            GUI.enabled = !isRunning;
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                StopServer(config); // just in case
                string path = AssetDatabase.GetAssetPath(config);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                // Close the layout block before breaking
                EditorGUILayout.EndHorizontal();
                LoadServerConfigs();
                Repaint();
                break;
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        // === Separator ===
        GUILayout.Space(20);

        // === Create new server section ===
        GUILayout.Label("Create New Server", EditorStyles.boldLabel);
        newNameField = EditorGUILayout.TextField("Server Name", newNameField);
        newPortField = EditorGUILayout.IntField("Port", newPortField);
        EditorGUILayout.BeginHorizontal();
        newExePathField = EditorGUILayout.TextField("Executable Path", newExePathField);
        if (GUILayout.Button("Browse...", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("Select Server Executable", "", "exe");
            if (!string.IsNullOrEmpty(path))
            {
                // Try to make it relative to the project root
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../"));
                if (path.StartsWith(projectRoot))
                {
                    newExePathField = path.Substring(projectRoot.Length).Replace('\\', '/');
                }
                else
                {
                    // Store absolute path – but warn the user
                    newExePathField = path;
                    EditorUtility.DisplayDialog("Warning", "The selected file is outside the Unity project. The path will be stored as absolute, which may not work on other machines.", "OK");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Create Server Asset"))
        {
            if (!string.IsNullOrEmpty(newNameField) && newPortField > 0 && !string.IsNullOrEmpty(newExePathField))
            {
                CreateServerAsset(newNameField, newPortField, newExePathField);
                // Clear fields
                newNameField = "";
                newPortField = 50001;
                newExePathField = "Tools/";
                // Reload the list to show the new asset
                LoadServerConfigs();
                Repaint();
            }
            else
            {
                EditorUtility.DisplayDialog("Invalid Input", "Please fill all fields.", "OK");
            }
        }
        
        // === Stop all button ===
        GUILayout.Space(10);
        if (GUILayout.Button("Stop All Servers"))
        {
            StopAllServers();
        }
        if (GUILayout.Button("Refresh List"))
        {
            LoadServerConfigs();
            Repaint();
        }
    }

    private void StartServer(ServerConfig config)
    {
        // Build full path (relative to project root)
        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../", config.executablePath));

        if (!File.Exists(fullPath))
        {
            UnityEngine.Debug.LogError($"Server executable not found at {fullPath}");
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fullPath,
            Arguments = $"--port {config.port}",  // adjust if your server expects different arguments
            UseShellExecute = false,
            CreateNoWindow = true,   // set false if you want to see console windows
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        Process process = new Process { StartInfo = startInfo };

        // Forward output to Unity console
        process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                UnityEngine.Debug.Log($"[{config.serverName}] {args.Data}");
        };
        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                UnityEngine.Debug.LogError($"[{config.serverName}] {args.Data}");
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            runningProcesses[config] = process;

            UnityEngine.Debug.Log($"Started {config.serverName} on port {config.port}");
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
                    process.WaitForExit(5000); // wait up to 5 seconds
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
        // Copy keys to a list to avoid modification during iteration
        foreach (var config in new List<ServerConfig>(runningProcesses.Keys))
        {
            StopServer(config);
        }
    }

    private void CreateServerAsset(string name, int port, string exePath)
    {
        // Create a new instance of the ScriptableObject
        ServerConfig config = ScriptableObject.CreateInstance<ServerConfig>();
        config.serverName = name;
        config.port = port;
        config.executablePath = exePath;

        // Ensure the target folder exists
        string folder = "Assets/ServerConfigs";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "ServerConfigs");
        }

        // Generate a unique asset path
        string assetPath = $"{folder}/{name}.asset";
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        // Save the asset
        AssetDatabase.CreateAsset(config, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UnityEngine.Debug.Log($"Created server config asset at {assetPath}");
    }

    // Optional: clean up when window is destroyed (though quitting handler already does it)
    private void OnDestroy()
    {
        StopAllServers();
    }
}