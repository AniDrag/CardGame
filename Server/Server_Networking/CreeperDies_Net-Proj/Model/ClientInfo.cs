using NetworkConnections;

namespace CreeperDice_Net_Proj.Model

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
