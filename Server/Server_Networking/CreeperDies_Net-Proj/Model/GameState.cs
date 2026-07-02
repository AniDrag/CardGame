using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace CreeperDice_Net_Proj.Model
{
    public class GameState
    {
        #region Fields

        private readonly TcpServer _server;

        private readonly Dictionary<string, HashSet<int>> _readyPlayersByRoom = new();
        private readonly HashSet<string> _startedRooms = new();

        #endregion

        #region Constructor

        public GameState(TcpServer server)
        {
            _server = server;
            RegisterHandlers();
        }

        #endregion

        #region Message Registration

        private void RegisterHandlers()
        {
            OSCDispatcher dispatcher = _server.Dispatcher;

            dispatcher.AddListener(Msg.C_GAME_SCENE_READY, OnGameSceneReady);
            dispatcher.AddListener(Msg.C_SELECT_DICE, OnSelectDice, OSCUtil.INT);
            dispatcher.AddListener(Msg.C_STAKE_ANSWER, OnStakeAnswer, OSCUtil.BOOL);

            LogGame("Registered game message handlers.");
        }

        #endregion

        #region Game Flow

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

            room.data = new GameData(13);
            room.data.ParticipantOrder = room.Participants.Select(p => p.id).ToList();
            room.data.CurrentPlayerIndex = 0;
            room.data.Phase = RoomGamePhase.NotStarted;

            LogGame(room, "Turn order: " + string.Join(", ", room.data.ParticipantOrder));
            LogGame(room, "Initial scores: " + FormatScores(room));

            SendGameState(room);
            StartTurn(room);
        }

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

                data.CurrentPlayerIndex = (data.CurrentPlayerIndex + 1) % room.Participants.Count;

                LogGame(room, $"Next player index: {data.CurrentPlayerIndex}.");

                StartTurn(room);
            }
            catch (Exception ex)
            {
                LogGameError("EndTurn failed", ex);
                EndGame(room, "Game ended because the server failed to end a turn.");
            }
        }

        private void EndGame(RoomData room, string message)
        {
            LogGame(room, "Game ended. " + message);

            if (room.data != null)
                room.data.Phase = RoomGamePhase.Finished;

            SendGameEnd(room, message);

            _readyPlayersByRoom.Remove(room.roomName);
            _startedRooms.Remove(room.roomName);

            _server.RemoveRoom(room.roomName);
        }

        #endregion

        #region Received Messages

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
                return;
            }

            int diceType = msg.ReadInt();

            LogGame(room, $"{client.Name} selected {DiceTypeName(diceType)}({diceType}).");

            if (!data.TrySelectDice(diceType, out string error))
            {
                LogGame(room, $"{client.Name}'s dice selection was rejected. Reason: {error}");

                SendInvalidMove(client, error);

                // Re-send valid options so the current player can re-enable the right buttons.
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

            if (data.Phase != RoomGamePhase.WaitingForStakeAnswer)
            {
                LogGame(room, $"{client.Name} tried to answer stake during invalid phase: {data.Phase}.");
                SendInvalidMove(client, "You are not being asked to stake right now.");
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

        #endregion

        #region Sending Messages

        private void SendYourTurn(RoomData room, Participant player)
        {
            LogGame(room, $"SEND {Msg.S_YOUR_TURN} -> {player.clientName}'s turn.");

            var msg = new OSCMessageOut(Msg.S_YOUR_TURN)
                .AddString($"{player.clientName}'s turn");

            _server.BroadcastToRoom(room, msg);
        }

        private void SendTurnStarted(RoomData room, Participant player)
        {
            LogGame(room, $"SEND {Msg.S_TURN_STARTED} -> currentPlayerId={player.id}, currentPlayerName={player.clientName}");

            var msg = new OSCMessageOut(Msg.S_TURN_STARTED)
                .AddInt(player.id)
                .AddString(player.clientName);

            _server.BroadcastToRoom(room, msg);
        }

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

            // Who owns this roll/turn.
            msg.AddInt(currentClient.Id);

            // Raw dice visuals for everyone.
            IReadOnlyList<int> roll = data.CurrentRoll;

            msg.AddInt(roll.Count);

            foreach (int dice in roll)
                msg.AddInt(dice);

            // Public authoritative turn values.
            msg.AddInt(data.CurrentPoints);
            msg.AddInt(data.CurrentDefense);
            msg.AddInt(data.CurrentAttack);
            msg.AddBool(data.DoubleStakeActive);

            LogGame(room, $"SEND {Msg.S_DICE_ROLLED} -> owner={currentClient.Id}, count={roll.Count}, roll=[{FormatRoll(roll)}], {FormatTurnStats(data)}");

            _server.BroadcastToRoom(room, msg);
        }

        private void SendDiceSelected(RoomData room, int diceType)
        {
            LogGame(room, $"SEND {Msg.S_DICE_SELECTED} -> diceType={DiceTypeName(diceType)}({diceType})");

            var msg = new OSCMessageOut(Msg.S_DICE_SELECTED)
                .AddInt(diceType);

            _server.BroadcastToRoom(room, msg);
        }

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

        private void SendStakePrompt(ClientInfo client)
        {
            LogGame($"SEND {Msg.S_STAKE_PROMPT} -> to {client.Name}");

            var msg = new OSCMessageOut(Msg.S_STAKE_PROMPT);

            _server.Send(client.Connection, msg);
        }

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

        private void SendGameAnnouncement(RoomData room, string message)
        {
            LogGame(room, $"SEND {Msg.S_GAME_ANNOUNCEMENT} -> {message}");

            var msg = new OSCMessageOut(Msg.S_GAME_ANNOUNCEMENT)
                .AddString(message);

            _server.BroadcastToRoom(room, msg);
        }

        private void SendInvalidMove(ClientInfo client, string reason)
        {
            LogGame($"SEND {Msg.S_INVALID_MOVE} -> to {client?.Name ?? "NULL_CLIENT"}: {reason}");

            if (client == null)
                return;

            var msg = new OSCMessageOut(Msg.S_INVALID_MOVE)
                .AddString(reason);

            _server.Send(client.Connection, msg);
        }

        private void SendGameEnd(RoomData room, string message)
        {
            LogGame(room, $"SEND {Msg.S_GAME_END} -> {message}");

            var msg = new OSCMessageOut(Msg.S_GAME_END)
                .AddString(message);

            _server.BroadcastToRoom(room, msg);
        }

        #endregion

        #region Validation

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
                return false;
            }

            return true;
        }

        #endregion

        #region Helpers

        private Participant CurrentParticipant(RoomData room)
        {
            int currentPlayerId = room.data.ParticipantOrder[room.data.CurrentPlayerIndex];

            return room.Participants.First(p => p.id == currentPlayerId);
        }

        private ClientInfo CurrentClient(RoomData room)
        {
            int currentPlayerId = room.data.ParticipantOrder[room.data.CurrentPlayerIndex];

            return _server.FindPlayerById(currentPlayerId);
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