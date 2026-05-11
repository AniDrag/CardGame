using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyView : MonoBehaviour
{
    // TO DO: Room entry needs to pass it self in to SelectRoom Action<RoomEntry> selectRoom.
    // TO DO: Handle room joining then on the info panel.
    [Header("Player Info")]
    public TMP_Text playerNameText;
    public Button disconnectBtn;
    public Button createRoomBtn;
    public Button refreshRoomsButton;

    [Header("Room List")]
    public Transform content;
    public GameObject prg_roomEntry;

    public Action<bool> OnEnableButtons;
    private List<RoomEntryView> roomEntries = new List<RoomEntryView>();

    private void Start()
    {
        OnEnableButtons += EnableButtons;
        createRoomBtn.onClick.AddListener(DissableButtons);
    }
    public void SetPlayerName(string name)
    {
        if (playerNameText != null) playerNameText.text = name;
    }
    void EnableButtons(bool enabled)
    {
        disconnectBtn.interactable = enabled;
        createRoomBtn.interactable = enabled;
        refreshRoomsButton.interactable = enabled;
    }

    public void ClearRoomList()
    {
        foreach (var entry in roomEntries)
            Destroy(entry.gameObject);
        roomEntries.Clear();
    }
    /// <summary>
    /// Used To Set the Join Btn On Click event
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public RoomEntryView CreateRoomEntry(RoomEntryData data)
    {
        GameObject go = Instantiate(prg_roomEntry, content);
        var entry = go.GetComponent<RoomEntryView>();
        entry.Initialize(data.roomName, data.pointGoal, data.currParticipants, data.ID);
        roomEntries.Add(entry);
        return entry;
    }

    void DissableButtons()
    {
        OnEnableButtons?.Invoke(false);
    }

    private void OnDestroy()
    {
        OnEnableButtons -= EnableButtons;
        createRoomBtn.onClick.RemoveAllListeners();
        disconnectBtn.onClick.RemoveAllListeners();
        refreshRoomsButton.onClick.RemoveAllListeners();    
    }
}
