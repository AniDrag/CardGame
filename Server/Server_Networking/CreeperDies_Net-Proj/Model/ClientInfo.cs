using NetworkConnections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreeperDies_Net_Proj.Model
{
    public class ClientInfo
    {
        public int Id;
        public string Name = null!;
        public TcpNetworkConnection Connection = null!;
        public string? CurrentRoom;
    }

    public class ClientRateInfo
    {
        public DateTime LastRequestTime;
        public int RequestCountInCurrentSecond;
        public int BanCount;
    }
}
