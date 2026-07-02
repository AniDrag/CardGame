using AniDrag.EventBus;
using OSCTools;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    #region View References

    [Header("View References")]
    [SerializeField] private GameView view;
    [SerializeField] private ConfirmChoiceView confirmChoice;
    [SerializeField] private RollAgainView rollAgainView;
    [SerializeField] private RoundResultsView roundResultsView;
    [SerializeField] private AnnouncementsView announcementsView;

    #endregion

    #region State

    private bool _isMyTurn = false;
    private int _currentTurnClientId = -1;

    private Dictionary<int, bool> _lastSelectableDice = new();

    #endregion

    #region Event Bindings

    private EventBinding<SelectedDiceType> selectedDiceBinding;
    private EventBinding<StakeRoll> stakeRollBinding;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (!ValidateReferences())
            return;

        RegisterUIEvents();
        RegisterServerMessages();
        RegisterButtons();

        view.SetTurnIndicator(false);
        view.EnableDiceSelection(false);

        SendGameSceneReady();
    }

    private void OnDestroy()
    {
        UnregisterUIEvents();
        UnregisterServerMessages();
        UnregisterButtons();
    }

    #endregion

    #region Setup

    private bool ValidateReferences()
    {
        if (Client.Instance == null)
        {
            Debug.LogError("Client instance missing!");
            return false;
        }

        if (view == null)
            view = GetComponent<GameView>();

        if (confirmChoice == null)
            confirmChoice = FindFirstObjectByType<ConfirmChoiceView>();

        if (rollAgainView == null)
            rollAgainView = FindFirstObjectByType<RollAgainView>();

        if (roundResultsView == null)
            roundResultsView = FindFirstObjectByType<RoundResultsView>();

        if (announcementsView == null)
            announcementsView = FindFirstObjectByType<AnnouncementsView>();

        bool valid = true;

        if (view == null)
        {
            Debug.LogError("GameView missing");
            valid = false;
        }

        if (rollAgainView == null)
        {
            Debug.LogError("RollAgainView missing");
            valid = false;
        }

        if (roundResultsView == null)
        {
            Debug.LogError("RoundResultsView missing");
            valid = false;
        }

        if (announcementsView == null)
        {
            Debug.LogError("AnnouncementsView missing");
            valid = false;
        }

        return valid;
    }

    #endregion

    #region Event Registration

    private void RegisterUIEvents()
    {
        selectedDiceBinding = new EventBinding<SelectedDiceType>(OnSelectedDice);
        stakeRollBinding = new EventBinding<StakeRoll>(OnStakeChoice);

        EventBus<SelectedDiceType>.Subscribe(selectedDiceBinding);
        EventBus<StakeRoll>.Subscribe(stakeRollBinding);
    }

    private void UnregisterUIEvents()
    {
        if (selectedDiceBinding != null)
            EventBus<SelectedDiceType>.Unsubscribe(selectedDiceBinding);

        if (stakeRollBinding != null)
            EventBus<StakeRoll>.Unsubscribe(stakeRollBinding);
    }

    private void RegisterServerMessages()
    {
        Client client = Client.Instance;

        client.AddListener(Msg.S_YOUR_TURN, OnYourTurn, OSCUtil.STRING);
        client.AddListener(Msg.S_TURN_STARTED, OnTurnStarted, OSCUtil.INT, OSCUtil.STRING);
        client.AddListener(Msg.S_DICE_ROLLED, OnDiceRolled);
        client.AddListener(Msg.S_TURN_OPTIONS, OnTurnOptions);
        client.AddListener(Msg.S_DICE_SELECTED, OnDiceSelected, OSCUtil.INT);
        client.AddListener(Msg.S_GAME_STATE, OnGameState);
        client.AddListener(Msg.S_GAME_ANNOUNCEMENT, OnAnnouncement, OSCUtil.STRING);
        client.AddListener(Msg.S_ROUND_RESULTS, OnRoundResults, OSCUtil.STRING);
        client.AddListener(Msg.S_STAKE_PROMPT, OnStakePrompt);
        client.AddListener(Msg.S_INVALID_MOVE, OnInvalidMove, OSCUtil.STRING);
        client.AddListener(Msg.S_GAME_END, OnGameEnd, OSCUtil.STRING);
    }

    private void UnregisterServerMessages()
    {
        if (Client.Instance == null)
            return;

        Client client = Client.Instance;

        client.RemoveListener(Msg.S_YOUR_TURN, OnYourTurn);
        client.RemoveListener(Msg.S_TURN_STARTED, OnTurnStarted);
        client.RemoveListener(Msg.S_DICE_ROLLED, OnDiceRolled);
        client.RemoveListener(Msg.S_TURN_OPTIONS, OnTurnOptions);
        client.RemoveListener(Msg.S_DICE_SELECTED, OnDiceSelected);
        client.RemoveListener(Msg.S_GAME_STATE, OnGameState);
        client.RemoveListener(Msg.S_GAME_ANNOUNCEMENT, OnAnnouncement);
        client.RemoveListener(Msg.S_ROUND_RESULTS, OnRoundResults);
        client.RemoveListener(Msg.S_STAKE_PROMPT, OnStakePrompt);
        client.RemoveListener(Msg.S_INVALID_MOVE, OnInvalidMove);
        client.RemoveListener(Msg.S_GAME_END, OnGameEnd);
    }

    private void RegisterButtons()
    {
        if (view.disconnectButton != null)
            view.disconnectButton.onClick.AddListener(OnDisconnectButtonClicked);
    }

    private void UnregisterButtons()
    {
        if (view != null && view.disconnectButton != null)
            view.disconnectButton.onClick.RemoveListener(OnDisconnectButtonClicked);
    }

    #endregion

    #region Received Messages

    private void OnYourTurn(OSCMessageIn msg, IPEndPoint sender)
    {
        string message = msg.ReadString();

        announcementsView.ShowAnnouncement(message);
    }
    private void OnTurnStarted(OSCMessageIn msg, IPEndPoint sender)
    {
        int currentPlayerId = msg.ReadInt();
        string currentPlayerName = msg.ReadString();

        _currentTurnClientId = currentPlayerId;
        _isMyTurn = Client.Instance.ClientId == currentPlayerId;

        _lastSelectableDice.Clear();

        view.ClearTurnDiceZones();
        view.SetTurnIndicator(_isMyTurn);
        view.SyncTurnStats(0, 0, 0, false);

        announcementsView.ShowAnnouncement($"{currentPlayerName}'s turn.");
    }
    private void OnDiceRolled(OSCMessageIn msg, IPEndPoint sender)
    {
        int currentPlayerId = msg.ReadInt();

        _currentTurnClientId = currentPlayerId;
        _isMyTurn = Client.Instance.ClientId == currentPlayerId;

        Client.Log("Game", $"Dice rolled owner={currentPlayerId}, myId={Client.Instance.ClientId}");

        int diceCount = msg.ReadInt();

        List<int> dice = new List<int>();

        for (int i = 0; i < diceCount; i++)
            dice.Add(msg.ReadInt());

        int turnPoints = msg.ReadInt();
        int defense = msg.ReadInt();
        int attack = msg.ReadInt();
        bool doubleStakeActive = msg.ReadBool();

        _lastSelectableDice.Clear();

        view.GenerateRollingZoneDice(dice);
        view.EnableDiceSelection(false);
        view.SetTurnIndicator(_isMyTurn);
        view.SyncTurnStats(turnPoints, defense, attack, doubleStakeActive);
    }
    private void OnDiceSelected(OSCMessageIn msg, IPEndPoint sender)
    {
        int diceType = msg.ReadInt();

        view.MoveSelectedDiceToZone(diceType);
        view.PredictSelectedDiceStats(diceType);
    }
    private void OnTurnOptions(OSCMessageIn msg, IPEndPoint sender)
    {
        _isMyTurn = true;
        Client.Log("Game", "Received turn options. This client is current player.");
        int selectableCount = msg.ReadInt();

        Dictionary<int, bool> selectableDice = new Dictionary<int, bool>();

        for (int i = 0; i < selectableCount; i++)
        {
            int diceType = msg.ReadInt();
            bool isSelectable = msg.ReadBool();

            selectableDice[diceType] = isSelectable;
        }

        _lastSelectableDice = selectableDice;

        view.SetTurnIndicator(true);
        view.SetDiceSelectable(selectableDice, true);
    }

    private void OnGameState(OSCMessageIn msg, IPEndPoint sender)
    {
        int currentTurnIndex = msg.ReadInt();
        int playerCount = msg.ReadInt();

        view.ClearUsers();

        for (int i = 0; i < playerCount; i++)
        {
            string name = msg.ReadString();
            int points = msg.ReadInt();

            view.UpdateOrAddUser(name, points);
        }
    }

    private void OnAnnouncement(OSCMessageIn msg, IPEndPoint sender)
    {
        string text = msg.ReadString();

        announcementsView.ShowAnnouncement(text);
    }

    private void OnRoundResults(OSCMessageIn msg, IPEndPoint sender)
    {
        string result = msg.ReadString();

        EventBus<RoundResults>.Publish(new RoundResults(result));
    }

    private void OnStakePrompt(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Game", "Stake prompt received.");

        view.EnableDiceSelection(false);

        if (rollAgainView == null)
        {
            Client.Log("[GameController] RollAgainView missing.");
            return;
        }

        rollAgainView.Show();
    }

    private void OnInvalidMove(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();

        announcementsView.ShowAnnouncement(reason);

        if (_isMyTurn)
            view.SetDiceSelectable(_lastSelectableDice, true);
    }

    private void OnGameEnd(OSCMessageIn msg, IPEndPoint sender)
    {
        string winnerMessage = msg.ReadString();

        announcementsView.ShowAnnouncement(winnerMessage);

        view.EnableDiceSelection(false);
        rollAgainView.Hide();

        Invoke(nameof(ReturnToLobby), 3f);
    }

    #endregion

    #region UI Events

    private void OnSelectedDice(SelectedDiceType e)
    {
        SendSelectDice(e.diceType);
    }

    private void OnStakeChoice(StakeRoll e)
    {
        SendStakeAnswer(e.doReRoll);
    }

    private void OnDisconnectButtonClicked()
    {
        Client.Instance.Disconnect();
    }

    #endregion

    #region Sending Messages
    private void SendGameSceneReady()
    {
        var msg = new OSCMessageOut(Msg.C_GAME_SCENE_READY);

        Client.Instance.Send(msg);

        Client.Log("Game", "Sent game scene ready.");
    }
    private void SendSelectDice(int diceType)
    {
        if (!_isMyTurn)
            return;

        var msg = new OSCMessageOut(Msg.C_SELECT_DICE)
            .AddInt(diceType);

        Client.Instance.Send(msg);

        view.EnableDiceSelection(false);
    }

    private void SendStakeAnswer(bool doReRoll)
    {
        if (!_isMyTurn)
            return;

        var msg = new OSCMessageOut(Msg.C_STAKE_ANSWER)
            .AddBool(doReRoll);

        Client.Instance.Send(msg);

        rollAgainView.Hide();
    }

    #endregion

    #region Helpers

    private void ReturnToLobby()
    {
        SceneManager.LoadScene(Scenes.Lobby);
    }

    #endregion
}