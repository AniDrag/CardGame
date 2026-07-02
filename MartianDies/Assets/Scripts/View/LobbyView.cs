using AniDrag.EventBus;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyView : MonoBehaviour
{
    #region View References

    [Header("Player Info")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Button createRoomBtn;
    [SerializeField] private Button refreshRoomsButton;
    public Button disconnectBtn;

    [Header("Room List")]
    [SerializeField] private Transform content;
    [SerializeField] private GameObject prf_roomEntry;

    [Header("Panels")]
    public GameObject Panel_CreateRoom;
    public GameObject Panel_WaitingForHost;
    public GameObject Panel_HostRoom;

    #endregion

    #region State

    private readonly Dictionary<string, RoomEntryView> roomEntries = new();

    #endregion

    #region Event Bindings

    private EventBinding<EnableButtons> enableButtonsBinding;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        RegisterButtons();
        RegisterEvents();

        ShowLobby();
    }

    private void OnDestroy()
    {
        UnregisterButtons();
        UnregisterEvents();
    }

    #endregion

    #region Registration

    private void RegisterButtons()
    {
        createRoomBtn.onClick.AddListener(OnCreateRoomButtonClicked);
        disconnectBtn.onClick.AddListener(OnDisconnectButtonClicked);
        refreshRoomsButton.onClick.AddListener(OnRefreshRoomsButtonClicked);
    }

    private void UnregisterButtons()
    {
        createRoomBtn.onClick.RemoveListener(OnCreateRoomButtonClicked);
        disconnectBtn.onClick.RemoveListener(OnDisconnectButtonClicked);
        refreshRoomsButton.onClick.RemoveListener(OnRefreshRoomsButtonClicked);
    }

    private void RegisterEvents()
    {
        enableButtonsBinding = new EventBinding<EnableButtons>(SetLobbyButtonsInteractable);
        EventBus<EnableButtons>.Subscribe(enableButtonsBinding);
    }

    private void UnregisterEvents()
    {
        if (enableButtonsBinding != null)
            EventBus<EnableButtons>.Unsubscribe(enableButtonsBinding);
    }

    #endregion

    #region UI Events

    private void OnCreateRoomButtonClicked()
    {
        ToggleCreateRoomPanel();
    }

    private void OnDisconnectButtonClicked()
    {
        EventBus<Disconnect>.Publish(new Disconnect());
    }

    private void OnRefreshRoomsButtonClicked()
    {
        EventBus<RefreshRooms>.Publish(new RefreshRooms());
    }

    #endregion

    #region Player Info

    public void SetPlayerName(string name)
    {
        if (playerNameText != null)
            playerNameText.text = name;
    }

    #endregion

    #region Panels

    public void ShowLobby()
    {
        SetPanel(Panel_CreateRoom, false);
        SetPanel(Panel_WaitingForHost, false);
        SetPanel(Panel_HostRoom, false);
    }

    public void ShowHostRoom()
    {
        SetPanel(Panel_CreateRoom, false);
        SetPanel(Panel_WaitingForHost, false);
        SetPanel(Panel_HostRoom, true);
    }

    public void ShowWaitingForHost()
    {
        SetPanel(Panel_CreateRoom, false);
        SetPanel(Panel_WaitingForHost, true);
        SetPanel(Panel_HostRoom, false);
    }

    public void ToggleCreateRoomPanel()
    {
        bool isActive = Panel_CreateRoom != null && Panel_CreateRoom.activeSelf;

        SetPanel(Panel_CreateRoom, !isActive);
        SetPanel(Panel_WaitingForHost, false);
        SetPanel(Panel_HostRoom, false);
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    #endregion

    #region Room List

    public void ClearRoomList()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        roomEntries.Clear();
    }

    public void PopulateRoomList(List<RoomDataModel> rooms)
    {
        foreach (RoomDataModel room in rooms)
            UpdateRoomEntry(room);
    }

    public void UpdateRoomEntry(RoomDataModel room)
    {
        if (room == null)
            return;

        if (room.isInGame)
        {
            RemoveRoomEntry(room.roomName);
            return;
        }

        if (roomEntries.TryGetValue(room.roomName, out RoomEntryView entry))
        {
            entry.UpdateData(room);
            return;
        }

        CreateRoomEntry(room);
    }

    public void RemoveRoomEntry(string roomName)
    {
        if (!roomEntries.TryGetValue(roomName, out RoomEntryView entry))
            return;

        if (entry != null)
            Destroy(entry.gameObject);

        roomEntries.Remove(roomName);
    }

    private void CreateRoomEntry(RoomDataModel data)
    {
        GameObject go = Instantiate(prf_roomEntry, content);

        RoomEntryView entry = go.GetComponent<RoomEntryView>();
        entry.Initialize(data);

        roomEntries[data.roomName] = entry;
    }

    #endregion

    #region Button State

    private void SetLobbyButtonsInteractable(EnableButtons e)
    {
        if (createRoomBtn != null)
            createRoomBtn.interactable = e.isEnabled;

        if (refreshRoomsButton != null)
            refreshRoomsButton.interactable = e.isEnabled;

        // Disconnect intentionally stays usable.
        if (disconnectBtn != null)
            disconnectBtn.interactable = true;
    }

    #endregion
}