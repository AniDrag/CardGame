using System;
using System.Collections.Generic;
using System.Linq;

namespace CreeperDice_Net_Proj.Model
{
    /*
     * GameData
     *
     * Purpose:
     * This class stores and controls the active dice game data for one room/game session.
     *
     * It handles:
     * - How many dice are rolled.
     * - What dice types exist.
     * - Weighted dice rolling.
     * - Current turn points.
     * - Current turn defense.
     * - Current turn attack/tanks.
     * - Which dice can be selected.
     * - Applying selected dice results.
     * - Bust and banking checks.
     *
     * Important:
     * This class does not send OSC messages.
     * This class does not receive OSC messages.
     * It is only the server-side game logic data.
     *
     * The server/controller should call these methods, then send the result to clients.
     */
    public class GameData
    {
        #region Dice Type Constants

        /*
         * Dice type IDs.
         *
         * These integers are used by both server and client.
         * The client uses these values to know which dice visual/type to show.
         *
         * 0 = Human
         * 1 = Cow
         * 2 = Chicken
         * 3 = Tank
         * 4 = UFO
         */

        public const int Human = 0;
        public const int Cow = 1;
        public const int Chicken = 2;
        public const int Tank = 3;
        public const int Ufo = 4;

        #endregion

        #region Dice Setup
        private readonly int _startingDiceCount;
        private readonly Dictionary<int, int> _diceWeights = new()
        {
            { Human, 50 },
            { Cow, 50 },
            { Chicken, 50 },
            { Tank, 45 },
            { Ufo, 50 }
        };

        #endregion

        #region Public Turn State
        public int DiceToRoll { get; private set; }
        public int CurrentPoints { get; private set; }
        public int CurrentDefense { get; private set; }
        public int CurrentAttack { get; private set; }

        public int CurrentDanger => CurrentAttack;

        /*
         * DoubleStakeActive:
         * True when double stake is active for the current turn.
         *
         * ScoreMultiplier:
         * Returns 2 when double stake is active.
         * Returns 1 when it is not active.
         */
        public bool DoubleStakeActive { get; private set; }
        public int ScoreMultiplier => DoubleStakeActive ? 2 : 1;

        #endregion

        #region Game Flow State

        /*
         * Phase:
         * Current phase of the room game.
         *
         * Example phases could be:
         * - NotStarted
         * - Rolling
         * - WaitingForDiceSelection
         *
         * The exact values come from RoomGamePhase.
         */
        public RoomGamePhase Phase { get; set; } = RoomGamePhase.NotStarted;

        public List<int> ParticipantOrder { get; set; } = new();
        public int CurrentPlayerIndex { get; set; }

        #endregion

        #region Public Roll Read-Only Access

        public IReadOnlyList<int> CurrentRoll => _currentRoll.AsReadOnly();

        public IReadOnlyDictionary<int, int> DiceMap => _diceMap;

        #endregion

        #region Private Roll State

        /*
         * _currentRoll:
         * Raw dice values from the latest roll.
         *
         * Tanks stay visible here because the client still needs to show them.
         */
        private readonly List<int> _currentRoll = new();

        private readonly HashSet<int> _usedPointTypes = new();

        private readonly Dictionary<int, int> _diceMap = new();

        #endregion

        #region Constructor

        public GameData(int initialDiceCount = 13)
        {
            _startingDiceCount = initialDiceCount;
            DiceToRoll = initialDiceCount;
        }

        #endregion

        #region Turn Control
        public void ResetTurn()
        {
            DiceToRoll = _startingDiceCount;
            CurrentPoints = 0;
            CurrentDefense = 0;
            CurrentAttack = 0;
            DoubleStakeActive = false;

            _currentRoll.Clear();
            _usedPointTypes.Clear();
            _diceMap.Clear();

            Phase = RoomGamePhase.Rolling;
        }
        public void EnableDoubleStake()
        {
            DoubleStakeActive = true;
        }

        #endregion

        #region Rolling

        /*
         * What this does:
         * Rolls all dice currently available in DiceToRoll.
         *
         * Flow:
         * 1. Check that there are dice left to roll.
         * 2. Clear the previous roll.
         * 3. Roll weighted dice DiceToRoll times.
         * 4. Store every raw dice value in _currentRoll.
         * 5. Count each dice type in _diceMap.
         * 6. Automatically collect Tanks.
         * 7. Move the phase to WaitingForDiceSelection.
         *
         * Throws:
         * InvalidOperationException if DiceToRoll is 0 or lower.
         */
        public void RollDice()
        {
            if (DiceToRoll <= 0)
                throw new InvalidOperationException("No dice left to roll.");

            _currentRoll.Clear();
            _diceMap.Clear();

            int diceAmountThisRoll = DiceToRoll;

            for (int i = 0; i < diceAmountThisRoll; i++)
            {
                int value = RollWeightedDice();

                _currentRoll.Add(value);

                if (!_diceMap.ContainsKey(value))
                    _diceMap[value] = 0;

                _diceMap[value]++;
            }

            CollectTanksAutomatically();

            Phase = RoomGamePhase.WaitingForDiceSelection;
        }

