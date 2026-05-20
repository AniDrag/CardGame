using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreeperDice_Net_Proj.Model
{
    public class Participant
    {
        public int id;
        public string clientName;
        public int currPoints;
        public Participant(int pID, string pName, int pCurrPoints = 0)
        {
            id = pID;
            clientName = pName;
            currPoints = pCurrPoints;
        }
    }
}
