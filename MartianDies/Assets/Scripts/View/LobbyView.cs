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

/*
[Header("Player Info")]
[SerializeField] public TMP_Text playerNameText;
public Button diconectBtn;

[Header("Player List")]
public Transform roomListParent;
public GameObject roomEntryPrefab;

[Header("Room Creation")]
public TMP_InputField createRoomNameInput;
public Slider pointGoalSlider;
public TMP_Text pointGoalValueText;
public Button createRoomButton;

[Header("Room Info")]
public TMP_Text roomInfoText;
public Button joinRoomButton;// shown to players -> Goes to Wait popup. while Host just waits and presses Start 
public Button startGameButton;// shown to only host

[Header("Waiting Popup")]
public Button leaveRoomButton;
public GameObject waitingPopup;
public TMP_Text waitingText;
public float blinkSpeed = 0.5f;

//private LobbyController controller;// Not needed Controller will handle all refrences
private Coroutine blinkCoroutine;


private string _roomName;
private string _hostName;
private bool _gameStarted;
private int _maxPlayers = 4;
private int _currentPlayers = 1;


void Start()
{
    controller = FindObjectOfType<LobbyController>();
    if (controller == null) Debug.LogError("LobbyController missing");

    createRoomButton.onClick.AddListener(() =>
    {
        string roomName = createRoomNameInput.text.Trim();
        int pointGoal = (int)pointGoalSlider.value;
        if (!string.IsNullOrEmpty(roomName))
            controller.CreateRoom(roomName, pointGoal);
    });

    joinRoomButton.onClick.AddListener(() =>
    {
        string roomName = joinRoomNameInput.text.Trim();
        if (!string.IsNullOrEmpty(roomName))
            controller.JoinRoom(roomName);
    });

    leaveRoomButton.onClick.AddListener(() => controller.LeaveRoom());
    startGameButton.onClick.AddListener(() => controller.StartGame());

    pointGoalSlider.onValueChanged.AddListener(val =>
    {
        pointGoalValueText.text = ((int)val).ToString();
    });

    waitingPopup.SetActive(false);
}

public void UpdateRoomUI(string roomName,  string hostName, bool isGameStarted = false, int maxPlayers = 4, int playerCount = 1)
{
    string readyTxt = isGameStarted ? "Starting" : "Waiting...";
    roomInfoText.text = $"{roomName} \n({playerCount}/{maxPlayers}) \nHost: {hostName} \n Ready: {readyTxt}";
    startGameButton.gameObject.SetActive(controller.IsHost && !isGameStarted);
    leaveRoomButton.interactable = true;
    createRoomButton.interactable = false;
    joinRoomButton.interactable = false;
}

public void UpdateRoomList(string[] playerNames)
{
    // Clear existing
    foreach (Transform child in playerListParent)
        Destroy(child.gameObject);

    foreach (string name in playerNames)
    {
        var item = Instantiate(playerListItemPrefab, playerListParent);
        item.GetComponentInChildren<TMP_Text>().text = name;
    }
}

public void ShowWaitingForHost()
{
    waitingPopup.SetActive(true);
    if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
    blinkCoroutine = StartCoroutine(BlinkText());
}

public void HideWaitingPopup()
{
    waitingPopup.SetActive(false);
    if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
}

private IEnumerator BlinkText()
{
    while (true)
    {
        waitingText.text = "Waiting for host to start game...";
        yield return new WaitForSeconds(blinkSpeed);
        waitingText.text = "Waiting for host to start game...  .";
        yield return new WaitForSeconds(blinkSpeed);
        waitingText.text = "Waiting for host to start game...  ..";
        yield return new WaitForSeconds(blinkSpeed);
        waitingText.text = "Waiting for host to start game...  ...";
        yield return new WaitForSeconds(blinkSpeed);
    }
}

private void OnDestroy()
{
    createRoomButton.onClick.RemoveAllListeners();
    joinRoomButton.onClick.RemoveAllListeners();
    leaveRoomButton.onClick.RemoveAllListeners();
    startGameButton.onClick.RemoveAllListeners();
}
}*/