        private int GetWeightForDiceType(int diceType, int baseWeight)
        {
            if (DoubleStakeActive && diceType == Tank)
                return baseWeight * 2;

            return baseWeight;
        }

        private int RollWeightedDice()
        {
            int totalWeight = _diceWeights.Sum(pair => GetWeightForDiceType(pair.Key, pair.Value));
            int roll = Random.Shared.Next(0, totalWeight);

            foreach (KeyValuePair<int, int> pair in _diceWeights)
            {
                int weight = GetWeightForDiceType(pair.Key, pair.Value);

                if (roll < weight)
                    return pair.Key;

                roll -= weight;
            }

            return Human;
        }

        /*
         * What this does:
         * Automatically collects all Tank dice from the current roll.
         *
         * Tank behavior:
         * - Tanks increase CurrentAttack.
         * - Tanks reduce DiceToRoll.
         * - Tanks are removed from _diceMap so they cannot be selected by the player.
         *
         * Important:
         * Tanks still remain inside _currentRoll.
         * This allows the client to show the Tank dice visually after the roll.
         */
        private void CollectTanksAutomatically()
        {
            if (!_diceMap.TryGetValue(Tank, out int tankCount))
                return;

            CurrentAttack += tankCount;
            DiceToRoll -= tankCount;

            _diceMap.Remove(Tank);
        }

        #endregion

        #region Dice Selection
        public Dictionary<int, bool> GetSelectableDice()
        {
            return new Dictionary<int, bool>
            {
                { Human, CanSelectDice(Human) },
                { Cow, CanSelectDice(Cow) },
                { Chicken, CanSelectDice(Chicken) },
                { Tank, false },
                { Ufo, CanSelectDice(Ufo) }
            };
        }

        /*
         * What this does:
         * Checks if a dice type can be selected right now.
         *
         * Data received:
         * diceType = dice type id.
         *
         * Returns:
         * true if the dice can be selected.
         * false if it cannot.
         *
         * Selection rules:
         * - The dice type must exist in _diceMap.
         * - Tank cannot be selected.
         * - Point dice can only be selected once per turn.
         * - The dice must be either point dice or defense dice.
         */
        public bool CanSelectDice(int diceType)
        {
            if (!_diceMap.ContainsKey(diceType))
                return false;

            if (diceType == Tank)
                return false;

            if (IsPointDice(diceType) && _usedPointTypes.Contains(diceType))
                return false;

            return IsPointDice(diceType) || IsDefenseDice(diceType);
        }

        /*
         * What this does:
         * Tries to select one dice type from the current roll.
         *
         * Data received:
         * diceType = dice type id the player wants to select.
         *
         * Output:
         * error = empty string if selection succeeds.
         * error = reason why selection failed.
         *
         * Returns:
         * true if the dice was selected.
         * false if the dice could not be selected.
         *
         * What happens on success:
         * - Point dice add to CurrentPoints.
         * - Defense dice add to CurrentDefense.
         * - DiceToRoll is reduced by the amount selected.
         * - The selected dice type is removed from _diceMap.
         *
         * Important:
         * This should be called by the server after a client requests a dice selection.
         * The result should then be sent back to the client.
         */
        public bool TrySelectDice(int diceType, out string error)
        {
            error = string.Empty;

            if (diceType < Human || diceType > Ufo)
            {
                error = "Invalid dice type.";
                return false;
            }

            if (diceType == Tank)
            {
                error = "Tanks are attackers and cannot be selected.";
                return false;
            }

            if (!_diceMap.TryGetValue(diceType, out int count))
            {
                error = "That dice type is not available in the current roll.";
                return false;
            }

            if (IsPointDice(diceType) && _usedPointTypes.Contains(diceType))
            {
                error = "You already selected that point type this turn.";
                return false;
            }

            if (IsPointDice(diceType))
            {
                int pointsGained = count;

                if (DoubleStakeActive)
                    pointsGained *= 2;

                CurrentPoints += pointsGained;
                _usedPointTypes.Add(diceType);
            }
            else if (IsDefenseDice(diceType))
            {
                CurrentDefense += count;
            }

            DiceToRoll -= count;
            _diceMap.Remove(diceType);

            return true;
        }

        #endregion

        #region Bust And Banking Checks
        public bool IsUnrecoverableDefenseBust()
        {
            return CurrentAttack > CurrentDefense + DiceToRoll;
        }

        public bool HasSurvivedAttack()
        {
            return CurrentDefense >= CurrentAttack;
        }
        public bool CanBankPoints()
        {
            return CurrentPoints > 0 && HasSurvivedAttack();
        }

        public bool HasSelectableDice()
        {
            return GetSelectableDice().Any(pair => pair.Value);
        }

        #endregion

        #region Dice Type Helperserwise.
         
        public bool IsPointDice(int diceType)
        {
            return diceType == Human ||
                   diceType == Cow ||
                   diceType == Chicken;
        }
        public bool IsDefenseDice(int diceType)
        {
            return diceType == Ufo;
        }

        #endregion
    }
}