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
        // On destroy i
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

/*
Q & A session – LobbyView

Q1: What is the main purpose of LobbyView?
A1: It is responsible for displaying the lobby UI: the player name, the list of available rooms, and buttons 
    for creating/refreshing rooms and disconnecting. It manages the visual representation of room entries 
    and responds to UI events.

Q2: Why use EventBus for button actions instead of calling methods directly?
A2: The view publishes events (Disconnect, RefreshRooms) when buttons are clicked. This decouples the view 
    from the logic layer – the controller (LobbyController) subscribes to these events and handles the 
    network operations. The view doesn't need to know how to disconnect or refresh; it just signals intent.

Q3: What is the purpose of Panel_CreateRoom, Panel_WaitingForHost, Panel_HostRoom?
A3: These are references to different UI panels that represent states in the lobby:
    - CreateRoom: the panel where the user sets room name and point goal.
    - WaitingForHost: shown to non?host players after joining a room.
    - HostRoom: shown to the host after creating a room.
    The controller toggles these panels based on game state; the view simply holds references.

Q4: Why use a Dictionary<string, RoomEntryView> to store room entries?
A4: The dictionary allows O(1) lookups by room name. This is efficient when updating a specific room’s 
    participant count or removing an entry. Without it, we would have to iterate through the content 
    children, which is less performant and more error?prone.

Q5: How does the view handle enabling/disabling buttons?
A5: It subscribes to the EnableButtons event. When the event is published (e.g., during connection attempts 
    or timeouts), the DisableButtons method toggles the interactivity of disconnectBtn, createRoomBtn, and 
    refreshRoomsButton. This centralises UI locking.

Q6: Why use separate methods for ClearRoomList, CreateRoomEntry, PopulateRoomList, and UpdateRoomEntry?
A6: Each method has a single responsibility:
    - ClearRoomList: removes all entries and clears the dictionary.
    - CreateRoomEntry: instantiates a new UI entry from a prefab and adds it to the dictionary.
    - PopulateRoomList: creates entries for a whole list (used after initial fetch).
    - UpdateRoomEntry: updates an existing entry or creates it if missing.
    This makes the code more maintainable and testable.

Q7: What does UpdateRoomsList do, and why does it check isInGame?
A7: UpdateRoomsList processes a list of rooms, typically from a server update. It removes entries for rooms 
    that are in?game (isInGame == true) because they are no longer joinable, and it updates or creates 
    entries for other rooms. This keeps the UI in sync with the server state.

Q8: Why is OnCreateBtnRoomPressed toggling both the panel visibility and publishing EnableButtons?
A8: When the create room panel opens, we want to disable interaction with the rest of the lobby (buttons) 
    to prevent conflicting actions. Publishing EnableButtons with false disables them. When the panel 
    closes, we re?enable them. This ensures the user cannot click "Create Room" while the panel is open.

Q9: How does the view handle destruction and cleanup?
A9: In OnDestroy, it unsubscribes from the EnableButtons event and removes all button listeners. This 
    prevents memory leaks and ensures that callbacks are not invoked on a destroyed object.

Q10: Why does disconnectBtn use a lambda that publishes a Disconnect event?
A10: Using a lambda is concise and avoids creating a separate method. However, note that in OnDestroy, we 
    use RemoveListener(() => EventBus<Disconnect>.Publish(new Disconnect())) – this works because lambdas 
    are cached if they capture no variables. In this case, it captures nothing, so it's safe.

Q11: Why use the [SerializeField] attribute for UI references?
A11: This allows the Inspector to assign references directly, making it easy to wire up the prefab/scene 
    without writing find?by?name code. It also makes the dependencies explicit.

Q12: How does the view know when to update a room entry?
A12: The LobbyController calls UpdateRoomEntry when it receives a room update from the server (OnRoomUpdate). 
    The view updates the participant count and other visual data via the RoomEntryView component.

Q13: Why is there no async/await code in LobbyView?
A13: The view is purely a presentation layer – it doesn't perform network operations or asynchronous tasks. 
    All asynchronous work (connection, registration, server communication) is handled by the Client and 
    controller. The view remains simple and synchronous.

Q14: What happens if the room list is refreshed while the user is inside a room?
A14: The LobbyController handles that logic. The view only displays what the controller tells it. If the 
    controller receives a new room list and the user is in a room, it may update the list in the background 
    without affecting the active room panel. The view's methods (ClearRoomList, PopulateRoomList) are 
    called accordingly.

Q15: Why store RoomEntryView components instead of just GameObjects?
A15: RoomEntryView likely has methods to update participants, set room name, etc. Storing the component 
    allows direct method calls instead of using GetComponent each time, improving performance and code 
    clarity.

Q16: How does the view ensure that duplicate room entries are not created?
A16: The dictionary is checked before adding a new entry. In CreateRoomEntry, we assume the room doesn't 
    exist; in UpdateRoomEntry, we check if it exists and update, else create. The controller should manage 
    the state to avoid duplicates, but the view is defensive.

Q17: Why is the room list cleared before populating, instead of incrementally updating?
A17: When the server sends a full room list (S_ROOM_LIST), it's simpler to clear and repopulate. This 
    ensures consistency and handles rooms that were removed. For incremental updates (S_ROOM_UPDATE), 
    we use UpdateRoomEntry to avoid flicker.

Q18: What is the role of the EnableButtons event in the view?
A18: It allows external components (like LobbyController) to lock/unlock the UI during network operations 
    (e.g., creating a room, joining a room). This prevents the user from initiating multiple concurrent 
    actions, which could cause race conditions.

Q19: How does the view handle the case where a room is closed (OnRoomClosed)?
A19: The LobbyController calls view.ClearRoomList() and then PopulateRoomList with the updated list, or 
    it calls UpdateRoomEntry to remove the specific room. The view provides the necessary methods, and the 
    controller decides which to use.

Q20: What improvements could be made to this view?
A20: Several improvements: 
    - Use a list pool for room entries to avoid instantiating/destroying frequently.
    - Add error handling for missing prefab references.
    - Combine UpdateRoomEntry and UpdateRoomsList for consistency.
    - Use an interface for the view so the controller depends on abstraction.
    - Add a loading spinner while refreshing rooms.
*/