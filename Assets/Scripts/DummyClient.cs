using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DummyClient : MonoBehaviour
{
    public static DummyClient Instance { get; private set; }

    private bool isConnected = false;
    private string requestedServerIpAddress;
    private string requestedPlayerName;

    private string myConnectionId;
    private DummyServer.ClientData myClientData;

    public string myPlayerName { get; private set; }

    public Action<string> MainMenuUpdateStatus;
    public Action MainMenuConnectonFaliure;

    public Action<List<object>> OnRoomListReceived;
    public Action OnRoomLeft;


    public Action<string> OnActionFailed;

    void Awake()
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

    #region Main Menu Connection Logic
    public void ConnectToServer(string playerName, string serverIpAddress)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogError("Please enter a name first!");
            return;
        }
        if (string.IsNullOrEmpty(serverIpAddress))
        {
            Debug.LogError("Please enter a Server IP address first!");
            return;
        }

        requestedServerIpAddress = serverIpAddress;
        requestedPlayerName = playerName;

        MainMenuUpdateStatus?.Invoke($"Connecting to server at {serverIpAddress}...");

        InvokeDelayed(() =>
        {
            if (DummyServer.Instance != null && DummyServer.Instance.ServerIpAddress == serverIpAddress)
            {
                DummyServer.Instance.RegisterClient(requestedPlayerName, this);
            }
            else
            {
                MainMenuUpdateStatus?.Invoke($"No server active at IP: {serverIpAddress}");
                MainMenuConnectonFaliure?.Invoke();
            }
        }, 1f);
    }

    public void SetConnectionId(string connectionId)
    {
        myConnectionId = connectionId;
        myClientData = DummyServer.Instance?.GetClientByConnectionId(connectionId);
    }

    public void OnRegistrationComplete(bool success)
    {
        if (success)
        {
            isConnected = true;
            myPlayerName = requestedPlayerName;
            MainMenuUpdateStatus?.Invoke($"Registered successfully! Loading matchmaking scene...");

            InvokeDelayed(() => { LoadScene.Instance.LOADSCENE(1); }, 1f);
        }
        else
        {
            MainMenuUpdateStatus?.Invoke($"Failed to register at IP: {requestedServerIpAddress}");
            MainMenuConnectonFaliure?.Invoke();
        }
    }
    #endregion


    #region Room List Logic

    #endregion

    #region Helper Methods
    public void InvokeDelayed(Action action, float delay)
    {
        StartCoroutine(DelayedAction(action, delay));
    }

    private IEnumerator DelayedAction(Action action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    }

    private void OnDestroy()
    {
        if (isConnected && DummyServer.Instance != null && myClientData != null)
        {
            DummyServer.Instance.DisconnectClient(myClientData);
        }
    }
    #endregion
}