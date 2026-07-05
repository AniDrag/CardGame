using AniDrag.EventBus;
using OSCTools;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
/*
 * GameController
 * 
 * Purpose:
 * This script controls the client-side gameplay scene.
 * It does not decide the real game rules by itself.
 * The server is authoritative, so this script mostly receives server messages,
 * updates the UI, and sends player choices back to the server.
 * 
 * Main responsibilities:
 * - Register and unregister server OSC messages.
 * - Receive game state updates from the server.
 * - Update dice visuals, player scores, turn indicators, and announcements.
 * - Send local player actions like selecting dice, answering stake prompts, rematch, and leave game.
 * - Connect UI actions to networking messages.
 * 
 * Important:
 * The client should not be trusted for scoring or turn rules.
 * The client only sends requests.
 * The server validates if the action is allowed.
 */
public class GameController : MonoBehaviour
{
    #region View References

    /*
     * These are references to the UI/view scripts used by the gameplay scene.
     * 
     * GameController does not directly build UI objects itself.
     * Instead, it tells the view scripts what to show.
     * 
     * Example:
     * - GameView handles dice visuals and player score UI.
     * - AnnouncementsView shows text messages to the player.
     * - RollAgainView shows the prompt for staking or rolling again.
     * - GameOverView shows the end screen.
     */

    [Header("View References")]
    [SerializeField] private GameView view;
    [SerializeField] private ConfirmChoiceView confirmChoice;
    [SerializeField] private RollAgainView rollAgainView;
    [SerializeField] private RoundResultsView roundResultsView;
    [SerializeField] private AnnouncementsView announcementsView;
    [SerializeField] private GameOverView gameOverView;

    #endregion

    #region State

    /*
     * _isMyTurn:
     * True when the server says this client is the current player.
     * This is used to stop the local player from sending actions when it is not their turn.
     * 
     * _currentTurnClientId:
     * Stores which client id currently owns the turn.
     * This helps the client know who is playing right now.
     * 
     * _lastSelectableDice:
     * Stores the last dice types the server said were selectable.
     * If the player makes an invalid move, this can be used to restore the selectable state.
     */

    private bool _isMyTurn = false;
    private int _currentTurnClientId = -1;

    private Dictionary<int, bool> _lastSelectableDice = new();

    #endregion

    #region Event Bindings

    /*
     * These bindings listen to local UI/game events.
     * 
     * SelectedDiceType:
     * Published when the player clicks/selects a dice type in the UI.
     * 
     * StakeRoll:
     * Published when the player chooses whether they want to roll again or stop/bank.
     */

    private EventBinding<SelectedDiceType> selectedDiceBinding;
    private EventBinding<StakeRoll> stakeRollBinding;

    #endregion

    #region Unity Lifecycle

    /*
     * Start
     * 
     * What this does:
     * Runs when the gameplay scene starts.
     * It validates references, registers UI events, registers server messages,
     * connects button events, and tells the server that this client loaded the game scene.
     * 
     * Why SendGameSceneReady is important:
     * The server may wait until clients are ready before fully starting the game.
     */
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

    /*
     * OnDestroy
     * 
     * What this does:
     * Cleans up all listeners when this scene object is destroyed.
     * 
     * Why this matters:
     * If listeners are not removed, old destroyed scene objects could still receive messages.
     * That can cause null reference errors or duplicate message handling.
     */
    private void OnDestroy()
    {
        UnregisterUIEvents();
        UnregisterServerMessages();
        UnregisterButtons();
        UnregisterGameOverView();
    }

    #endregion

    #region Setup

