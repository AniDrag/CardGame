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

    private Dictionary<int, RoomEntryView> roomEntries = new();

    EventBinding<DisableButtons> disableButtonsBainding;

    private void Start()
    {
        createRoomBtn.onClick.AddListener(OnCreateBtnRoomPressed);

        disableButtonsBainding = new EventBinding<DisableButtons>(DisableButtons);
        EventBus<DisableButtons>.Subscribe(disableButtonsBainding);

        refreshRoomsButton.onClick.AddListener(() => EventBus<RefreshRooms>.Publish(new RefreshRooms()));
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
        foreach (var entry in roomEntries)
            Destroy(entry.gameObject);
        roomEntries.Clear();
    }
    public RoomEntryView CreateRoomEntry(RoomEntryData data)
    {
        GameObject go = Instantiate(prf_roomEntry, content);
        var entry = go.GetComponent<RoomEntryView>();
        entry.Initialize(data);
        roomEntries.Add(data.ID, entry);
        return entry;
    }

    // First Call when opening this view this request server for a lsist of eneries
    public void PopulateRoomList(List<RoomEntryData> pRoomEntries)
    {
        foreach( var entry in pRoomEntries)
            CreateRoomEntry(entry);
    }

    public void UpdateRoomsList(List<RoomEntryData> pRoomEntries)
    {
        foreach(var entry in pRoomEntries)
        {
            if(entry.isInGame)
                roomEntries.Remove(entry.ID);
            roomEntries[entry.ID].UpdateParticipants(entry.currParticipants);
        }
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
