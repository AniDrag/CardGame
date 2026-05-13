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

        public GameData()
        {
            id = 0;
            diceToRoll = 13;
            currentPoints = 0;
            currentDefense = 0;
            currentDanger = 0;
        }
    }
}
