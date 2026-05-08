using OSCTools;
using UnityEngine;
using System.Net;
using System.Net.Sockets;

namespace Client
{
    public class Client : MonoBehaviour
    {
        public string username = "user";
        public string serverIP = "127.0.0.1";
        public int serverPort = 9000;

        OSCDispatcher dispatcher = new OSCDispatcher();
        UdpClient udp;
        IPEndPoint serverEndPoint;

        void Start()
        {
            udp = new UdpClient();
            serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            dispatcher.AddListener("/connect/reply", OnConnectReply, OSCUtil.BOOL, OSCUtil.STRING);

            SendConnect(username);
        }

        void Update()
        {
            // Receive messages
            while (udp.Available > 0)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udp.Receive(ref sender);

                dispatcher.HandlePacket(data, sender);
            }

            dispatcher.Update();
        }

        void Send(OSCMessageOut msg)
        {
            byte[] data = msg.GetBytes();
            udp.Send(data, data.Length, serverEndPoint);
        }

        void SendConnect(string username)
        {
            OSCMessageOut msg = new OSCMessageOut("/connect")
                .AddString(username);

            Send(msg);
        }

        void OnConnectReply(OSCMessageIn msg, IPEndPoint sender)
        {
            bool success = msg.ReadBool();
            string message = msg.ReadString();

            if (success)
            {
                Debug.Log("Connected! Moving to lobby...");
            }
            else
            {
                Debug.LogError("Failed: " + message);
            }
        }
    }
}