using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
namespace Server
{
    class GameServer
    {
        OSCDispatcher dispatcher = new OSCDispatcher();
        Dictionary<IPEndPoint, PlayerConnection> clients = new();

        UdpClient udp;
        bool running = true;

        public void Start(int port)
        {
            udp = new UdpClient(port);
            InitHandlers();

            Console.WriteLine("Server started...");

            new Thread(ReceiveLoop).Start();
        }

        void ReceiveLoop()
        {
            while (running)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udp.Receive(ref sender);

                dispatcher.HandlePacket(data, sender);
                dispatcher.Update();
            }
        }

        void Send(OSCMessageOut msg, IPEndPoint target)
        {
            byte[] data = msg.GetBytes();
            udp.Send(data, data.Length, target);
        }
        public void InitHandlers()
        {
            dispatcher.AddListener("/connect", OnClientConnect, OSCUtil.STRING);
        }

        void OnClientConnect(OSCMessageIn msg, IPEndPoint sender)
        {
            string username = msg.ReadString();

            if (clients.Values.Any(p => p.Username == username))
            {
                SendConnectReply(sender, false, "Username taken");
                return;
            }
            // validation
            if (string.IsNullOrWhiteSpace(username))
            {
                SendConnectReply(sender, false, "Invalid username");
                return;
            }

            if (clients.ContainsKey(sender))
            {
                SendConnectReply(sender, false, "Already connected");
                return;
            }

            // create player
            PlayerConnection player = new PlayerConnection
            {
                Username = username,
                EndPoint = sender,
                IsConnected = true
            };

            clients[sender] = player;

            Console.WriteLine($"Player connected: {username}");

            SendConnectReply(sender, true, "Connected!");
        }

        void SendConnectReply(IPEndPoint target, bool success, string message)
        {
            OSCMessageOut reply = new OSCMessageOut("/connect/reply")
                .AddBool(success)
                .AddString(message);

            Send(reply, target);
        }
    }

    class PlayerConnection
    {
        public string Username;
        public IPEndPoint EndPoint;
        public bool IsConnected;
    }
}