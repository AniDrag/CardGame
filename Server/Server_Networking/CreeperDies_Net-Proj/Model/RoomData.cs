using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreeperDies_Net_Proj.Model
{
    [Serializable]
    public class RoomData
    {
        public int ID;
        public string roomName;
        public string host;
        public int pointGoal;
        public List<Participant> Participants = new();
        public bool GameStarted;
        public GameData data;

        public RoomData(int pId, string pRoomName, string pHostName, int pPointGoal, object pCurrParticipants = null)
        {
            ID = pId;
            roomName = pRoomName;
            host = pHostName;
            pointGoal = pPointGoal;
            GameStarted = false;
            data = new GameData();
        }

        public bool AddParticipant(Participant pParticipant)
        {
            if (Participants.Contains(pParticipant)) return false;d
            if (Participants.Count >= 4) return false;
            Participants.Add(pParticipant);
            return true;
        }

        public int CurrParticipants => Participants.Count;
    }
}