    /*
     * ValidateReferences
     * 
     * What this does:
     * Checks if the needed view scripts exist.
     * If a reference was not assigned in the Inspector, it tries to find it on this object
     * or inside child objects.
     * 
     * Returns:
     * true  = enough references exist to continue.
     * false = an important reference is missing, so this controller should not continue.
     * 
     * Important for reviewer:
     * confirmChoice is found, but this script does not currently use it directly.
     * It may be used by another view flow or kept for future UI logic.
     */
    private bool ValidateReferences()
    {
        if (Client.Instance == null)
        {
            Debug.LogError("Client instance missing!");
            return false;
        }

        if (view == null)
            view = GetComponent<GameView>() ?? GetComponentInChildren<GameView>(true);

        if (confirmChoice == null)
            confirmChoice = GetComponent<ConfirmChoiceView>() ?? GetComponentInChildren<ConfirmChoiceView>(true);

        if (rollAgainView == null)
            rollAgainView = GetComponent<RollAgainView>() ?? GetComponentInChildren<RollAgainView>(true);

        if (roundResultsView == null)
            roundResultsView = GetComponent<RoundResultsView>() ?? GetComponentInChildren<RoundResultsView>(true);

        if (announcementsView == null)
            announcementsView = GetComponent<AnnouncementsView>() ?? GetComponentInChildren<AnnouncementsView>(true);

        if (gameOverView == null)
            gameOverView = GetComponent<GameOverView>() ?? GetComponentInChildren<GameOverView>(true);

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

    /*
     * RegisterGameOverView
     * 
     * What this does:
     * Connects the game over screen buttons to network actions.
     * 
     * UI actions:
     * - Rematch button sends a rematch request to the server.
     * - Leave button tells the server this client wants to leave the game.
     */
    private void RegisterGameOverView()
    {
        if (gameOverView == null)
            return;

        gameOverView.OnRematchClicked += SendRematchRequest;
        gameOverView.OnLeaveClicked += SendLeaveGame;
    }

    /*
     * UnregisterGameOverView
     * 
     * What this does:
     * Removes the game over button events.
     * This prevents old scene objects from keeping event subscriptions.
     */
    private void UnregisterGameOverView()
    {
        if (gameOverView == null)
            return;

        gameOverView.OnRematchClicked -= SendRematchRequest;
        gameOverView.OnLeaveClicked -= SendLeaveGame;
    }

    #endregion

    #region Event Registration

    /*
     * RegisterUIEvents
     * 
     * What this does:
     * Subscribes to local EventBus events.
     * These events are usually fired by UI objects or dice objects.
     * 
     * SelectedDiceType data:
     * - e.diceType = the dice type/value the player selected.
     * 
     * StakeRoll data:
     * - e.doReRoll = true if the player wants to continue rolling.
     * - e.doReRoll = false if the player does not want to continue.
     */
    private void RegisterUIEvents()
    {
        selectedDiceBinding = new EventBinding<SelectedDiceType>(OnSelectedDice);
        stakeRollBinding = new EventBinding<StakeRoll>(OnStakeChoice);

        EventBus<SelectedDiceType>.Subscribe(selectedDiceBinding);
        EventBus<StakeRoll>.Subscribe(stakeRollBinding);
    }

    /*
     * UnregisterUIEvents
     * 
     * What this does:
     * Unsubscribes from the local EventBus.
     * 
     * Why this matters:
     * Without this, the EventBus could call methods on destroyed objects.
     */
    private void UnregisterUIEvents()
    {
        if (selectedDiceBinding != null)
            EventBus<SelectedDiceType>.Unsubscribe(selectedDiceBinding);

        if (stakeRollBinding != null)
            EventBus<StakeRoll>.Unsubscribe(stakeRollBinding);
    }

    /*
     * RegisterServerMessages
     * 
     * What this does:
     * Registers all server messages that are important during the game scene.
     * 
     * How it works:
     * Client.Instance owns the OSC dispatcher.
     * This script tells the Client which OSC address should call which method.
     * 
     * Example:
     * Msg.S_DICE_ROLLED will call OnDiceRolled.
     * 
     * The OSCUtil arguments are used as basic payload/type expectations.
     * Some messages have dynamic length, so they do not list all arguments here.
     */
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

    /*
     * UnregisterServerMessages
     * 
     * What this does:
     * Removes all server message listeners added by this controller.
     * 
     * Why this matters:
     * When the scene changes, this GameController should stop receiving game messages.
     */
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

    /*
     * RegisterButtons
     * 
     * What this does:
     * Connects the disconnect button to the client disconnect function.
     */
    private void RegisterButtons()
    {
        if (view.disconnectButton != null)
            view.disconnectButton.onClick.AddListener(OnDisconnectButtonClicked);
    }

    /*
     * UnregisterButtons
     * 
     * What this does:
     * Removes button listener when this object is destroyed.
     */
    private void UnregisterButtons()
    {
        if (view != null && view.disconnectButton != null)
            view.disconnectButton.onClick.RemoveListener(OnDisconnectButtonClicked);
    }

    #endregion

    #region Received Messages

    /*
     * SERVER MESSAGE: Msg.S_YOUR_TURN
     * 
     * Payload received:
     * [0] string message
     * 
     * Example:
     * "It is your turn."
     * 
     * What this does:
     * Shows the message from the server in the announcement UI.
     */
    private void OnYourTurn(OSCMessageIn msg, IPEndPoint sender)
    {
        string message = msg.ReadString();

        announcementsView.Show(message);
    }

    /*
     * SERVER MESSAGE: Msg.S_TURN_STARTED
     * 
     * Payload received:
     * [0] int currentPlayerId
     * [1] string currentPlayerName
     * 
     * Example:
     * currentPlayerId = 2
     * currentPlayerName = "Nik"
     * 
     * What this does:
     * Updates the client-side turn state.
     * Clears old turn dice.
     * Resets temporary turn stats.
     * Shows whose turn it is.
     */
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

        announcementsView.Show($"{currentPlayerName}'s turn.");
    }

