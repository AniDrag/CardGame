using AniDrag.EventBus;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyView : MonoBehaviour
{
    [Header("Player Info")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Button createRoomBtn;
    [SerializeField] private Button refreshRoomsButton;
    public Button disconnectBtn;

    [Header("Room List")]
    [SerializeField] private Transform content;
    [SerializeField] private GameObject prf_roomEntry;

    [Header("Room List")]
    public GameObject Panel_CreateRoom;
    public GameObject Panel_WaitingForHost;
    public GameObject Panel_HostRoom;

    private Dictionary<string, RoomEntryView> roomEntries = new();

    EventBinding<DisableButtons> disableButtonsBainding;

    private void Start()
    {
        createRoomBtn.onClick.AddListener(OnCreateBtnRoomPressed);
        disconnectBtn.onClick.AddListener(() => Client.Instance.Disconnect());
        refreshRoomsButton.onClick.AddListener(() => EventBus<RefreshRooms>.Publish(new RefreshRooms()));

        disableButtonsBainding = new EventBinding<DisableButtons>(DisableButtons);
        EventBus<DisableButtons>.Subscribe(disableButtonsBainding);

    }
    public void SetPlayerName(string name)
    {
        if (playerNameText != null) playerNameText.text = name;
    }
    void DisableButtons(DisableButtons e)
    {
        disconnectBtn.interactable = e.isEnabled;
        createRoomBtn.interactable = e.isEnabled;
        refreshRoomsButton.interactable = e.isEnabled;
    }


    #region Room Creation 
    public void ClearRoomList()
    {
        foreach (var entry in roomEntries.Values)
            Destroy(entry.gameObject);
        roomEntries.Clear();
    }
    public RoomEntryView CreateRoomEntry(RoomDataModel data)
    {
        GameObject go = Instantiate(prf_roomEntry, content);
        var entry = go.GetComponent<RoomEntryView>();
        entry.Initialize(data);
        roomEntries.Add(data.roomName, entry);
        return entry;
    }

    // First Call when opening this view this request server for a lsist of eneries
    public void PopulateRoomList(List<RoomDataModel> pRoomEntries)
    {
        foreach( var entry in pRoomEntries)
            CreateRoomEntry(entry);
    }

    public void UpdateRoomsList(List<RoomDataModel> pRoomEntries)
    {
        foreach(var entry in pRoomEntries)
        {
            if(entry.isInGame)
                roomEntries.Remove(entry.roomName);
            roomEntries[entry.roomName].UpdateParticipants(entry.participantCount);
        }
    }
    public void UpdateRoomVisuals(string roomName)
    {
       // roomEntries[roomName].Initialize();
    }
    void OnCreateBtnRoomPressed()
    {
        bool isActive = Panel_CreateRoom.gameObject.activeSelf;
        Panel_CreateRoom.SetActive(!isActive);EventBus<DisableButtons>.Publish(new DisableButtons(!isActive));
    }

    #endregion

    private void OnDestroy()
    {
        EventBus<DisableButtons>.Subscribe(disableButtonsBainding);
        createRoomBtn.onClick.RemoveAllListeners();
        disconnectBtn.onClick.RemoveListener(() => Client.Instance.Disconnect());
        refreshRoomsButton.onClick.RemoveListener(() => EventBus<RefreshRooms>.Publish(new RefreshRooms()));
    }
}
