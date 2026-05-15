using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreeperDies_Net_Proj.Model
{
    public class GameData
    {
        public int id;
        public int diceToRoll;
        public int currentPoints;
        public int currentDefense;
        public int currentDanger;
        public List<int> participantOrder = new();
        public int currentPlayerIndex;
        public int[] currentRoll = System.Array.Empty<int>();
        public Dictionary<int,int> diceMap = new Dictionary<int,int>();// dice ID, count

        public GameData()
        {
            id = 0;
            diceToRoll = 13;
            currentPoints = 0;
            currentDefense = 0;
            currentDanger = 0;
        }
        /// <summary>
        /// 0 = human
        /// 1 = Cow
        /// 2 = chicken
        /// 3 = Tank
        /// 4 = UFO
        /// </summary>
        public void RollDice()
        {
            Random rnd = new Random();
            List<int> list = new List<int>();
            for (int i = 0; i < diceToRoll; i++)
            {
                int num = rnd.Next(0, 5);
                list.Add(num);
                if (diceMap.ContainsKey(num))
                {
                    diceMap[num]++;
                }
                else
                    diceMap[num] = 1;
            }
            currentRoll = list.ToArray();
        }
        /// <summary>
        /// 0 = human
        /// 1 = Cow
        /// 2 = chicken
        /// 3 = Tank
        /// 4 = UFO
        /// </summary>
        public int SelectedDice(int selected)
        {
            diceToRoll -= diceMap[selected];
            return diceMap[selected];
        }

        public bool HasPoints() => currentPoints > 0;
    }
}