    /*
     * SERVER MESSAGE: Msg.S_DICE_ROLLED
     * 
     * Payload received:
     * [0] int currentPlayerId
     * [1] int diceCount
     * [2...] int dice values repeated diceCount times
     * After the dice list:
     * int turnPoints
     * int defense
     * int attack
     * bool doubleStakeActive
     * 
     * Example with 5 dice:
     * [0] currentPlayerId = 1
     * [1] diceCount = 5
     * [2] dice 1 = 4
     * [3] dice 2 = 2
     * [4] dice 3 = 6
     * [5] dice 4 = 1
     * [6] dice 5 = 3
     * [7] turnPoints = 100
     * [8] defense = 2
     * [9] attack = 1
     * [10] doubleStakeActive = false
     * 
     * What this does:
     * Updates the dice visuals after the server rolls dice.
     * The client does not roll the dice locally.
     * It only displays the dice values received from the server.
     */
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

    /*
     * SERVER MESSAGE: Msg.S_DICE_SELECTED
     * 
     * Payload received:
     * [0] int diceType
     * 
     * Example:
     * diceType = 1
     * 
     * What this does:
     * Moves the selected dice type to the selected dice zone in the UI.
     * Also predicts or updates the visible stats for that selected dice type.
     * 
     * Important:
     * This message comes from the server.
     * That means the server accepted the selection before the UI moves it.
     */
    private void OnDiceSelected(OSCMessageIn msg, IPEndPoint sender)
    {
        int diceType = msg.ReadInt();

        view.MoveSelectedDiceToZone(diceType);
        view.PredictSelectedDiceStats(diceType);
    }

    /*
     * SERVER MESSAGE: Msg.S_TURN_OPTIONS
     * 
     * Payload received:
     * [0] int selectableCount
     * Then repeated selectableCount times:
     *     int diceType
     *     bool isSelectable
     * 
     * Example:
     * selectableCount = 3
     * diceType = 1, isSelectable = true
     * diceType = 5, isSelectable = true
     * diceType = 2, isSelectable = false
     * 
     * What this does:
     * Tells the client which dice types can be selected.
     * This is usually sent privately to the current player.
     * 
     * Important:
     * The server decides what is selectable.
     * The client only uses this to enable or disable UI clicking.
     */
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

    /*
     * SERVER MESSAGE: Msg.S_GAME_STATE
     * 
     * Payload received:
     * [0] int currentTurnIndex
     * [1] int playerCount
     * Then repeated playerCount times:
     *     string name
     *     int points
     * 
     * Example:
     * currentTurnIndex = 0
     * playerCount = 2
     * name = "Nik", points = 500
     * name = "Alex", points = 350
     * 
     * What this does:
     * Rebuilds or updates the player score list on the UI.
     * 
     * Note for reviewer:
     * currentTurnIndex is currently read but not used in this method.
     * The UI may already use other messages for turn display.
     */
    private void OnGameState(OSCMessageIn msg, IPEndPoint sender)
    {
        int currentTurnIndex = msg.ReadInt();
        int playerCount = msg.ReadInt();

        int pointGoal = Client.Instance.CurrentPointGoal;

        view.ClearUsers();

        for (int i = 0; i < playerCount; i++)
        {
            string name = msg.ReadString();
            int points = msg.ReadInt();

            view.UpdateOrAddUser(name, points, pointGoal);
        }
    }

