using System;
using System.Collections.Generic;
using System.Linq;

namespace CreeperDice_Net_Proj.Model
{
    public class GameData
    {
        public const int Human = 0;
        public const int Cow = 1;
        public const int Chicken = 2;
        public const int Tank = 3;
        public const int Ufo = 4;

        private readonly int _startingDiceCount;

        private readonly Dictionary<int, int> _diceWeights = new()
        {
            { Human, 50 },
            { Cow, 50 },
            { Chicken, 50 },
            { Tank, 45 },
            { Ufo, 50 }
        };

        public int DiceToRoll { get; private set; }

        // Points collected during this current turn only.
        public int CurrentPoints { get; private set; }

        // Defense collected during this current turn only.
        public int CurrentDefense { get; private set; }

        // Attackers/tanks collected during this current turn only.
        public int CurrentAttack { get; private set; }

        // Compatibility alias in case older code still uses "Danger".
        public int CurrentDanger => CurrentAttack;

        public bool DoubleStakeActive { get; private set; }
        public int ScoreMultiplier => DoubleStakeActive ? 2 : 1;

        public RoomGamePhase Phase { get; set; } = RoomGamePhase.NotStarted;

        public List<int> ParticipantOrder { get; set; } = new();
        public int CurrentPlayerIndex { get; set; }

        public IReadOnlyList<int> CurrentRoll => _currentRoll.AsReadOnly();
        public IReadOnlyDictionary<int, int> DiceMap => _diceMap;

        // Raw dice values from the latest roll.
        // Example: [0, 1, 3, 4, 4, 2]
        // Used by the client to visually show all dice that were rolled.
        private readonly List<int> _currentRoll = new();

        // Point dice types already selected during this player's current turn.
        // 0, 1, and 2 can only be selected once per player turn.
        private readonly HashSet<int> _usedPointTypes = new();

        // Current selectable dice counts after automatic tank handling.
        // Key = dice type/index.
        // Value = amount of dice of that type in the current roll.
        // Tanks/index 3 are removed automatically and should not remain selectable.
        private readonly Dictionary<int, int> _diceMap = new();

        public GameData(int initialDiceCount = 13)
        {
            _startingDiceCount = initialDiceCount;
            DiceToRoll = initialDiceCount;
        }
        //Resets all prams that are turn based to their starting values. This is called at the start of each player's turn
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

        private int RollWeightedDice()
        {
            int totalWeight = _diceWeights.Values.Sum();
            int roll = Random.Shared.Next(0, totalWeight);

            foreach (KeyValuePair<int, int> pair in _diceWeights)
            {
                if (roll < pair.Value)
                    return pair.Key;

                roll -= pair.Value;
            }

            return Human;
        }

        private void CollectTanksAutomatically()
        {
            if (!_diceMap.TryGetValue(Tank, out int tankCount))
                return;

            CurrentAttack += tankCount;
            DiceToRoll -= tankCount;

            // Tanks are visible in CurrentRoll, but not selectable.
            _diceMap.Remove(Tank);
        }



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
                CurrentPoints += count;
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
    }
}