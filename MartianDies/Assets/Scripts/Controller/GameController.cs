using AniDrag.EventBus;
using OSCTools;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rundow of what i need to accomplish.
/// 
///     No. 1:
///         make sure that it goes.
///         on round start server rolls dice. 
///         Display rolled dice on clients.
///         Server also precalculates and takes all Tanks off the list and adds them to danger slot. so server sends a GameStateModel -> dice rolled list.
///         -> we send tanks to the danger line since precalculated that we cant select those. Instantiates the dice.
///         prevent clients from taking action if not on turn.
///         once rolled dice.interactibel = true and player selects a die 
///         -> trigger Dice type. 
///         -> server confirms type,
///         -> we see if u colected points or whatever, client will do it after server confirms selection of dice,
///         -> Server sends a lsit of rolled dice if no points aquired his turn. else open stakes roll view.
///         -> make sure we update view and move in a circle for the players pressent, update charts of point conters per round and a winner screen.
///         Winner: name of winer
///         YOU LOSE example if u didnt win ull see U Lose and the winers name on the bottom.
/// </summary>

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
    #region OCT Strings
    //OSC message Identifiers
    private const string SELECT_DICE = "/selected_dice";
    private const string STAKE_ROLL = "/roll_accepted";

    //OSC message Identifiers Subscriptions Replies
    private const string S_DISCONECT = "/disconected";
    private const string S_YOUR_TURN = "/your_turn";
    private const string S_DICE_ROLLED = "/dice_rolled";
    private const string S_GAME_STATE = "/game_state";
    private const string S_DICE_SELECTED = "/dice_selected";
    private const string S_GAME_ANNOUNCMENT = "/game_announcement";
    private const string S_ROUND_RESULTS = "/round_results";
    private const string S_STAKE_ROLL_PROMPT = "/stake_prompt";


    #endregion

    private void Start()
    {
        if (!CheckReferences()) return;
        SubscribeEvents();
        SubscribeOSC();
        // Listen for turn and dice updates
        Client.Instance.AddListener(S_DISCONECT, OnDisconnectedReplie);
        Client.Instance.AddListener(S_YOUR_TURN, OnYourTurn, OSCUtil.STRING);
        Client.Instance.AddListener(S_DICE_ROLLED, OnDiceRolled);
        Client.Instance.AddListener(S_GAME_STATE, OnGameState);
        Client.Instance.AddListener(S_DICE_SELECTED, OnGameState);
        Client.Instance.AddListener(S_GAME_ANNOUNCMENT, OnAnouncmentMade, OSCUtil.STRING);
        Client.Instance.AddListener(S_ROUND_RESULTS, OnResultsPublished, OSCUtil.STRING);
        Client.Instance.AddListener(S_STAKE_ROLL_PROMPT, OnStakePrompt, OSCUtil.BOOL, OSCUtil.STRING);
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        if (Client.Instance != null) 
        {
            Client.Instance.RemoveListener(S_DISCONECT, OnDisconnectedReplie);
            Client.Instance.RemoveListener(S_YOUR_TURN, OnYourTurn);
            Client.Instance.RemoveListener(S_DICE_ROLLED, OnDiceRolled);
            Client.Instance.RemoveListener(S_GAME_STATE, OnGameState);
            Client.Instance.RemoveListener(S_DICE_SELECTED, OnGameState);
            Client.Instance.RemoveListener(S_GAME_ANNOUNCMENT, OnAnouncmentMade);
            Client.Instance.RemoveListener(S_ROUND_RESULTS, OnResultsPublished);
            Client.Instance.RemoveListener(S_STAKE_ROLL_PROMPT, OnStakePrompt);
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
        Client.Log(result);
        EventBus<RoundResults>.Publish(new RoundResults(result));
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

    private void OnDisconnectedReplie(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Player gave up: " + msg.ReadString());
        SceneManager.LoadScene("0_SC_MainMenu");
    }
}