    /*
     * SERVER MESSAGE: Msg.S_GAME_ANNOUNCEMENT
     * 
     * Payload received:
     * [0] string text
     * 
     * Example:
     * "Nik rolled 5 dice."
     * 
     * What this does:
     * Shows a general gameplay announcement.
     */
    private void OnAnnouncement(OSCMessageIn msg, IPEndPoint sender)
    {
        string text = msg.ReadString();

        announcementsView.Show(text);
    }

    /*
     * SERVER MESSAGE: Msg.S_ROUND_RESULTS
     * 
     * Payload received:
     * [0] string result
     * 
     * Example:
     * "Nik gained 300 points this turn."
     * 
     * What this does:
     * Publishes the result text into the local EventBus.
     * Another view can listen to RoundResults and display it.
     */
    private void OnRoundResults(OSCMessageIn msg, IPEndPoint sender)
    {
        string result = msg.ReadString();

        EventBus<RoundResults>.Publish(new RoundResults(result));
    }

    /*
     * SERVER MESSAGE: Msg.S_STAKE_PROMPT
     * 
     * Payload received:
     * No data is read in this method.
     * 
     * What this does:
     * Shows the roll again / stake choice UI to the current player.
     * 
     * Important:
     * This message is expected to be sent privately to the current player.
     * Because of that, _isMyTurn is set to true here.
     */
    private void OnStakePrompt(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Game", "Stake prompt received.");

        // The stake prompt is sent privately to the current player.
        // Keep this true so the RollAgainView buttons can send Msg.C_STAKE_ANSWER,
        // even if an older local turn flag was wrong or stale.
        _isMyTurn = true;

        if (view != null)
        {
            view.EnableDiceSelection(false);
            view.SetTurnIndicator(true);
        }

        if (rollAgainView == null)
        {
            Client.Log("GameController", "RollAgainView reference is missing.");
            return;
        }

        rollAgainView.Show();

        Client.Log("Game", "RollAgainView.Show() called.");
    }

    /*
     * SERVER MESSAGE: Msg.S_INVALID_MOVE
     * 
     * Payload received:
     * [0] string reason
     * 
     * Example:
     * "Invalid dice selection."
     * 
     * What this does:
     * Shows the error reason to the player.
     * If it is this player's turn, it restores the last selectable dice state.
     */
    private void OnInvalidMove(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();

        announcementsView.Show(reason);

        if (_isMyTurn)
            view.SetDiceSelectable(_lastSelectableDice, true);
    }

    /*
     * SERVER MESSAGE: Msg.S_REMATCH_UPDATE
     * 
     * Payload received:
     * [0] int readyCount
     * [1] int neededCount
     * 
     * Example:
     * readyCount = 1
     * neededCount = 2
     * 
     * What this does:
     * Updates the rematch status on the game over screen.
     */
    private void OnRematchUpdate(OSCMessageIn msg, IPEndPoint sender)
    {
        int readyCount = msg.ReadInt();
        int neededCount = msg.ReadInt();

        Client.Log("Game", $"Rematch update: {readyCount}/{neededCount}");

        if (gameOverView != null)
            gameOverView.SetRematchStatus(readyCount, neededCount);
    }

    /*
     * SERVER MESSAGE: Msg.S_REMATCH_STARTED
     * 
     * Payload received:
     * No data is read in this method.
     * 
     * What this does:
     * Hides the game over screen and resets the visible game UI for a new match.
     */
    private void OnRematchStarted(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Game", "Rematch started.");

        if (gameOverView != null)
            gameOverView.Hide();

        view.ClearTurnDiceZones();
        view.SyncTurnStats(0, 0, 0, false);
        view.EnableDiceSelection(false);
    }

    /*
     * SERVER MESSAGE: Msg.S_RETURN_TO_LOBBY
     * 
     * Payload received:
     * [0] string reason
     * 
     * Example:
     * "Room closed."
     * 
     * What this does:
     * Clears the current room and loads the Lobby scene.
     */
    private void OnReturnToLobby(OSCMessageIn msg, IPEndPoint sender)
    {
        string reason = msg.ReadString();

        Client.Log("Game", "Returning to lobby: " + reason);

        Client.Instance.CurrentRoom = null;

        SceneManager.LoadSceneAsync(Scenes.Lobby);
    }

