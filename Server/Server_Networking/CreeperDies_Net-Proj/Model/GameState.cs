using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CreeperDies_Net_Proj.Model
{
    public class GameState
    {
        private readonly TcpServer _server;
        private readonly Random _rng = new Random();

        public GameState(TcpServer server)
        {
            _server = server;
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            var d = _server.Dispatcher;
            d.AddListener("/start_game", OnStartGame);
            d.AddListener("/select_die", OnSelectedDie, OSCUtil.INT);
            d.AddListener("/stake_roll", OnStakeRollAnswer, OSCUtil.BOOL);
        }

        private void OnStartGame(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
            if (!_server.TryGetRoom(client.CurrentRoom, out var room)) return;
            if (room.host != client.Name) { _server.SendError(client.Connection, "Only host can start"); return; }
            if (room.GameStarted) { _server.SendError(client.Connection, "Game already started"); return; }

            room.GameStarted = true;
            room.data = new GameData();
            room.data.participantOrder = room.Participants.Select(p => p.id).ToList();
            room.data.currentPlayerIndex = 0;
            room.data.currentPoints = 0;
            room.data.currentDefense = 0;
            room.data.currentDanger = 0;
            room.data.diceToRoll = 13;

            var stateMsg = new OSCMessageOut("/game_state")
                .AddInt(room.data.currentPlayerIndex).AddInt(room.Participants.Count);
            foreach (var p in room.Participants)
                stateMsg.AddString(p.clientName).AddInt(p.currPoints);
            _server.BroadcastToRoom(room, stateMsg);

            _server.BroadcastToRoom(room, new OSCMessageOut("/game_started"));
            Console.WriteLine($"[GAME] Started in '{room.roomName}' by {client.Name}");
            StartTurn(room);
        }

        private void StartTurn(RoomData room)
        {
            int playerId = room.data.participantOrder[room.data.currentPlayerIndex];
            var player = room.Participants.First(p => p.id == playerId);
            var client = _server.Clients[playerId];

            room.data.currentPoints = 0;
            room.data.currentDefense = 0;
            room.data.currentDanger = 0;
            room.data.diceToRoll = 13;

            var turnMsg = new OSCMessageOut("/your_turn").AddString(player.clientName);
            _server.BroadcastToRoom(room, turnMsg);
            _server.Send(client.Connection, new OSCMessageOut("/your_turn").AddString("It's your turn!"));
            RollDice(room);
        }

        private void RollDice(RoomData room)
        {
            int[] results = new int[room.data.diceToRoll];
            for (int i = 0; i < results.Length; i++) results[i] = _rng.Next(0, 5);
            room.data.currentRoll = results;

            var diceMsg = new OSCMessageOut("/dice_rolled").AddInt(results.Length);
            foreach (int val in results) diceMsg.AddInt(val);
            _server.BroadcastToRoom(room, diceMsg);
        }

        private void OnSelectedDie(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
            if (!_server.TryGetRoom(client.CurrentRoom, out var room)) return;
            if (!room.GameStarted) return;

            int playerId = room.data.participantOrder[room.data.currentPlayerIndex];
            if (client.Id != playerId) { _server.SendError(client.Connection, "Not your turn"); return; }

            int dieIndex = msg.ReadInt();
            if (dieIndex < 0 || dieIndex >= room.data.currentRoll.Length)
            { _server.SendError(client.Connection, "Invalid die"); return; }

            int dieValue = room.data.currentRoll[dieIndex];
            var newRoll = room.data.currentRoll.ToList();
            newRoll.RemoveAt(dieIndex);
            room.data.currentRoll = newRoll.ToArray();
            room.data.diceToRoll = newRoll.Count;

            switch (dieValue)
            {
                case 0: room.data.currentPoints += 10; break;
                case 1: room.data.currentPoints += 5; break;
                case 2: room.data.currentPoints += 1; break;
                case 3: room.data.currentDanger++; break;
                case 4: room.data.currentDefense++; break;
            }

            if (room.data.diceToRoll == 0) { EndTurn(room); return; }

            if (room.data.currentDefense >= room.data.currentDanger)
            {
                _server.Send(client.Connection, new OSCMessageOut("/stake_prompt").AddBool(true));
            }
            else
            {
                if (room.data.currentPoints > 0)
                    _server.Send(client.Connection, new OSCMessageOut("/stake_prompt").AddBool(false).AddString("Cannot stake. Collect or risk bust?"));
                else
                    EndTurn(room, busted: true);
            }
        }

        private void OnStakeRollAnswer(OSCMessageIn msg, IPEndPoint sender)
        {
            var client = _server.GetClientByEndpoint(sender);
            if (client == null || string.IsNullOrEmpty(client.CurrentRoom)) return;
            if (!_server.TryGetRoom(client.CurrentRoom, out var room)) return;
            if (!room.GameStarted) return;

            int playerId = room.data.participantOrder[room.data.currentPlayerIndex];
            if (client.Id != playerId) { _server.SendError(client.Connection, "Not your turn"); return; }

            bool doStake = msg.ReadBool();
            if (doStake) RollDice(room);
            else EndTurn(room);
        }

        private void EndTurn(RoomData room, bool busted = false)
        {
            int playerId = room.data.participantOrder[room.data.currentPlayerIndex];
            var player = room.Participants.First(p => p.id == playerId);

            if (!busted)
                player.currPoints += room.data.currentPoints;
            else
            {
                var bustMsg = new OSCMessageOut("/game_announcement").AddString($"{player.clientName} busted!");
                _server.BroadcastToRoom(room, bustMsg);
            }

            if (player.currPoints >= room.pointGoal)
            {
                var winMsg = new OSCMessageOut("/game_announcement").AddString($"{player.clientName} wins!");
                _server.BroadcastToRoom(room, winMsg);
                _server.RemoveRoom(room.roomName);
                return;
            }

            room.data.currentPlayerIndex = (room.data.currentPlayerIndex + 1) % room.Participants.Count;
            _server.BroadcastToRoom(room, new OSCMessageOut("/round_results").AddString("Scores updated"));

            var stateMsg = new OSCMessageOut("/game_state")
                .AddInt(room.data.currentPlayerIndex).AddInt(room.Participants.Count);
            foreach (var p in room.Participants)
                stateMsg.AddString(p.clientName).AddInt(p.currPoints);
            _server.BroadcastToRoom(room, stateMsg);

            StartTurn(room);
        }
    }
}
