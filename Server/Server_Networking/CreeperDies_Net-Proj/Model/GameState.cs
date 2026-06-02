using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace CreeperDice_Net_Proj.Model
{
    public class GameState
    {
        private readonly TcpServer _server;
        private readonly Dictionary<RoomData, bool> _awaitingStakeAnswer = new();
        public GameState(TcpServer server)
        {
            _server = server;
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            var d = _server.Dispatcher;
            d.AddListener(Msg.C_SELECT_DICE, OnSelectDice, OSCUtil.INT);
            d.AddListener(Msg.C_STAKE_ANSWER, OnStakeAnswer, OSCUtil.BOOL);
        }
        #region Game Flow Methods
        private void StartTurn(RoomData room)
        {
            try
            {
                var data = room.data;
                data.ResetTurn();
                _awaitingStakeAnswer[room] = false;

                int currentPlayerId = data.ParticipantOrder[data.CurrentPlayerIndex];
                var player = room.Participants.First(p => p.id == currentPlayerId);

                var turnMsg = new OSCMessageOut(Msg.S_YOUR_TURN).AddString($"{player.clientName}'s turn");
                _server.BroadcastToRoom(room, turnMsg);

                RollDice(room);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] StartTurn: {ex}");
                ForceEndGame(room, "Internal server error");
            }
        }
        private void RollDice(RoomData room)
        {
            try
            {
                var data = room.data;
                data.RollDice();

                var diceMsg = new OSCMessageOut(Msg.S_DICE_ROLLED);
                var roll = data.CurrentRoll;
                diceMsg.AddInt(roll.Count);
                foreach (int val in roll)
                    diceMsg.AddInt(val);

                _server.BroadcastToRoom(room, diceMsg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] RollDice: {ex}");
                EndTurn(room, busted: true);
            }
        }
        private void OnSelectDice(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (!ValidateTurn(client, out var room, out var data, out var player)) return;

            // Check if we are waiting for a stake answer – if so, ignore dice selection
            if (_awaitingStakeAnswer.TryGetValue(room, out bool awaiting) && awaiting)
            {
                _server.SendError(client.Connection, "Please answer the stake prompt first");
                return;
            }

            int diceType = msg.ReadInt();
            if (diceType < 0 || diceType > 4)
            {
                _server.AddMaliciousStrike(client);
                _server.SendError(client.Connection, "Invalid dice type, malicious action taken");
                return;
            }

            int result = data.SelectedDice(diceType);
            if (result == -1)
            {
                _server.AddMaliciousStrike(client);
                _server.SendError(client.Connection, "Dice type not available, malicious action taken");
                return;
            }
            else if (result == -2)
            {
                _server.SendError(client.Connection, "Point type already collected this turn");
                return;
            }
            else if (diceType == 3)
            {
                _server.AddMaliciousStrike(client);
                _server.SendError(client.Connection, "Tanks are automatically collected, malicious action taken");
                return;
            }

            BroadcastGameState(room);

            if (data.DiceToRoll == 0)
            {
                bool busted = data.CurrentDanger > data.CurrentDefense;
                EndTurn(room, busted);
                return;
            }

            if (data.CanCashOut())
            {
                var prompt = new OSCMessageOut(Msg.S_STAKE_PROMPT);
                prompt.AddBool(true);
                prompt.AddString("Cash out or stake?");
                _server.Send(client.Connection, prompt);
                _awaitingStakeAnswer[room] = true;  
            }
            else
            {
                if (data.CurrentPoints == 0 && data.CurrentDanger > data.CurrentDefense)
                {
                    EndTurn(room, busted: true);
                }
                else
                {
                    RollDice(room);
                }
            }
        }
        private void OnStakeAnswer(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (!ValidateTurn(client, out var room, out var data, out var player)) return;

            // Clear the awaiting flag regardless of answer
            _awaitingStakeAnswer[room] = false;

            bool cashOut = msg.ReadBool();
            if (cashOut)
            {
                EndTurn(room, busted: false);
            }
            else
            {
                if (data.DiceToRoll > 0)
                    RollDice(room);
                else
                    EndTurn(room, room.data.IsBusted());
            }
        }
        private void EndTurn(RoomData room, bool busted)
        {
            // Mybe calculate Bust here? or a helper func in RoomData/GameData isbust?
            try
            {
                var data = room.data;
                int playerId = data.ParticipantOrder[data.CurrentPlayerIndex];
                var player = room.Participants.First(p => p.id == playerId);

                if (!busted)
                {
                    player.currPoints += data.CurrentPoints;
                    var addMsg = new OSCMessageOut(Msg.S_GAME_ANNOUNCEMENT)
                        .AddString($"{player.clientName} earned {data.CurrentPoints} points.");
                    _server.BroadcastToRoom(room, addMsg);
                }
                else
                {
                    var bustMsg = new OSCMessageOut(Msg.S_GAME_ANNOUNCEMENT)
                        .AddString($"{player.clientName} busted! No points added.");
                    _server.BroadcastToRoom(room, bustMsg);
                }

                if (player.currPoints >= room.pointGoal)
                {
                    var winMsg = new OSCMessageOut(Msg.S_GAME_END)
                        .AddString($"{player.clientName} wins the game!");
                    _server.BroadcastToRoom(room, winMsg);
                    _server.RemoveRoom(room.roomName);
                    return;
                }

                data.CurrentPlayerIndex = (data.CurrentPlayerIndex + 1) % room.Participants.Count;
                BroadcastGameState(room);
                StartTurn(room);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] EndTurn: {ex}");
                ForceEndGame(room, "Game ended due to error");
            }
        }

        private void BroadcastGameState(RoomData room)
        {
            var data = room.data;
            var stateMsg = new OSCMessageOut(Msg.S_GAME_STATE)
                .AddInt(data.CurrentPlayerIndex)
                .AddInt(room.Participants.Count);
            foreach (var p in room.Participants)
                stateMsg.AddString(p.clientName).AddInt(p.currPoints);
            _server.BroadcastToRoom(room, stateMsg);
        }
        #endregion

        #region Validation & Helpers
        public void StartGameForRoom(RoomData room)
        {
            room.data = new GameData(13);
            room.data.ParticipantOrder = room.Participants.Select(p => p.id).ToList();
            room.data.CurrentPlayerIndex = 0;

            BroadcastGameState(room);
            StartTurn(room);
        }
        private bool ValidateTurn(ClientInfo client, out RoomData room, out GameData data, out Participant player)
        {
            room = null;
            data = null;
            player = null;

            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return false;
            if (!_server.TryGetRoom(client.CurrentRoom, out room)) return false;
            if (!room.GameStarted) return false;

            data = room.data;
            if (data == null) return false;

            int currentPlayerId = data.ParticipantOrder[data.CurrentPlayerIndex];
            if (client.Id != currentPlayerId)
            {
                _server.SendError(client.Connection, "Not your turn");
                return false;
            }

            player = room.Participants.FirstOrDefault(p => p.id == currentPlayerId);
            return player != null;
        }

        private void ForceEndGame(RoomData room, string reason)
        {
            var endMsg = new OSCMessageOut(Msg.S_GAME_END).AddString(reason);
            _server.BroadcastToRoom(room, endMsg);
            _server.RemoveRoom(room.roomName);
        }
        #endregion
        ///private void OnSelectedDie(OSCMessageIn msg, IPEndPoint sender)
        ///{
        ///    var client = _server.GetClientByEndpoint(sender);
        ///
        ///    //is in room, and in an active game
        ///    if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
        ///    if (!_server.TryGetRoom(client.CurrentRoom, out var room)) return;
        ///    if (!room.GameStarted) return;
        ///
        ///    // Is current player check
        ///    int playerId = room.data.ParticipantOrder[room.data.CurrentPlayerIndex];
        ///    if (client.Id != playerId) 
        ///    { 
        ///        _server.SendError(client.Connection, "Not your turn"); 
        ///        return; 
        ///    }
        ///
        ///    int diceIndex = msg.ReadInt();
        ///    if(diceIndex < 0 || diceIndex > 4)
        ///    {
        ///        _server.SendError(client.Connection, "Invalid dice ID"); 
        ///        return;
        ///    }
        ///    if (!room.data.diceMap.ContainsKey(diceIndex))
        ///    { 
        ///        _server.SendError(client.Connection, "Dice type not in roll cast"); 
        ///        return; 
        ///    }
        ///
        ///    int discardDice = room.data.SelectedDice(diceIndex);// already reduces the count of dice collected
        ///
        ///    room.data.CollectDanger(); // Collects tank dice and reduces the count
        ///
        ///    if (room.data.IsPointDice(diceIndex) && !room.data.usedPointDice.Contains(diceIndex))
        ///    {
        ///        room.data.currentPoints += discardDice;
        ///    }
        ///    else if (room.data.IsDefenseDice(diceIndex))
        ///    {
        ///        room.data.currentDefense += discardDice;
        ///    }
        ///
        ///    
        ///
        ///    if (room.data.diceToRoll == 0) 
        ///    {
        ///        EndTurn(room); 
        ///        return; 
        ///    }
        ///
        ///    if (room.data.currentDefense >= room.data.currentDanger)
        ///    {
        ///        _server.Send(client.Connection, new OSCMessageOut(STAKE_ROLL_PROMPT).AddBool(true));
        ///    }
        ///    else
        ///    {
        ///        if (room.data.currentPoints > 0)
        ///            _server.Send(client.Connection, new OSCMessageOut(STAKE_ROLL_PROMPT).AddBool(false).AddString("Cannot stake. Collect or risk bust?"));
        ///        else
        ///            EndTurn(room, busted: true);
        ///    }
        ///
        ///}

        /// private void OnStakeRollAnswer(OSCMessageIn msg, IPEndPoint sender)
        /// {
        ///     var client = _server.GetClientByEndpoint(sender);
        ///     if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
        ///     if (!_server.TryGetRoom(client.CurrentRoom, out var room)) return;
        ///     if (!room.GameStarted) return;
        ///
        ///     int playerId = room.data.participantOrder[room.data.currentPlayerIndex];
        ///     if (client.Id != playerId) { _server.SendError(client.Connection, "Not your turn"); return; }
        ///
        ///     bool doStake = msg.ReadBool();
        ///     if (doStake) RollDice(room);
        ///     else EndTurn(room);
        /// }

    }
}
