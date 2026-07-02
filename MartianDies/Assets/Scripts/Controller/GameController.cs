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
    [SerializeField] private GameOverView gameOverView;
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
        RegisterGameOverView();

        view.SetTurnIndicator(false);
        view.EnableDiceSelection(false);

        SendGameSceneReady();
    }

    private void OnDestroy()
    {
        UnregisterUIEvents();
        UnregisterServerMessages();
        UnregisterButtons();
        UnregisterGameOverView();
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
            confirmChoice = FindFirstObjectByType<ConfirmChoiceView>(FindObjectsInactive.Include);

        if (rollAgainView == null)
            rollAgainView = FindFirstObjectByType<RollAgainView>(FindObjectsInactive.Include);

        if (roundResultsView == null)
            roundResultsView = FindFirstObjectByType<RoundResultsView>(FindObjectsInactive.Include);

        if (announcementsView == null)
            announcementsView = FindFirstObjectByType<AnnouncementsView>(FindObjectsInactive.Include);

        if (rollAgainView == null)
            rollAgainView = FindFirstObjectByType<RollAgainView>(FindObjectsInactive.Include);

        if (gameOverView == null)
            gameOverView = FindFirstObjectByType<GameOverView>(FindObjectsInactive.Include);

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

    private void RegisterGameOverView()
    {
        if (gameOverView == null)
            return;

        gameOverView.OnRematchClicked += SendRematchRequest;
        gameOverView.OnLeaveClicked += SendLeaveGame;
    }

    private void UnregisterGameOverView()
    {
        if (gameOverView == null)
            return;

        gameOverView.OnRematchClicked -= SendRematchRequest;
        gameOverView.OnLeaveClicked -= SendLeaveGame;
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

        client.AddListener(Msg.S_REMATCH_UPDATE, OnRematchUpdate, OSCUtil.INT, OSCUtil.INT);
        client.AddListener(Msg.S_REMATCH_STARTED, OnRematchStarted);
        client.AddListener(Msg.S_RETURN_TO_LOBBY, OnReturnToLobby, OSCUtil.STRING);
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

        client.RemoveListener(Msg.S_REMATCH_UPDATE, OnRematchUpdate);
        client.RemoveListener(Msg.S_REMATCH_STARTED, OnRematchStarted);
        client.RemoveListener(Msg.S_RETURN_TO_LOBBY, OnReturnToLobby);
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

        if (view != null)
            view.EnableDiceSelection(false);

        if (rollAgainView == null)
        {
            Client.Log("GameController", "RollAgainView reference is missing.");
            return;
        }

        rollAgainView.Show();

        Client.Log("Game", "RollAgainView.Show() called.");
    }

    private void OnInvalidMove(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();

        announcementsView.ShowAnnouncement(reason);

        if (_isMyTurn)
            view.SetDiceSelectable(_lastSelectableDice, true);
    }
    private void OnRematchUpdate(OSCMessageIn msg, IPEndPoint sender)
    {
        int readyCount = msg.ReadInt();
        int neededCount = msg.ReadInt();

        Client.Log("Game", $"Rematch update: {readyCount}/{neededCount}");

        if (gameOverView != null)
            gameOverView.SetRematchStatus(readyCount, neededCount);
    }

    private void OnRematchStarted(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Game", "Rematch started.");

        if (gameOverView != null)
            gameOverView.Hide();

        view.ClearTurnDiceZones();
        view.SyncTurnStats(0, 0, 0, false);
        view.EnableDiceSelection(false);
    }

    private void OnReturnToLobby(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();

        Client.Log("Game", "Returning to lobby: " + reason);

        Client.Instance.CurrentRoom = null;

        SceneManager.LoadSceneAsync(Scenes.Lobby);
    }
    private void OnGameEnd(OSCMessageIn msg, IPEndPoint sender)
    {
        string message = msg.ReadString();

        Client.Log("Game", "Game ended: " + message);

        view.EnableDiceSelection(false);

        if (gameOverView != null)
            gameOverView.Show(message);
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
    private void SendRematchRequest()
    {
        var msg = new OSCMessageOut(Msg.C_REMATCH_REQUEST);

        Client.Instance.Send(msg);

        Client.Log("Game", "Sent rematch request.");
    }

    private void SendLeaveGame()
    {
        var msg = new OSCMessageOut(Msg.C_LEAVE_GAME);

        Client.Instance.Send(msg);

        Client.Log("Game", "Sent leave game.");
    }
    #endregion

    #region Helpers

    private void ReturnToLobby()
    {
        SceneManager.LoadScene(Scenes.Lobby);
    }

    #endregion
}