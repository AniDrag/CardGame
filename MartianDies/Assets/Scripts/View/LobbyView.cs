using AniDrag.EventBus;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.EventTrigger;

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

    EventBinding<EnableButtons> disableButtonsBainding;

    private void Start()
    {
        createRoomBtn.onClick.AddListener(OnCreateBtnRoomPressed);
        disconnectBtn.onClick.AddListener(() => EventBus<Disconnect>.Publish(new Disconnect()));
        refreshRoomsButton.onClick.AddListener(() => EventBus<RefreshRooms>.Publish(new RefreshRooms()));

        disableButtonsBainding = new EventBinding<EnableButtons>(DisableButtons);
        EventBus<EnableButtons>.Subscribe(disableButtonsBainding);

    }
    public void SetPlayerName(string name)
    {
        if (playerNameText != null) playerNameText.text = name;
    }
    void DisableButtons(EnableButtons e)
    {
        disconnectBtn.interactable = e.isEnabled;
        createRoomBtn.interactable = e.isEnabled;
        refreshRoomsButton.interactable = e.isEnabled;
    }


    #region Room Creation 
    public void ClearRoomList()
    {
        for (int i = 0; i < content.childCount; i++)
            Destroy(content.GetChild(i).gameObject);
        roomEntries.Clear();
    }
    public void CreateRoomEntry(RoomDataModel data)
    {
        GameObject go = Instantiate(prf_roomEntry, content);
        var entry = go.GetComponent<RoomEntryView>();
        entry.Initialize(data);
        roomEntries.Add(data.roomName, entry);
    }

    public void PopulateRoomList(List<RoomDataModel> pRoomEntries)
    {
        foreach( var entry in pRoomEntries)
            CreateRoomEntry(entry);
    }

    public void UpdateRoomsList(List<RoomDataModel> pRoomEntries)
    {
        foreach (var entry in pRoomEntries)
        {
            if (entry.isInGame)
            {
                if (roomEntries.ContainsKey(entry.roomName))
                    Destroy(roomEntries[entry.roomName].gameObject);
                roomEntries.Remove(entry.roomName);
            }
            else
            {
                if (roomEntries.TryGetValue(entry.roomName, out var roomEntry))
                    roomEntry.UpdateParticipants(entry.participantCount);
                else
                    CreateRoomEntry(entry);
            }
        }
    }
    public void UpdateRoomVisuals(string roomName)
    {
       // roomEntries[roomName].Initialize();
    }
    public void UpdateRoomEntry(RoomDataModel room)
    {
        if (roomEntries.TryGetValue(room.roomName, out var entry))
        {
            entry.UpdateParticipants(room.participantCount);
            // Update other fields if needed
        }
        else
        {
            CreateRoomEntry(room);
        }
    }
    void OnCreateBtnRoomPressed()
    {
        bool isActive = Panel_CreateRoom.gameObject.activeSelf;
        Panel_CreateRoom.SetActive(!isActive);
        EventBus<EnableButtons>.Publish(new EnableButtons(!isActive));
    }

    #endregion

    private void OnDestroy()
    {
        EventBus<EnableButtons>.Unsubscribe(disableButtonsBainding);
        createRoomBtn.onClick.RemoveAllListeners();
        disconnectBtn.onClick.RemoveListener(() => EventBus<Disconnect>.Publish(new Disconnect()));
        refreshRoomsButton.onClick.RemoveAllListeners();
    }
}