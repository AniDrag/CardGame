using System;
using TMPro;
using AniDrag.EventBus;
using UnityEngine;
using UnityEngine.UI;

public class RoomEntryView : MonoBehaviour
{
    [SerializeField] private TMP_Text roomDetails;
    [SerializeField] private TMP_Text participants;
    [SerializeField] private Button joinBtn;
    private RoomEntryData data;

    EventBinding<DisableButtons> disableButtonsBainding;

    private void OnEnable()
    {
        Sub();
    }
    private void OnDestroy()
    {
        UnSub();
    }

    private void OnDisable()
    {
        UnSub();
    }

    void Sub()
    {
        joinBtn.onClick.AddListener(() => EventBus<JoinRoom>.Publish(new JoinRoom(data)));
        disableButtonsBainding = new EventBinding<DisableButtons>(DisableButtons);
        EventBus<DisableButtons>.Subscribe(disableButtonsBainding);
    }
    void UnSub()
    {
        joinBtn.onClick.RemoveListener(() => EventBus<JoinRoom>.Publish(new JoinRoom(data)));
        EventBus<DisableButtons>.Unsubscribe(disableButtonsBainding);
    }

    void DisableButtons(DisableButtons e)
    {
        joinBtn.interactable = e.isEnabled;
    }
    public void Initialize(RoomEntryData pData)
    {
        data = pData;
        Sub();
        roomDetails.text = $"{data.roomName}    Goal: {data.pointGoal}pt";
        UpdateParticipants(data.currParticipants);
    }
    public void UpdateParticipants(int count)
    {
        participants.text = $"{count} / 4";
    }
    
}
[Serializable]
public class RoomEntryData
{
    public int ID;
    public string roomName;
    public string host;
    public int pointGoal;
    public int currParticipants;
    public bool isInGame;
    public RoomEntryData(int pId, string pRoomName, string pHostName, int pPointGoal, int pCurrParticipants, bool pIsInGame = false)
    {
        ID = pId;
        roomName = pRoomName;
        host = pHostName;
        pointGoal = pPointGoal;
        currParticipants = pCurrParticipants;
        isInGame = pIsInGame;
    }
}