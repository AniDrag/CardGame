using AniDrag.EventBus;
using OSCTools;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{
    [Header("View references")]
    [SerializeField] private GameView view;
    [SerializeField] private ConfirmChoiceView confirmChoice;
    [SerializeField] private RollAgainView rollAgainView;
    [SerializeField] private RoundResultsView roundResultsView;
    [SerializeField] private AnouncmentsView anouncmentsView;

    private EventBinding<SelectedDiceType> selectedDiceTypeBinding;
    private EventBinding<StakeRoll> stakeRollBinding;

    private bool isYourTurn = false;
    private int myPlayerIndex = -1;
    private List<int> currentDice = new();

    private void Start()
    {
        if (!CheckReferences()) return;
        SubscribeEvents();
        SubscribeOSC();
        // Listen for turn and dice updates
        Client.Instance.AddListener("/your_turn", OnYourTurn, OSCUtil.STRING);
        Client.Instance.AddListener("/dice_rolled", OnDiceRolled);
        Client.Instance.AddListener("/game_state", OnGameState);
        Client.Instance.AddListener("/game_announcement", OnAnouncmentMade, OSCUtil.STRING);
        Client.Instance.AddListener("/round_results", OnResultsPublished, OSCUtil.STRING);
        Client.Instance.AddListener("/stake_prompt", OnStakePrompt, OSCUtil.BOOL, OSCUtil.STRING);
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        if (Client.Instance != null)
        {
            Client.Instance.RemoveListener("/your_turn", OnYourTurn);
            Client.Instance.RemoveListener("/dice_rolled", OnDiceRolled);
            Client.Instance.RemoveListener("/game_state", OnGameState);
            Client.Instance.RemoveListener("/game_announcement", OnAnouncmentMade);
            Client.Instance.RemoveListener("/round_results", OnResultsPublished);
            Client.Instance.RemoveListener("/stake_prompt", OnStakePrompt);
        }
    }

    #region Setup
    private bool CheckReferences()
    {
        if (view == null) view = GetComponent<GameView>();
        if (confirmChoice == null) confirmChoice = FindFirstObjectByType<ConfirmChoiceView>();
        if (rollAgainView == null) rollAgainView = FindFirstObjectByType<RollAgainView>();
        if (roundResultsView == null) roundResultsView = FindFirstObjectByType<RoundResultsView>();
        if (anouncmentsView == null) anouncmentsView = FindFirstObjectByType<AnouncmentsView>();

        if (view == null) { Debug.LogError("GameView missing"); return false; }
        if (confirmChoice == null) { Debug.LogError("ConfirmChoiceView missing"); return false; }
        if (rollAgainView == null) { Debug.LogError("RollAgainView missing"); return false; }
        if (roundResultsView == null) { Debug.LogError("RoundResultsView missing"); return false; }
        if (anouncmentsView == null) { Debug.LogError("AnouncmentsView missing"); return false; }
        return true;
    }

    private void SubscribeEvents()
    {
        selectedDiceTypeBinding = new EventBinding<SelectedDiceType>(OnSelectDice);
        EventBus<SelectedDiceType>.Subscribe(selectedDiceTypeBinding);
        stakeRollBinding = new EventBinding<StakeRoll>(OnStakeReroll);
        EventBus<StakeRoll>.Subscribe(stakeRollBinding);
    }

    private void UnsubscribeEvents()
    {
        EventBus<SelectedDiceType>.Unsubscribe(selectedDiceTypeBinding);
        EventBus<StakeRoll>.Unsubscribe(stakeRollBinding);
    }

    private void SubscribeOSC()
    {
        // Already added in Start, but ensure disconnect button works
        view.DisconnectButton.onClick.AddListener(() => Client.Instance.Disconnect());
    }
    #endregion

    #region OSC Handlers
    void OnDiceRolled(OSCMessageIn msg, IPEndPoint sender)
    {
        int count = msg.ReadInt();
        List<int> dice = new List<int>();
        for (int i = 0; i < count; i++) dice.Add(msg.ReadInt());
        view.DisplayDice(dice);
        view.EnableDiceSelection(isYourTurn);
    }

    void OnResultsPublished(OSCMessageIn msg, IPEndPoint sender)
    {
        string result = msg.ReadString();
        roundResultsView.ShowResults(result);
    }

    void OnAnouncmentMade(OSCMessageIn msg, IPEndPoint sender)
    {
        string announcement = msg.ReadString();
        anouncmentsView.ShowAnnouncement(announcement);
    }

    // Add these new handlers (keep in same region)
    void OnYourTurn(OSCMessageIn msg, IPEndPoint sender)
    {
        string turnText = msg.ReadString();
        anouncmentsView.ShowAnnouncement(turnText);
        isYourTurn = turnText.Contains("your turn");
        view.SetTurnIndicator(isYourTurn);
        if (isYourTurn) view.EnableDiceSelection(true);
    }

    void OnGameState(OSCMessageIn msg, IPEndPoint sender)
    {
        int currentTurnIndex = msg.ReadInt();
        int playerCount = msg.ReadInt();
        view.ClearUsers();
        for (int i = 0; i < playerCount; i++)
        {
            string name = msg.ReadString();
            int points = msg.ReadInt();
            view.UpdateOrAddUser(name, points);
            if (name == Client.Instance.Username) isYourTurn = (i == currentTurnIndex);
        }
        view.SetTurnIndicator(isYourTurn);
    }

    void OnStakePrompt(OSCMessageIn msg, IPEndPoint sender)
    {
        bool canStake = msg.ReadBool();
        string optionalMsg = msg.ReadString();
        rollAgainView.Show(canStake, optionalMsg);
    }
    #endregion

    #region UI Event Handlers
    void OnSelectDice(SelectedDiceType e)
    {
        if (!isYourTurn) return;
        var msg = new OSCMessageOut("/select_die");
        msg.AddInt(e.diceType);
        Client.Instance.Send(msg);
        view.EnableDiceSelection(false);
    }

    void OnStakeReroll(StakeRoll e)
    {
        if (!isYourTurn) return;
        var msg = new OSCMessageOut("/stake_roll");
        msg.AddBool(e.doReRoll);
        Client.Instance.Send(msg);
        rollAgainView.Hide();
    }
    #endregion
}