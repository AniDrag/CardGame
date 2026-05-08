using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ClientRoomSelectionManager : MonoBehaviour
{
    [Header("Top Bar")]
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private Button disconnectButton;

    [Header("Create Room Panel")]
    [SerializeField] private GameObject createRoomPanel;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_InputField playerHealthInput;
    [SerializeField] private TMP_InputField mpRegenInput;
    [SerializeField] private TMP_Dropdown restrictionDropdown;
    [SerializeField] private TMP_InputField specialBuffInput;
    [SerializeField] private Button confirmCreateRoomButton;
    [SerializeField] private Button cancelCreateRoomButton;
    [SerializeField] private Button openCreateRoomButton;

    [Header("Room List")]
    [SerializeField] private Transform roomListContainer;
    [SerializeField] private GameObject roomItemPrefab; // Prefab with Text for room info and Join button

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    private List<GameObject> roomItems = new List<GameObject>();

    private void Start()
    {
        // Set username
        if (usernameText != null && DummyClient.Instance != null)
            usernameText.text = $"Player: {DummyClient.Instance.myPlayerName}";

        // Disconnect button
        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(OnDisconnectClicked);

        // Create room UI
        if (openCreateRoomButton != null)
            openCreateRoomButton.onClick.AddListener(() => createRoomPanel.SetActive(true));
        if (cancelCreateRoomButton != null)
            cancelCreateRoomButton.onClick.AddListener(() => createRoomPanel.SetActive(false));
        if (confirmCreateRoomButton != null)
            confirmCreateRoomButton.onClick.AddListener(OnCreateRoomConfirmed);

        // Default values
        if (playerHealthInput != null) playerHealthInput.text = "30";
        if (mpRegenInput != null) mpRegenInput.text = "2";
        if (restrictionDropdown != null)
        {
            restrictionDropdown.ClearOptions();
            restrictionDropdown.AddOptions(new List<string> { "None", "NoSpells", "OnlyBeasts", "NoBuffs" });
        }

        // Subscribe to client events
        if (DummyClient.Instance != null)
        {
            DummyClient.Instance.OnRoomListReceived += RefreshRoomList;
            //DummyClient.Instance.OnRoomCreated += OnRoomCreated;
            //DummyClient.Instance.OnRoomJoined += OnRoomJoined;
            DummyClient.Instance.OnRoomLeft += OnRoomLeft;
            DummyClient.Instance.OnActionFailed += ShowStatusMessage;
        }

        // Request initial room list
        //DummyClient.Instance?.RequestRoomList();
    }

    private void OnDisconnectClicked()
    {
        //DummyClient.Instance?.DisconnectFromServer();
    }

    private void OnCreateRoomConfirmed()
    {
        string roomName = roomNameInput.text;
        if (string.IsNullOrEmpty(roomName))
        {
            ShowStatusMessage("Room name cannot be empty");
            return;
        }

        int playerHealth = int.TryParse(playerHealthInput.text, out int ph) ? ph : 30;
        int mpRegen = int.TryParse(mpRegenInput.text, out int mp) ? mp : 2;
        string restriction = restrictionDropdown.options[restrictionDropdown.value].text;
        string specialBuff = specialBuffInput.text;

        //DummyClient.Instance.CreateRoom(roomName, playerHealth, mpRegen, restriction, specialBuff);
        createRoomPanel.SetActive(false);
    }

    private void RefreshRoomList(List<object> rooms)
    {
        // Clear existing items
        foreach (var item in roomItems)
            Destroy(item);
        roomItems.Clear();

        foreach (var roomObj in rooms)
        {
            // Convert to dictionary (since we sent anonymous object)
            var room = (Dictionary<string, object>)roomObj;
            string roomId = room["roomId"].ToString();
            string roomName = room["roomName"].ToString();
            int currentPlayers = (int)room["currentPlayers"];
            int maxPlayers = (int)room["maxPlayers"];
            int playerHealth = (int)room["playerHealth"];
            int mpRegen = (int)room["mpRegenPerTurn"];
            string restrictions = room["cardTypeRestrictions"].ToString();
            string specialBuff = room["specialBuff"].ToString();

            // Instantiate UI item
            GameObject item = Instantiate(roomItemPrefab, roomListContainer);
            roomItems.Add(item);

            // Find text components (adjust names based on your prefab)
            TMP_Text[] texts = item.GetComponentsInChildren<TMP_Text>();
            if (texts.Length >= 1) texts[0].text = $"{roomName} ({currentPlayers}/{maxPlayers})";
            if (texts.Length >= 2) texts[1].text = $"Health: {playerHealth} | MP Regen: {mpRegen} | Restrictions: {restrictions} | Buff: {specialBuff}";

            Button joinBtn = item.GetComponentInChildren<Button>();
            if (joinBtn != null)
            {
                string id = roomId; // capture
                //joinBtn.onClick.AddListener(() => DummyClient.Instance.JoinRoom(id));
            }
        }

        ShowStatusMessage($"Found {rooms.Count} available rooms");
    }

    private void OnRoomCreated(string roomId, string roomName)
    {
        ShowStatusMessage($"Room '{roomName}' created! Joining...");
        // The server automatically puts you in the room, so we might not need to call Join.
        // But we can show a lobby scene later. For now, just refresh list.
        //DummyClient.Instance.RequestRoomList();
    }

    private void OnRoomJoined(string roomId, string roomName)
    {
        ShowStatusMessage($"Joined room '{roomName}'!");
        // TODO: Load lobby scene or show lobby UI inside this scene
        // For now, just refresh list
        //DummyClient.Instance.RequestRoomList();
    }

    private void OnRoomLeft()
    {
        ShowStatusMessage("Left the room");
        //DummyClient.Instance.RequestRoomList();
    }

    private void ShowStatusMessage(string msg)
    {
        if (statusText != null)
        {
            statusText.text = msg;
            CancelInvoke(nameof(ClearStatus));
            Invoke(nameof(ClearStatus), 3f);
        }
        Debug.Log($"[UI] {msg}");
    }

    private void ClearStatus()
    {
        if (statusText != null) statusText.text = "";
    }

    private void OnDestroy()
    {
        if (DummyClient.Instance != null)
        {
            DummyClient.Instance.OnRoomListReceived -= RefreshRoomList;
           //DummyClient.Instance.OnRoomCreated -= OnRoomCreated;
           //DummyClient.Instance.OnRoomJoined -= OnRoomJoined;
            DummyClient.Instance.OnRoomLeft -= OnRoomLeft;
            DummyClient.Instance.OnActionFailed -= ShowStatusMessage;
        }
    }
}