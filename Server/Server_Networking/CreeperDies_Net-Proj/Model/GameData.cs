using System;
using System.Collections.Generic;
using System.Linq;

namespace CreeperDice_Net_Proj.Model
{
    public class GameData
    {
        public int DiceToRoll { get; private set; }      // dice remaining this turn
        public int CurrentPoints { get; private set; }   // collected this turn (0,1,2)
        public int CurrentDefense { get; private set; }  // UFOs this turn
        public int CurrentDanger { get; private set; }   // Tanks this turn

        public List<int> ParticipantOrder { get; set; } = new();
        public int CurrentPlayerIndex { get; set; }

        // Last roll result (for reference only)
        public IReadOnlyList<int> CurrentRoll => _currentRoll.AsReadOnly();
        private List<int> _currentRoll = new();

        // Used to prevent re-selecting the same point type in one turn
        private HashSet<int> _usedPointTypes = new();

        // Dice map: key = dice type (0-4), value = count in current roll
        private Dictionary<int, int> _diceMap = new();


        public GameData(int initialDiceCount = 13)
        {
            DiceToRoll = initialDiceCount;
            CurrentPoints = 0;
            CurrentDefense = 0;
            CurrentDanger = 0;
            _usedPointTypes.Clear();
            _diceMap.Clear();
        }

        /// <summary>
        /// Rolls the remaining dice, replacing the current dice map.
        /// Dice types: 
        ///     0=Human, 
        ///     1=Cow, 
        ///     2=Chicken, 
        ///     3=Tank, 
        ///     4=UFO
        /// </summary>
        public void RollDice()
        {
            if (DiceToRoll <= 0)
                throw new InvalidOperationException("No dice left to roll.");

            _diceMap.Clear();
            _currentRoll.Clear();
            Random rng = new Random();

            for (int i = 0; i < DiceToRoll; i++)
            {
                int value = rng.Next(0, 5); // 0..4
                _currentRoll.Add(value);
                _diceMap[value] = _diceMap.GetValueOrDefault(value) + 1;
            }
            if (_diceMap.TryGetValue(3, out int tankCount))
            {
                CurrentDanger += tankCount;
                DiceToRoll -= tankCount;
                _diceMap.Remove(3);
            }
        }

        /// <summary>
        /// Selects a dice type: removes all dice of that type from the pool and returns the count.
        /// Returns -1 if the type doesn't exist in the current dice map.
        /// Return - 2 if the dice point dice was already used,
        /// Return - 3 if tank dice was selected.
        /// </summary>
        public int SelectedDice(int diceType)
        {
            if (!_diceMap.ContainsKey(diceType))
                return -1;

            int count = _diceMap[diceType];

            if (IsPointDice(diceType))
            {
                
                if (_usedPointTypes.Contains(diceType))
                    return -2;   // TODO: return a invalid choice. mybe make it unselectibel from the start

                CurrentPoints += count;
                _usedPointTypes.Add(diceType);
            }
            else if (IsDefenseDice(diceType))
            {
                CurrentDefense += count;
            }
            else if (IsDangerDice(diceType)) 
            {
                return -3; // Ur not supposed to Be able to select this one.
            }

            DiceToRoll -= count;
            _diceMap.Remove(diceType);
            return count;
        }

        /// <summary>
        /// Resets turn state (used at start of new turn).
        /// </summary>
        public void ResetTurn()
        {
            DiceToRoll = 13;
            CurrentPoints = 0;
            CurrentDefense = 0;
            CurrentDanger = 0;
            _usedPointTypes.Clear();
            _diceMap.Clear();
            _currentRoll.Clear();
        }

        // Type check helpers
        public bool IsPointDice(int type) => type >= 0 && type <= 2;
        public bool IsDefenseDice(int type) => type == 4;
        public bool IsDangerDice(int type) => type == 3;

        /// <summary>
        /// Returns true if player can voluntarily end turn and keep points.
        /// </summary>
        public bool CanCashOut() => CurrentPoints > 0 && CurrentDefense >= CurrentDanger;

        /// <summary>
        /// Returns true if the player is forced to bust (danger > defense + remaining dice).
        /// Actually we check: if danger > defense AND no dice left to roll => bust.
        /// More advanced: you could predict if impossible to ever cover danger, but simple version:
        /// </summary>
        public bool IsBusted()
        {
            // If no dice left and danger > defense, bust
            if (DiceToRoll == 0 && CurrentDanger > CurrentDefense + DiceToRoll)
                return true;
            // If player has no points and cannot roll (maybe no dice) also bust?
            // According to original: if can't stake and no points, end turn with bust.
            return false;
        }
    }
}