    /*
     * SERVER MESSAGE: Msg.S_GAME_END
     * 
     * Payload received:
     * [0] string message
     * 
     * Example:
     * "Nik won the game."
     * 
     * What this does:
     * Stops dice selection and shows the game over UI.
     */
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

    /*
     * LOCAL EVENT: SelectedDiceType
     * 
     * Data received:
     * e.diceType = the dice type/value selected by the player.
     * 
     * What this does:
     * Converts the local UI event into a network message.
     */
    private void OnSelectedDice(SelectedDiceType e)
    {
        SendSelectDice(e.diceType);
    }

    /*
     * LOCAL EVENT: StakeRoll
     * 
     * Data received:
     * e.doReRoll = true if the player wants to roll again.
     * e.doReRoll = false if the player wants to stop/bank.
     * 
     * What this does:
     * Sends the stake answer to the server.
     */
    private void OnStakeChoice(StakeRoll e)
    {
        SendStakeAnswer(e.doReRoll);
    }

    /*
     * UI BUTTON: Disconnect
     * 
     * What this does:
     * Disconnects the client from the server.
     */
    private void OnDisconnectButtonClicked()
    {
        Client.Instance.Disconnect();
    }

    #endregion

    #region Sending Messages

    /*
     * CLIENT MESSAGE: Msg.C_GAME_SCENE_READY
     * 
     * Payload sent:
     * No data.
     * 
     * What this tells the server:
     * This client has loaded the game scene and is ready to receive game messages.
     */
    private void SendGameSceneReady()
    {
        var msg = new OSCMessageOut(Msg.C_GAME_SCENE_READY);

        Client.Instance.Send(msg);

        Client.Log("Game", "Sent game scene ready.");
    }

    /*
     * CLIENT MESSAGE: Msg.C_SELECT_DICE
     * 
     * Payload sent:
     * [0] int diceType
     * 
     * Example:
     * diceType = 1
     * 
     * What this tells the server:
     * The player wants to select this dice type.
     * 
     * Important:
     * This method refuses to send if it is not this client's turn.
     * The server still validates the move again.
     */
    private void SendSelectDice(int diceType)
    {
        if (!_isMyTurn)
            return;

        var msg = new OSCMessageOut(Msg.C_SELECT_DICE)
            .AddInt(diceType);

        Client.Instance.Send(msg);

        view.EnableDiceSelection(false);
    }

    /*
     * CLIENT MESSAGE: Msg.C_STAKE_ANSWER
     * 
     * Payload sent:
     * [0] bool doReRoll
     * 
     * Example:
     * doReRoll = true  means the player wants to continue rolling.
     * doReRoll = false means the player does not want to continue.
     * 
     * What this tells the server:
     * The player answered the stake/roll again prompt.
     */
    private void SendStakeAnswer(bool doReRoll)
    {
        if (!_isMyTurn)
            return;

        var msg = new OSCMessageOut(Msg.C_STAKE_ANSWER)
            .AddBool(doReRoll);

        Client.Instance.Send(msg);

        rollAgainView.Hide();
    }

    /*
     * CLIENT MESSAGE: Msg.C_REMATCH_REQUEST
     * 
     * Payload sent:
     * No data.
     * 
     * What this tells the server:
     * This client wants to play another match in the same room/session.
     */
    private void SendRematchRequest()
    {
        var msg = new OSCMessageOut(Msg.C_REMATCH_REQUEST);

        Client.Instance.Send(msg);

        Client.Log("Game", "Sent rematch request.");
    }

    /*
     * CLIENT MESSAGE: Msg.C_LEAVE_GAME
     * 
     * Payload sent:
     * No data.
     * 
     * What this tells the server:
     * This client wants to leave the current game.
     */
    private void SendLeaveGame()
    {
        var msg = new OSCMessageOut(Msg.C_LEAVE_GAME);

        Client.Instance.Send(msg);

        Client.Log("Game", "Sent leave game.");
    }

    #endregion

    #region Helpers

    /*
     * ReturnToLobby
     * 
     * What this does:
     * Loads the Lobby scene.
     * 
     * Note:
     * This helper is not currently used in this script.
     * OnReturnToLobby uses SceneManager.LoadSceneAsync directly.
     */
    private void ReturnToLobby()
    {
        SceneManager.LoadScene(Scenes.Lobby);
    }

    #endregion
}