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
    [Header("View References")]
    [SerializeField] private GameView view;
    [SerializeField] private ConfirmChoiceView confirmChoice;
    [SerializeField] private RollAgainView rollAgainView;
    [SerializeField] private RoundResultsView roundResultsView;
    [SerializeField] private AnnouncementsView announcementsView;

    // Event bindings
    private EventBinding<SelectedDiceType> _selectedDiceBinding;
    private EventBinding<StakeRoll> _stakeRollBinding;

    private bool _isMyTurn = false;
    private int _myPlayerIndex = -1;

    private void Start()
    {
        if (!ValidateReferences()) return;
        SubscribeEvents();
        SubscribeOSC();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        UnsubscribeOSC();
    }


    #region Setup
    private bool ValidateReferences()
    {
        if (view == null) view = GetComponent<GameView>();
        if (confirmChoice == null) confirmChoice = FindFirstObjectByType<ConfirmChoiceView>();
        if (rollAgainView == null) rollAgainView = FindFirstObjectByType<RollAgainView>();
        if (roundResultsView == null) roundResultsView = FindFirstObjectByType<RoundResultsView>();
        if (announcementsView == null) announcementsView = FindFirstObjectByType<AnnouncementsView>();

        bool ok = true;
        if (view == null) { Debug.LogError("GameView missing"); ok = false; }
        if (confirmChoice == null) { Debug.LogError("ConfirmChoiceView missing"); ok = false; }
        if (rollAgainView == null) { Debug.LogError("RollAgainView missing"); ok = false; }
        if (roundResultsView == null) { Debug.LogError("RoundResultsView missing"); ok = false; }
        if (announcementsView == null) { Debug.LogError("AnnouncementsView missing"); ok = false; }
        return ok;
    }

    private void SubscribeEvents()
    {
        _selectedDiceBinding = new EventBinding<SelectedDiceType>(OnSelectedDice);
        EventBus<SelectedDiceType>.Subscribe(_selectedDiceBinding);

        _stakeRollBinding = new EventBinding<StakeRoll>(OnStakeChoice);
        EventBus<StakeRoll>.Subscribe(_stakeRollBinding);
    }

    private void UnsubscribeEvents()
    {
        EventBus<SelectedDiceType>.Unsubscribe(_selectedDiceBinding);
        EventBus<StakeRoll>.Unsubscribe(_stakeRollBinding);
    }

    private void SubscribeOSC()
    {
        var client = Client.Instance;
        if (client == null) return;

        client.AddListener(Msg.S_YOUR_TURN, OnYourTurn, OSCUtil.STRING);
        client.AddListener(Msg.S_DICE_ROLLED, OnDiceRolled);
        client.AddListener(Msg.S_GAME_STATE, OnGameState);
        client.AddListener(Msg.S_GAME_ANNOUNCEMENT, OnAnnouncement, OSCUtil.STRING);
        client.AddListener(Msg.S_ROUND_RESULTS, OnRoundResults, OSCUtil.STRING);
        client.AddListener(Msg.S_STAKE_PROMPT, OnStakePrompt, OSCUtil.BOOL, OSCUtil.STRING);
        client.AddListener(Msg.S_GAME_END, OnGameEnd, OSCUtil.STRING);

        view.disconnectButton.onClick.AddListener(() => client.Disconnect());
    }

    private void UnsubscribeOSC()
    {
        var client = Client.Instance;
        if (client == null) return;

        client.RemoveListener(Msg.S_YOUR_TURN, OnYourTurn);
        client.RemoveListener(Msg.S_DICE_ROLLED, OnDiceRolled);
        client.RemoveListener(Msg.S_GAME_STATE, OnGameState);
        client.RemoveListener(Msg.S_GAME_ANNOUNCEMENT, OnAnnouncement);
        client.RemoveListener(Msg.S_ROUND_RESULTS, OnRoundResults);
        client.RemoveListener(Msg.S_STAKE_PROMPT, OnStakePrompt);
        client.RemoveListener(Msg.S_GAME_END, OnGameEnd);

        view.disconnectButton.onClick.RemoveListener(() => client.Disconnect());
    }
    #endregion

    #region OSC Handlers

    private void OnYourTurn(OSCMessageIn msg, IPEndPoint sender)
    {
        string message = msg.ReadString();
        announcementsView.ShowAnnouncement(message);
        _isMyTurn = message.Contains("your turn") || message.Contains(Client.Instance.Username);
        view.SetTurnIndicator(_isMyTurn);
        if (_isMyTurn)
            view.EnableDiceSelection(true);
    }
    private void OnDiceRolled(OSCMessageIn msg, IPEndPoint sender)
    {

        //TODO: if Count is -1 Means selection failed and we need to select a dice again. Show msg select new dice.
        int count = msg.ReadInt();
        List<int> dice = new List<int>();
        for (int i = 0; i < count; i++)
            dice.Add(msg.ReadInt());

        view.GenerateRollingZoneDice(dice);
        view.EnableDiceSelection(_isMyTurn);
    }

    private void OnRoundResults(OSCMessageIn msg, IPEndPoint sender)
    {
        string result = msg.ReadString();
        EventBus<RoundResults>.Publish(new RoundResults(result));
    }
    private void OnAnnouncement(OSCMessageIn msg, IPEndPoint sender)
    {
        string text = msg.ReadString();
        announcementsView.ShowAnnouncement(text);
    }

    private void OnGameState(OSCMessageIn msg, IPEndPoint sender)
    {
        int currentTurnIndex = msg.ReadInt();
        int playerCount = msg.ReadInt();
        view.ClearUsers();

        string myName = Client.Instance.Username;
        for (int i = 0; i < playerCount; i++)
        {
            string name = msg.ReadString();
            int points = msg.ReadInt();
            view.UpdateOrAddUser(name, points);
            if (name == myName)
                _isMyTurn = (i == currentTurnIndex);
        }
        view.SetTurnIndicator(_isMyTurn);

        //TODO: View.RolledDice(int[] dice);-> Moves Tanks to Defene Line,
        // TODO: IF selected UFOS or dice add them to the Defense or Point bar.
    }

    private void OnStakePrompt(OSCMessageIn msg, IPEndPoint sender)
    {
        bool canStake = msg.ReadBool();
        string optionalMsg = msg.ReadString();
        rollAgainView.Show(canStake, optionalMsg);
    }

    private void OnGameEnd(OSCMessageIn msg, IPEndPoint sender)
    {
        string winnerMsg = msg.ReadString();
        announcementsView.ShowAnnouncement(winnerMsg);
        // Optionally go back to lobby after a delay
        Invoke(nameof(ReturnToLobby), 3f);
    }
    private void ReturnToLobby()
    {
        SceneManager.LoadScene(Scenes.Lobby);
    }
    #endregion

    #region UI Event Handlers (via EventBus)
    private void OnSelectedDice(SelectedDiceType e)
    {
        if (!_isMyTurn) return;
        var msg = new OSCMessageOut(Msg.C_SELECT_DICE).AddInt(e.diceType);
        Client.Instance.Send(msg);
        view.EnableDiceSelection(false); // disable until server responds
    }

    private void OnStakeChoice(StakeRoll e)
    {
        if (!_isMyTurn) return;
        var msg = new OSCMessageOut(Msg.C_STAKE_ANSWER).AddBool(e.doReRoll);
        Client.Instance.Send(msg);
        rollAgainView.Hide();
    }
    #endregion
}