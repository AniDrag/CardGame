using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace CreeperDice_Net_Proj.Model
{
    /*
     * GameState
     *
     * Purpose:
     * This class controls the server-side game flow for Creeper Dice.
     *
     * It is responsible for:
     * - Registering game-related OSC messages from clients.
     * - Waiting until all players have loaded the game scene.
     * - Starting the game for a room.
     * - Starting and ending turns.
     * - Rolling dice through GameData.
     * - Validating if a client is allowed to act.
     * - Handling dice selection.
     * - Handling stake / double danger choices.
     * - Handling rematch requests.
     * - Handling players leaving the game.
     * - Sending game state updates to clients.
     *
     * Naming rule used:
     * - On prefix = receives an OSC message from a client.
     * - Send prefix = sends an OSC message to one client or a room.
     * - No On prefix = normal server logic, helper, validation, timeout, or logging.
     *
     * Important:
     * This is server-side authoritative logic.
     * Clients can request actions, but this class checks if the action is valid.
     */

    public class GameState
    {
        #region Fields
        private readonly TcpServer _server;

        /*
         * _readyPlayersByRoom:
         * Tracks which players have loaded the game scene.
         *
         * Key:
         * room name.
         *
         * Value:
         * client ids that sent Msg.C_GAME_SCENE_READY.
         */
        private readonly Dictionary<string, HashSet<int>> _readyPlayersByRoom = new();
        private readonly HashSet<string> _startedRooms = new();
        private readonly Dictionary<string, HashSet<int>> _rematchVotesByRoom = new();
        private readonly Dictionary<string, DateTime> _stakePromptTimesByRoom = new();
        private readonly TimeSpan _stakeAnswerTimeout = TimeSpan.FromSeconds(60);
        private static readonly int _defaultDiceCount = 13;

        #endregion

        #region Constructor

        public GameState(TcpServer server)
        {
            _server = server;
            RegisterHandlers();
        }

        #endregion

        #region Message Registration

        /*
         * What this does:
         * Registers all client-to-server OSC game messages.
         *
         * OSC received:
         *
         * Msg.C_GAME_SCENE_READY
         * Payload:
         * No data.
         *
         * Msg.C_SELECT_DICE
         * Payload:
         * [0] int diceType
         *
         * Msg.C_STAKE_ANSWER
         * Payload:
         * [0] bool doReRollOrDoubleStake
         *
         * Msg.C_REMATCH_REQUEST
         * Payload:
         * No data.
         *
         * Msg.C_LEAVE_GAME
         * Payload:
         * No data.
         */
        private void RegisterHandlers()
        {
            OSCDispatcher dispatcher = _server.Dispatcher;

            dispatcher.AddListener(Msg.C_GAME_SCENE_READY, OnGameSceneReady);
            dispatcher.AddListener(Msg.C_SELECT_DICE, OnSelectDice, OSCUtil.INT);
            dispatcher.AddListener(Msg.C_STAKE_ANSWER, OnStakeAnswer, OSCUtil.BOOL);
            dispatcher.AddListener(Msg.C_REMATCH_REQUEST, OnRematchRequest);
            dispatcher.AddListener(Msg.C_LEAVE_GAME, OnLeaveGame);

            LogGame("Registered game message handlers.");
        }

        #endregion

        #region Game Flow
        public void Update()
        {
            CheckStakeAnswerTimeouts();
        }

        /*
         * What this does:
         * Starts the game logic for a room.
         *
         * Data received:
         * room = room that should begin its game.
         *
         * Flow:
         * 1. Validate room exists.
         * 2. Validate room has participants.
         * 3. Create fresh GameData.
         * 4. Store turn order from room participants.
         * 5. Set current player index to 0.
         * 6. Send initial game state.
         * 7. Start the first turn.
         */
        public void StartGameForRoom(RoomData room)
        {
            if (room == null)
            {
                LogGame("Cannot start game. Room is null.");
                return;
            }

            if (room.Participants == null || room.Participants.Count == 0)
            {
                LogGame(room, "Cannot start game. Room has no participants.");
                return;
            }

            LogGame(room, $"Starting game. Participants={room.Participants.Count}. Goal={room.pointGoal}.");

            room.data = new GameData(_defaultDiceCount);
            room.data.ParticipantOrder = room.Participants.Select(p => p.id).ToList();
            room.data.CurrentPlayerIndex = 0;
            room.data.Phase = RoomGamePhase.NotStarted;

            LogGame(room, "Turn order: " + string.Join(", ", room.data.ParticipantOrder));
            LogGame(room, "Initial scores: " + FormatScores(room));

            SendGameState(room);
            StartTurn(room);
        }

        /*
         * What this does:
         * Starts one player's turn.
         *
         * Flow:
         * 1. Reset turn data.
         * 2. Find current player.
         * 3. Tell clients whose turn started.
         * 4. Send announcement.
         * 5. Send current game state.
         * 6. Roll dice for the current player.
         */
        private void StartTurn(RoomData room)
        {
            try
            {
                GameData data = room.data;

                data.ResetTurn();

                Participant currentPlayer = CurrentParticipant(room);

                LogGame(room, $"Turn started for {currentPlayer.clientName} (ID {currentPlayer.id}).");
                LogGame(room, "After reset: " + FormatTurnStats(data));

                SendTurnStarted(room, currentPlayer);
                SendGameAnnouncement(room, $"{currentPlayer.clientName}'s turn.");
                SendYourTurn(room, currentPlayer);
                SendGameState(room);

                RollForRoom(room);
            }
            catch (Exception ex)
            {
                LogGameError("StartTurn failed", ex);
                EndGame(room, "Game ended because the server failed to start a turn.");
            }
        }

        /*
         * What this does:
         * Rolls dice for the current room/player.
         *
         * Flow:
         * 1. Set phase to Rolling.
         * 2. Call GameData.RollDice().
         * 3. Send dice results to all clients in room.
         * 4. Check instant defense bust.
         * 5. Check if there are no selectable dice.
         * 6. Send selectable dice options to current player.
         * 7. Set phase to WaitingForDiceSelection.
         *
         * Important:
         * Dice are rolled by the server.
         * Clients only display the result.
         */
        private void RollForRoom(RoomData room)
        {
            try
            {
                GameData data = room.data;

                data.Phase = RoomGamePhase.Rolling;

                LogGame(room, $"Rolling {data.DiceToRoll} dice.");

                data.RollDice();

                LogGame(room, "Rolled: " + FormatRoll(data.CurrentRoll));
                LogGame(room, "After roll: " + FormatTurnStats(data));

                SendDiceRolledToRoom(room);

                if (data.IsUnrecoverableDefenseBust())
                {
                    LogGame(room, $"Instant bust. ATK={data.CurrentAttack}, DEF={data.CurrentDefense}, DiceToRoll={data.DiceToRoll}. Cannot possibly survive.");
                    EndTurn(room, busted: true, "Attack is too high to recover.");
                    return;
                }

                if (!data.HasSelectableDice())
                {
                    bool canBank = data.CanBankPoints();

                    LogGame(room, $"No selectable dice. CanBank={canBank}. ATK={data.CurrentAttack}, DEF={data.CurrentDefense}, Points={data.CurrentPoints}.");

                    if (canBank)
                    {
                        EndTurn(room, busted: false, "No selectable dice left, but defense held.");
                    }
                    else
                    {
                        EndTurn(room, busted: true, "No selectable dice left and defense did not hold.");
                    }

                    return;
                }

                SendTurnOptionsToCurrentPlayer(room);

                data.Phase = RoomGamePhase.WaitingForDiceSelection;

                LogGame(room, "Waiting for dice selection.");
            }
            catch (Exception ex)
            {
                LogGameError("RollForRoom failed", ex);
                EndTurn(room, busted: true, "Server failed while rolling.");
            }
        }

        /*
         * What this does:
         * Checks rooms waiting for a stake answer.
         *
         * If the current player does not answer in time:
         * - If they can bank, the server banks points automatically.
         * - If they cannot bank, the server busts the turn.
         */
        private void CheckStakeAnswerTimeouts()
        {
            foreach (KeyValuePair<string, DateTime> pair in _stakePromptTimesByRoom.ToList())
            {
                string roomName = pair.Key;
                DateTime promptTime = pair.Value;

                if (DateTime.UtcNow - promptTime < _stakeAnswerTimeout)
                    continue;

                if (!_server.TryGetRoom(roomName, out RoomData room))
                {
                    _stakePromptTimesByRoom.Remove(roomName);
                    continue;
                }

                if (room.data == null || room.data.Phase != RoomGamePhase.WaitingForStakeAnswer)
                {
                    _stakePromptTimesByRoom.Remove(roomName);
                    continue;
                }

                LogGame(room, "Stake answer timeout. Auto-resolving turn.");

                _stakePromptTimesByRoom.Remove(roomName);

                if (room.data.CanBankPoints())
                    EndTurn(room, busted: false, "Stake answer timed out. Points were banked automatically.");
                else
                    EndTurn(room, busted: true, "Stake answer timed out and points could not be banked.");
            }
        }

        /*
         * What this does:
         * Ends the current player's turn.
         *
         * Data received:
         * busted = true if the player lost the turn.
         * reason = explanation for logs and announcements.
         *
         * If busted:
         * Player gains 0 points.
         *
         * If not busted:
         * Player banks CurrentPoints * ScoreMultiplier.
         *
         * Double danger note:
         * In this current version, GameState applies ScoreMultiplier at bank time.
         * That means double danger/double stake doubles the final banked turn points.
         */
        private void EndTurn(RoomData room, bool busted, string reason)
        {
            try
            {
                GameData data = room.data;

                data.Phase = RoomGamePhase.TurnEnding;

                Participant player = CurrentParticipant(room);

                LogGame(room, $"Ending turn for {player.clientName}. Busted={busted}. Reason={reason}.");

                if (busted)
                {
                    LogGame(room, $"{player.clientName} gained 0 points this turn.");
                    SendGameAnnouncement(room, $"{player.clientName} busted. {reason}");
                }
                else
                {
                    int earnedPoints = data.CurrentPoints * data.ScoreMultiplier;
                    player.currPoints += earnedPoints;

                    LogGame(room, $"{player.clientName} banked {earnedPoints} points. New total={player.currPoints}.");
                    SendGameAnnouncement(room, $"{player.clientName} banked {earnedPoints} points.");
                }

                LogGame(room, "Scores: " + FormatScores(room));

                SendGameState(room);

                if (player.currPoints >= room.pointGoal)
                {
                    LogGame(room, $"{player.clientName} reached the goal and wins.");
                    EndGame(room, $"{player.clientName} wins with {player.currPoints} points!");
                    return;
                }

                if (data.ParticipantOrder == null || data.ParticipantOrder.Count == 0 || room.Participants.Count == 0)
                {
                    LogGame(room, "No players left after turn end. Ending game.");
                    EndGame(room, "Game ended because no players are left.");
                    return;
                }

                data.CurrentPlayerIndex = (data.CurrentPlayerIndex + 1) % data.ParticipantOrder.Count;

                LogGame(room, $"Next player index: {data.CurrentPlayerIndex}.");

                StartTurn(room);
            }
            catch (Exception ex)
            {
                LogGameError("EndTurn failed", ex);
                EndGame(room, "Game ended because the server failed to end a turn.");
            }
        }

        /*
         * What this does:
         * Starts a new match in the same room.
         *
         * Flow:
         * 1. Clear rematch votes.
         * 2. Reset participant scores.
         * 3. Tell clients rematch started.
         * 4. Start game again for the same room.
         */
        private void StartRematch(RoomData room)
        {
            if (room == null)
                return;

            LogGame(room, "Starting rematch.");

            if (_rematchVotesByRoom.ContainsKey(room.roomName))
                _rematchVotesByRoom[room.roomName].Clear();

            foreach (Participant participant in room.Participants)
                participant.currPoints = 0;

            SendRematchStarted(room);

            StartGameForRoom(room);
        }

        /*
         * What this does:
         * Checks if all current participants voted for rematch.
         */
        private bool ShouldStartRematch(RoomData room)
        {
            if (room == null)
                return false;

            if (room.Participants == null || room.Participants.Count == 0)
                return false;

            if (!_rematchVotesByRoom.TryGetValue(room.roomName, out HashSet<int> votes))
                return false;

            return votes.Count >= room.Participants.Count;
        }

        private void CloseRoomBecauseHostLeft(RoomData room)
        {
            if (room == null)
                return;

            LogGame(room, "Host left. Closing room and returning everyone to lobby.");

            SendReturnToLobby(room, "Host left. Room closed.");

            foreach (Participant participant in room.Participants)
            {
                ClientInfo client = _server.FindPlayerById(participant.id);

                if (client != null)
                    client.CurrentRoom = null;
            }

            CleanupRoom(room);
        }
        private void RemovePlayerFromRoom(RoomData room, ClientInfo client)
        {
            if (room == null || client == null)
                return;

            room.Participants.RemoveAll(participant => participant.id == client.Id);

            HandlePlayerRemovedFromGame(room, client.Id, $"{client.Name} left the game.");

            client.CurrentRoom = null;

            if (_rematchVotesByRoom.TryGetValue(room.roomName, out HashSet<int> votes))
                votes.Remove(client.Id);

            LogGame(room, $"Removed {client.Name}. Players left={room.Participants.Count}");
        }
        private void CleanupRoom(RoomData room)
        {
            if (room == null)
                return;

            LogGame(room, "Cleaning up room.");

            _readyPlayersByRoom.Remove(room.roomName);
            _startedRooms.Remove(room.roomName);
            _rematchVotesByRoom.Remove(room.roomName);
            _stakePromptTimesByRoom.Remove(room.roomName);

            _server.RemoveRoom(room.roomName);
        }

        /*
         * What this does:
         * Updates game state if a player is removed while the game exists.
         *
         * Important cases:
         * - Remove player from ready list.
         * - Remove player from rematch votes.
         * - Remove player from turn order.
         * - Fix CurrentPlayerIndex.
         * - If the removed player was the current player, start the next player's turn.
         */
        public void HandlePlayerRemovedFromGame(RoomData room, int removedClientId, string reason)
        {
            if (room == null)
                return;

            if (_readyPlayersByRoom.TryGetValue(room.roomName, out HashSet<int> readyPlayers))
                readyPlayers.Remove(removedClientId);

            if (_rematchVotesByRoom.TryGetValue(room.roomName, out HashSet<int> rematchVotes))
                rematchVotes.Remove(removedClientId);

            GameData data = room.data;

            if (data == null || data.ParticipantOrder == null || data.ParticipantOrder.Count == 0)
                return;

            int removedIndex = data.ParticipantOrder.IndexOf(removedClientId);

            if (removedIndex < 0)
                return;

            bool removedCurrentPlayer = data.CurrentPlayerIndex >= 0 &&
                                        data.CurrentPlayerIndex < data.ParticipantOrder.Count &&
                                        data.ParticipantOrder[data.CurrentPlayerIndex] == removedClientId;

            data.ParticipantOrder.RemoveAt(removedIndex);

            if (data.ParticipantOrder.Count == 0 || room.Participants.Count == 0)
            {
                _stakePromptTimesByRoom.Remove(room.roomName);
                LogGame(room, "All players left the active game.");
                return;
            }

            if (removedIndex < data.CurrentPlayerIndex)
                data.CurrentPlayerIndex--;

            if (data.CurrentPlayerIndex < 0 || data.CurrentPlayerIndex >= data.ParticipantOrder.Count)
                data.CurrentPlayerIndex = 0;

            _stakePromptTimesByRoom.Remove(room.roomName);

            if (!room.GameStarted || data.Phase == RoomGamePhase.NotStarted || data.Phase == RoomGamePhase.Finished)
                return;

            SendGameAnnouncement(room, reason);
            SendGameState(room);

            if (!removedCurrentPlayer)
                return;

            LogGame(room, "Current player was removed. Starting the next available player's turn.");
            StartTurn(room);
        }

        /*
         * What this does:
         * Finishes the game for the room.
         *
         * Flow:
         * 1. Set phase to Finished.
         * 2. Send game end message to clients.
         * 3. Create rematch vote list if needed.
         * 4. Send rematch update.
         */
        private void EndGame(RoomData room, string message)
        {
            if (room == null)
                return;

            LogGame(room, "Game ended. " + message);

            if (room.data != null)
                room.data.Phase = RoomGamePhase.Finished;

            SendGameEnd(room, message);

            if (!_rematchVotesByRoom.ContainsKey(room.roomName))
                _rematchVotesByRoom[room.roomName] = new HashSet<int>();

            SendRematchUpdate(room);
        }

        #endregion

        #region Received OSC Messages

        /*
         * OSC RECEIVE: Msg.C_GAME_SCENE_READY
         *
         * Payload received:
         * No data.
         *
         * What this means:
         * A client loaded the game scene and is ready.
         *
         * What this does:
         * Tracks ready players for the room.
         * When all players in the room are ready, the server starts the game.
         */
        private void OnGameSceneReady(OSCMessageIn msg, IPEndPoint sender)
        {
            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null)
            {
                LogGame($"Unknown endpoint sent game scene ready: {sender}");
                return;
            }

            LogGame($"{client.Name} sent game scene ready.");

            if (string.IsNullOrEmpty(client.CurrentRoom))
            {
                LogGame($"{client.Name} is ready, but has no current room.");
                return;
            }

            if (!_server.TryGetRoom(client.CurrentRoom, out RoomData room))
            {
                LogGame($"{client.Name} is ready, but room '{client.CurrentRoom}' was not found.");
                return;
            }

            if (!room.GameStarted)
            {
                LogGame(room, $"{client.Name} is ready, but room game has not been marked started.");
                return;
            }

            if (!_readyPlayersByRoom.TryGetValue(room.roomName, out HashSet<int> readyPlayers))
            {
                readyPlayers = new HashSet<int>();
                _readyPlayersByRoom[room.roomName] = readyPlayers;
            }

            readyPlayers.Add(client.Id);

            LogGame(room, $"{client.Name} ready. Ready players: {readyPlayers.Count}/{room.Participants.Count}");

            if (readyPlayers.Count < room.Participants.Count)
                return;

            if (_startedRooms.Contains(room.roomName))
            {
                LogGame(room, "All players ready, but game already started. Ignoring duplicate ready.");
                return;
            }

            _startedRooms.Add(room.roomName);

            LogGame(room, "All players ready. Starting game.");

            StartGameForRoom(room);
        }

        /*
         * OSC RECEIVE: Msg.C_SELECT_DICE
         *
         * Payload received:
         * [0] int diceType
         *
         * Example:
         * diceType = 0 means Human.
         * diceType = 1 means Cow.
         * diceType = 2 means Chicken.
         * diceType = 3 means Tank.
         * diceType = 4 means UFO.
         *
         * What this does:
         * Validates that the sender is the current player and that the game is waiting for dice selection.
         * Then it asks GameData to select the dice type.
         */
        private void OnSelectDice(OSCMessageIn msg, IPEndPoint sender)
        {
            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null)
            {
                LogGame($"Unknown endpoint tried to select dice: {sender}");
                return;
            }

            LogGame($"Received dice selection from {client.Name}.");

            if (!ValidateCurrentPlayer(client, out RoomData room, out GameData data))
                return;

            if (data.Phase != RoomGamePhase.WaitingForDiceSelection)
            {
                LogGame(room, $"{client.Name} tried to select dice during invalid phase: {data.Phase}.");
                SendInvalidMove(client, "You cannot select dice right now.");
                _server.AddMaliciousStrike(client);
                return;
            }

            int diceType = msg.ReadInt();

            LogGame(room, $"{client.Name} selected {DiceTypeName(diceType)}({diceType}).");

            if (!data.TrySelectDice(diceType, out string error))
            {
                LogGame(room, $"{client.Name}'s dice selection was rejected. Reason: {error}");

                SendInvalidMove(client, error);
                _server.AddMaliciousStrike(client);

                SendTurnOptionsToClient(room, client);
                return;
            }

            LogGame(room, $"{client.Name}'s selection accepted.");
            LogGame(room, "After selection: " + FormatTurnStats(data));

            SendDiceSelected(room, diceType);
            SendGameState(room);

            if (data.IsUnrecoverableDefenseBust())
            {
                LogGame(room, $"{client.Name} defense busted. ATK is higher than DEF and no dice are left.");
                EndTurn(room, busted: true, "Attack is higher than defense and no dice are left.");
                return;
            }

            if (data.DiceToRoll <= 0)
            {
                bool busted = !data.CanBankPoints();

                LogGame(room, $"No dice left. CanBank={data.CanBankPoints()}, Busted={busted}.");

                EndTurn(
                    room,
                    busted,
                    busted ? "You cannot bank points." : "No dice left."
                );

                return;
            }

            if (data.CanBankPoints())
            {
                data.Phase = RoomGamePhase.WaitingForStakeAnswer;

                LogGame(room, $"{client.Name} can bank points. Sending stake prompt.");
                SendStakePrompt(client);
                return;
            }

            LogGame(room, $"{client.Name} cannot bank yet. Rolling again.");
            RollForRoom(room);
        }

        /*
         * OSC RECEIVE: Msg.C_STAKE_ANSWER
         *
         * Payload received:
         * [0] bool doReRollOrDoubleStake
         *
         * Meaning:
         * false = player chooses to bank points.
         * true  = player chooses double danger / continue rolling.
         *
         * Current behavior:
         * If true, this calls GameData.EnableDoubleStake() and rolls again.
         * In the current code, EndTurn uses data.ScoreMultiplier when banking.
         */
        private void OnStakeAnswer(OSCMessageIn msg, IPEndPoint sender)
        {
            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null)
            {
                LogGame($"Unknown endpoint sent stake answer: {sender}");
                return;
            }

            LogGame($"Received stake answer from {client.Name}.");

            if (!ValidateCurrentPlayer(client, out RoomData room, out GameData data))
                return;

            _stakePromptTimesByRoom.Remove(room.roomName);

            if (data.Phase != RoomGamePhase.WaitingForStakeAnswer)
            {
                LogGame(room, $"{client.Name} tried to answer stake during invalid phase: {data.Phase}.");
                SendInvalidMove(client, "You are not being asked to stake right now.");
                _server.AddMaliciousStrike(client);
                return;
            }

            bool doReRollOrDoubleStake = msg.ReadBool();

            if (!doReRollOrDoubleStake)
            {
                LogGame(room, $"{client.Name} chose to bank points.");
                EndTurn(room, busted: false, "Player chose to bank points.");
                return;
            }

            LogGame(room, $"{client.Name} chose double stake / continue rolling.");

            data.EnableDoubleStake();

            LogGame(room, "After enabling double stake: " + FormatTurnStats(data));

            SendGameAnnouncement(room, $"{client.Name} chose double stake.");
            RollForRoom(room);
        }

        /*
         * OSC RECEIVE: Msg.C_REMATCH_REQUEST
         *
         * Payload received:
         * No data.
         *
         * What this does:
         * Adds the player to the rematch vote list.
         * If all current participants voted, a rematch starts.
         */
        private void OnRematchRequest(OSCMessageIn msg, IPEndPoint sender)
        {
            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null)
            {
                LogGame($"Unknown endpoint sent rematch request: {sender}");
                return;
            }

            if (!TryGetClientRoom(client, out RoomData room))
            {
                SendInvalidMove(client, "You are not in a room.");
                return;
            }

            if (room.data == null || room.data.Phase != RoomGamePhase.Finished)
            {
                LogGame(room, $"{client.Name} tried to rematch before the game ended.");
                SendInvalidMove(client, "You can only rematch after the game ends.");
                return;
            }

            if (!_rematchVotesByRoom.TryGetValue(room.roomName, out HashSet<int> votes))
            {
                votes = new HashSet<int>();
                _rematchVotesByRoom[room.roomName] = votes;
            }

            votes.Add(client.Id);

            LogGame(room, $"{client.Name} wants rematch. Votes={votes.Count}/{room.Participants.Count}");

            SendRematchUpdate(room);

            if (votes.Count >= room.Participants.Count)
            {
                StartRematch(room);
            }
        }

        /*
         * OSC RECEIVE: Msg.C_LEAVE_GAME
         *
         * Payload received:
         * No data.
         *
         * What this does:
         * Removes a player from the active game.
         * If the host leaves, the room closes.
         */
        private void OnLeaveGame(OSCMessageIn msg, IPEndPoint sender)
        {
            ClientInfo client = _server.GetClientByEndpoint(sender);

            if (client == null)
            {
                LogGame($"Unknown endpoint sent leave game: {sender}");
                return;
            }

            if (!TryGetClientRoom(client, out RoomData room))
            {
                SendReturnToLobby(client, "You are not in a room.");
                return;
            }

            bool isHost = room.host == client.Name;

            LogGame(room, $"{client.Name} left the game. IsHost={isHost}");

            if (isHost)
            {
                CloseRoomBecauseHostLeft(room);
                return;
            }

            RemovePlayerFromRoom(room, client);

            SendReturnToLobby(client, "You left the game.");

            if (room.Participants.Count <= 0)
            {
                CleanupRoom(room);
                return;
            }

            SendGameAnnouncement(room, $"{client.Name} left the game.");
            SendRematchUpdate(room);

            if (ShouldStartRematch(room))
                StartRematch(room);
        }

        #endregion

        #region Sending Messages

        /*
         * OSC SEND: Msg.S_YOUR_TURN
         *
         * Payload sent:
         * [0] string message
         */
        private void SendYourTurn(RoomData room, Participant player)
        {
            LogGame(room, $"SEND {Msg.S_YOUR_TURN} -> {player.clientName}'s turn.");

            var msg = new OSCMessageOut(Msg.S_YOUR_TURN)
                .AddString($"{player.clientName}'s turn");

            _server.BroadcastToRoom(room, msg);
        }

        /*
         * OSC SEND: Msg.S_TURN_STARTED
         *
         * Payload sent:
         * [0] int currentPlayerId
         * [1] string currentPlayerName
         */
        private void SendTurnStarted(RoomData room, Participant player)
        {
            LogGame(room, $"SEND {Msg.S_TURN_STARTED} -> currentPlayerId={player.id}, currentPlayerName={player.clientName}");

            var msg = new OSCMessageOut(Msg.S_TURN_STARTED)
                .AddInt(player.id)
                .AddString(player.clientName);

            _server.BroadcastToRoom(room, msg);
        }

        /*
         * OSC SEND: Msg.S_DICE_ROLLED
         *
         * Payload sent:
         * [0] int ownerClientId
         * [1] int diceCount
         * [2...] int diceType repeated diceCount times
         * After dice list:
         * int currentPoints
         * int currentDefense
         * int currentAttack
         * bool doubleStakeActive
         */
        private void SendDiceRolledToRoom(RoomData room)
        {
            GameData data = room.data;
            ClientInfo currentClient = CurrentClient(room);

            if (currentClient == null)
            {
                LogGame(room, "Cannot send dice rolled. CurrentClient is null.");
                EndGame(room, "Game ended because current player could not be found.");
                return;
            }

            var msg = new OSCMessageOut(Msg.S_DICE_ROLLED);

            msg.AddInt(currentClient.Id);

            IReadOnlyList<int> roll = data.CurrentRoll;

            msg.AddInt(roll.Count);

            foreach (int dice in roll)
                msg.AddInt(dice);

            msg.AddInt(data.CurrentPoints);
            msg.AddInt(data.CurrentDefense);
            msg.AddInt(data.CurrentAttack);
            msg.AddBool(data.DoubleStakeActive);

            LogGame(room, $"SEND {Msg.S_DICE_ROLLED} -> owner={currentClient.Id}, count={roll.Count}, roll=[{FormatRoll(roll)}], {FormatTurnStats(data)}");

            _server.BroadcastToRoom(room, msg);
        }

        /*
         * OSC SEND: Msg.S_DICE_SELECTED
         *
         * Payload sent:
         * [0] int diceType
         */
        private void SendDiceSelected(RoomData room, int diceType)
        {
            LogGame(room, $"SEND {Msg.S_DICE_SELECTED} -> diceType={DiceTypeName(diceType)}({diceType})");

            var msg = new OSCMessageOut(Msg.S_DICE_SELECTED)
                .AddInt(diceType);

            _server.BroadcastToRoom(room, msg);
        }

        /*
         * What this does:
         * Sends selectable dice only to the current player.
         */
        private void SendTurnOptionsToCurrentPlayer(RoomData room)
        {
            ClientInfo currentClient = CurrentClient(room);

            if (currentClient == null)
            {
                LogGame(room, "Cannot send turn options. CurrentClient is null.");
                return;
            }

            SendTurnOptionsToClient(room, currentClient);
        }

        /*
         * OSC SEND: Msg.S_TURN_OPTIONS
         *
         * Sent privately to one client.
         *
         * Payload sent:
         * [0] int selectableCount
         * Then repeated selectableCount times:
         *     int diceType
         *     bool isSelectable
         */
        private void SendTurnOptionsToClient(RoomData room, ClientInfo client)
        {
            Dictionary<int, bool> selectableDice = room.data.GetSelectableDice();

            LogGame(room, $"SEND {Msg.S_TURN_OPTIONS} -> to {client.Name}: {FormatSelectable(selectableDice)}");

            var msg = new OSCMessageOut(Msg.S_TURN_OPTIONS)
                .AddInt(selectableDice.Count);

            foreach (KeyValuePair<int, bool> pair in selectableDice)
            {
                msg.AddInt(pair.Key);
                msg.AddBool(pair.Value);
            }

            _server.Send(client.Connection, msg);
        }

        /*
         * OSC SEND: Msg.S_STAKE_PROMPT
         *
         * Sent privately to the current player.
         *
         * Payload sent:
         * No data.
         */
        private void SendStakePrompt(ClientInfo client)
        {
            LogGame($"SEND {Msg.S_STAKE_PROMPT} -> to {client.Name}");

            if (!string.IsNullOrEmpty(client.CurrentRoom))
                _stakePromptTimesByRoom[client.CurrentRoom] = DateTime.UtcNow;

            var msg = new OSCMessageOut(Msg.S_STAKE_PROMPT);

            _server.Send(client.Connection, msg);
        }

        /*
         * OSC SEND: Msg.S_GAME_STATE
         *
         * Payload sent:
         * [0] int currentPlayerIndex
         * [1] int participantCount
         * Then repeated participantCount times:
         *     string playerName
         *     int currentScore
         */
        private void SendGameState(RoomData room)
        {
            GameData data = room.data;

            LogGame(room, $"SEND {Msg.S_GAME_STATE} -> CurrentPlayerIndex={data.CurrentPlayerIndex}, Scores=[{FormatScores(room)}]");

            var msg = new OSCMessageOut(Msg.S_GAME_STATE)
                .AddInt(data.CurrentPlayerIndex)
                .AddInt(room.Participants.Count);

            foreach (Participant participant in room.Participants)
            {
                msg.AddString(participant.clientName);
                msg.AddInt(participant.currPoints);
            }

            _server.BroadcastToRoom(room, msg);
        }

        /*
         * OSC SEND: Msg.S_GAME_ANNOUNCEMENT
         *
         * Payload sent:
         * [0] string message
         */
        private void SendGameAnnouncement(RoomData room, string message)
        {
            LogGame(room, $"SEND {Msg.S_GAME_ANNOUNCEMENT} -> {message}");

            var msg = new OSCMessageOut(Msg.S_GAME_ANNOUNCEMENT)
                .AddString(message);

            _server.BroadcastToRoom(room, msg);
        }

        /*
         * OSC SEND: Msg.S_INVALID_MOVE
         *
         * Sent privately to one client.
         *
         * Payload sent:
         * [0] string reason
         */
        private void SendInvalidMove(ClientInfo client, string reason)
        {
            LogGame($"SEND {Msg.S_INVALID_MOVE} -> to {client?.Name ?? "NULL_CLIENT"}: {reason}");

            if (client == null)
                return;

            var msg = new OSCMessageOut(Msg.S_INVALID_MOVE)
                .AddString(reason);

            _server.Send(client.Connection, msg);
        }

        /*
         * OSC SEND: Msg.S_REMATCH_UPDATE
         *
         * Payload sent:
         * [0] int readyCount
         * [1] int neededCount
         */
        private void SendRematchUpdate(RoomData room)
        {
            if (room == null)
                return;

            int readyCount = 0;
            int neededCount = room.Participants != null ? room.Participants.Count : 0;

            if (_rematchVotesByRoom.TryGetValue(room.roomName, out HashSet<int> votes))
                readyCount = votes.Count;

            LogGame(room, $"SEND {Msg.S_REMATCH_UPDATE} -> {readyCount}/{neededCount}");

            var msg = new OSCMessageOut(Msg.S_REMATCH_UPDATE)
                .AddInt(readyCount)
                .AddInt(neededCount);

            _server.BroadcastToRoom(room, msg);
        }

        /*
         * OSC SEND: Msg.S_REMATCH_STARTED
         *
         * Payload sent:
         * No data.
         */
        private void SendRematchStarted(RoomData room)
        {
            LogGame(room, $"SEND {Msg.S_REMATCH_STARTED}");

            var msg = new OSCMessageOut(Msg.S_REMATCH_STARTED);

            _server.BroadcastToRoom(room, msg);
        }

        /*
         * OSC SEND: Msg.S_RETURN_TO_LOBBY
         *
         * Sent to one client.
         *
         * Payload sent:
         * [0] string reason
         */
        private void SendReturnToLobby(ClientInfo client, string reason)
        {
            if (client == null)
                return;

            LogGame($"SEND {Msg.S_RETURN_TO_LOBBY} -> to {client.Name}: {reason}");

            var msg = new OSCMessageOut(Msg.S_RETURN_TO_LOBBY)
                .AddString(reason);

            _server.Send(client.Connection, msg);
        }

        /*
         * OSC SEND: Msg.S_RETURN_TO_LOBBY
         *
         * Broadcast to full room.
         *
         * Payload sent:
         * [0] string reason
         */
        private void SendReturnToLobby(RoomData room, string reason)
        {
            if (room == null)
                return;

            LogGame(room, $"SEND {Msg.S_RETURN_TO_LOBBY} -> room: {reason}");

            var msg = new OSCMessageOut(Msg.S_RETURN_TO_LOBBY)
                .AddString(reason);

            _server.BroadcastToRoom(room, msg);
        }

        /*
         * OSC SEND: Msg.S_GAME_END
         *
         * Payload sent:
         * [0] string message
         */
        private void SendGameEnd(RoomData room, string message)
        {
            LogGame(room, $"SEND {Msg.S_GAME_END} -> {message}");

            var msg = new OSCMessageOut(Msg.S_GAME_END)
                .AddString(message);

            _server.BroadcastToRoom(room, msg);
        }

        #endregion

        #region Validation

        /*
         * What this does:
         * Checks if a client is allowed to perform current-player game actions.
         *
         * Used before:
         * - Selecting dice.
         * - Answering stake prompt.
         *
         * Validation checks:
         * - Client exists.
         * - Client is in a room.
         * - Room exists.
         * - Game has started.
         * - GameData exists.
         * - Turn order exists.
         * - CurrentPlayerIndex is valid.
         * - Client id matches current player id.
         *
         * If validation fails:
         * Sends invalid move message to the client.
         */
        private bool ValidateCurrentPlayer(ClientInfo client, out RoomData room, out GameData data)
        {
            room = null;
            data = null;

            if (client == null)
            {
                LogGame("ValidateCurrentPlayer failed: client is null.");
                return false;
            }

            if (string.IsNullOrEmpty(client.CurrentRoom))
            {
                LogGame($"{client.Name} failed validation: not in a room.");
                SendInvalidMove(client, "You are not in a room.");
                return false;
            }

            if (!_server.TryGetRoom(client.CurrentRoom, out room))
            {
                LogGame($"{client.Name} failed validation: room '{client.CurrentRoom}' not found.");
                SendInvalidMove(client, "Room not found.");
                return false;
            }

            if (!room.GameStarted)
            {
                LogGame(room, $"{client.Name} failed validation: game has not started.");
                SendInvalidMove(client, "Game has not started.");
                return false;
            }

            data = room.data;

            if (data == null)
            {
                LogGame(room, $"{client.Name} failed validation: game data missing.");
                SendInvalidMove(client, "Game data missing.");
                return false;
            }

            if (data.ParticipantOrder == null || data.ParticipantOrder.Count == 0)
            {
                LogGame(room, $"{client.Name} failed validation: turn order missing.");
                SendInvalidMove(client, "Turn order missing.");
                return false;
            }

            if (data.CurrentPlayerIndex < 0 || data.CurrentPlayerIndex >= data.ParticipantOrder.Count)
            {
                LogGame(room, $"{client.Name} failed validation: invalid current player index {data.CurrentPlayerIndex}.");
                SendInvalidMove(client, "Invalid turn index.");
                return false;
            }

            int currentPlayerId = data.ParticipantOrder[data.CurrentPlayerIndex];

            if (client.Id != currentPlayerId)
            {
                LogGame(room, $"{client.Name} failed validation: not their turn. CurrentPlayerId={currentPlayerId}, ClientId={client.Id}.");
                SendInvalidMove(client, "Not your turn.");
                _server.AddMaliciousStrike(client);
                return false;
            }

            return true;
        }

        #endregion

        #region Helpers

        /*
         * What this does:
         * Returns the Participant object for the current turn player.
         *
         * Throws:
         * InvalidOperationException if the current player id is no longer in room participants.
         */
        private Participant CurrentParticipant(RoomData room)
        {
            int currentPlayerId = room.data.ParticipantOrder[room.data.CurrentPlayerIndex];
            Participant participant = room.Participants.FirstOrDefault(p => p.id == currentPlayerId);

            if (participant == null)
                throw new InvalidOperationException($"Current player ID {currentPlayerId} is no longer in room participants.");

            return participant;
        }

        private ClientInfo CurrentClient(RoomData room)
        {
            int currentPlayerId = room.data.ParticipantOrder[room.data.CurrentPlayerIndex];

            return _server.FindPlayerById(currentPlayerId);
        }
        private bool TryGetClientRoom(ClientInfo client, out RoomData room)
        {
            room = null;

            if (client == null)
                return false;

            if (string.IsNullOrEmpty(client.CurrentRoom))
                return false;

            return _server.TryGetRoom(client.CurrentRoom, out room);
        }

        #endregion

        #region Logging

        private void LogGame(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [GAME] {message}");
        }

        private void LogGame(RoomData room, string message)
        {
            string roomName = room != null ? room.roomName : "NULL_ROOM";

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [GAME:{roomName}] {message}");
        }
        private void LogGameError(string context, Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [GAME ERROR] {context}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        private string DiceTypeName(int diceType)
        {
            switch (diceType)
            {
                case 0: return "Human";
                case 1: return "Cow";
                case 2: return "Chicken";
                case 3: return "Tank";
                case 4: return "UFO";
                default: return $"Unknown({diceType})";
            }
        }

        private string FormatRoll(IEnumerable<int> dice)
        {
            if (dice == null)
                return "null";

            return string.Join(", ", dice.Select(d => $"{DiceTypeName(d)}({d})"));
        }

        private string FormatSelectable(Dictionary<int, bool> selectableDice)
        {
            if (selectableDice == null || selectableDice.Count == 0)
                return "none";

            return string.Join(", ", selectableDice.Select(pair =>
                $"{DiceTypeName(pair.Key)}={pair.Value}"
            ));
        }

        private string FormatScores(RoomData room)
        {
            if (room == null || room.Participants == null)
                return "no scores";

            return string.Join(", ", room.Participants.Select(p =>
                $"{p.clientName}: {p.currPoints}/{room.pointGoal}"
            ));
        }

        private string FormatTurnStats(GameData data)
        {
            if (data == null)
                return "no game data";

            return $"TurnPoints={data.CurrentPoints}, DEF={data.CurrentDefense}, ATK={data.CurrentAttack}, DiceToRoll={data.DiceToRoll}, DoubleStake={data.DoubleStakeActive}, Phase={data.Phase}";
        }

        #endregion
    }
}