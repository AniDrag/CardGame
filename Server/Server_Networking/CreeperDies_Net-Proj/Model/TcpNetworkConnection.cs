using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace NetworkConnections
{
    #region Connection Status

    public enum ConnectionStatus
    {
        Connecting,
        Connected,
        Disconnecting,
        Disconnected
    }

    #endregion

    /// <summary>
    /// A user friendly wrapper around a TCP client.
    /// Handles message boundaries, avoids blocking, and catches most exceptions.
    /// </summary>
    public class TcpNetworkConnection
    {
        #region Properties

        public int LocalPort
        {
            get
            {
                if (localPort < 0 && socket.Client.LocalEndPoint != null)
                    localPort = ((IPEndPoint)socket.Client.LocalEndPoint).Port;

                return localPort;
            }
        }

        public IPEndPoint Remote
        {
            get
            {
                if (remote == null && socket.Connected && socket.Client.RemoteEndPoint != null)
                    remote = (IPEndPoint)socket.Client.RemoteEndPoint;

                return remote;
            }
        }

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Connecting;

        #endregion

        #region Fields

        private readonly TcpClient socket;

        // Incoming complete packets waiting to be processed.
        private readonly Queue<byte[]> incoming = new Queue<byte[]>();

        // Packet reading state.
        private bool _isReadingPacket = false;
        private int _nextPacketLength;

        // Cached socket properties.
        // These are cached because accessing socket endpoint properties can throw if the socket is closed.
        private int localPort = -1;
        private IPEndPoint remote = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Use this constructor to open a connection to a remote listener/server.
        /// </summary>
        /// <param name="remoteIPstring">Remote server IP address.</param>
        /// <param name="remotePort">Remote server port.</param>
        /// <param name="asynchronous">If true, connection starts asynchronously and begins in Connecting state.</param>
        /// <param name="fast">If true, disables Nagle's algorithm for faster small packets.</param>
        public TcpNetworkConnection(string remoteIPstring, int remotePort, bool asynchronous = false, bool fast = true)
        {
            Status = ConnectionStatus.Connecting;

            socket = new TcpClient();
            socket.NoDelay = fast;

            if (asynchronous)
            {
                socket.BeginConnect(remoteIPstring, remotePort, new AsyncCallback(ConnectionCallback), this);
            }
            else
            {
                try
                {
                    socket.Connect(remoteIPstring, remotePort);
                    ProcessConnectionResult();
                }
                catch (Exception error)
                {
                    ConnectionLog.WriteLine("Exception during connection attempt: " + error.Message);
                    Status = ConnectionStatus.Disconnected;
                }
            }
        }

        /// <summary>
        /// Use this constructor when accepting a TcpClient from a listener.
        /// </summary>
        /// <param name="client">TcpClient accepted from listener.</param>
        public TcpNetworkConnection(TcpClient client)
        {
            socket = client;

            if (client.Connected)
            {
                Status = ConnectionStatus.Connected;

                if (client.Client.LocalEndPoint != null)
                {
                    localPort = ((IPEndPoint)socket.Client.LocalEndPoint).Port;
                    remote = (IPEndPoint)socket.Client.RemoteEndPoint;
                }
            }
            else
            {
                Status = ConnectionStatus.Disconnected;
            }
        }

        #endregion

        #region Connection Lifecycle

        private void ConnectionCallback(IAsyncResult result)
        {
            ConnectionLog.WriteLine(2, "Connection callback. Completed: " + result.IsCompleted);

            try
            {
                // EndConnect is required after BeginConnect.
                socket.EndConnect(result);
                ProcessConnectionResult();
            }
            catch (Exception error)
            {
                ConnectionLog.WriteLine("Exception during connection callback: " + error.Message);
                Status = ConnectionStatus.Disconnected;
            }
        }

        private void ProcessConnectionResult()
        {
            if (socket.Connected)
            {
                ConnectionLog.WriteLine("Connection successful");
                Status = ConnectionStatus.Connected;

                if (socket.Client.LocalEndPoint != null)
                {
                    localPort = ((IPEndPoint)socket.Client.LocalEndPoint).Port;
                    remote = (IPEndPoint)socket.Client.RemoteEndPoint;
                }
            }
            else
            {
                ConnectionLog.WriteLine("Failed to connect");
                Status = ConnectionStatus.Disconnected;
            }
        }

        /// <summary>
        /// Call this when done, to clean up resources.
        /// </summary>
        public void Close()
        {
            Status = ConnectionStatus.Disconnected;
            socket.Close();
        }

        #endregion

        #region Packet Updating

        private void Update()
        {
            if (!socket.Connected)
            {
                Status = ConnectionStatus.Disconnected;
                ConnectionLog.WriteLine("NetworkConnection.Update: socket closed by remote");
                return;
            }

            try
            {
                while (Status == ConnectionStatus.Connected && socket.Available > 0)
                {
                    NetworkStream stream = socket.GetStream();

                    if (_isReadingPacket)
                    {
                        ReadPacketBody(stream);
                    }
                    else
                    {
                        ReadPacketHeader(stream);
                    }
                }
            }
            catch (Exception error)
            {
                Status = ConnectionStatus.Disconnected;
                ConnectionLog.WriteLine("NetworkConnection.Update: Exception: " + error.Message);
            }
        }

        private void ReadPacketHeader(NetworkStream stream)
        {
            if (socket.Available < 4)
                return;

            byte[] data = new byte[4];
            stream.Read(data, 0, 4);

            _nextPacketLength = BitConverter.ToInt32(data, 0);
            _isReadingPacket = true;

            ConnectionLog.WriteLine(2, "Incoming packet of length {0}", _nextPacketLength);
        }

        private void ReadPacketBody(NetworkStream stream)
        {
            if (socket.Available < _nextPacketLength)
                return;

            byte[] data = new byte[_nextPacketLength];
            stream.Read(data, 0, _nextPacketLength);

            _isReadingPacket = false;
            incoming.Enqueue(data);
        }

        #endregion

        #region Sending

        /// <summary>
        /// Send a packet to the remote endpoint.
        /// Only works when the status is Connected.
        /// </summary>
        public void Send(byte[] packet)
        {
            if (Status != ConnectionStatus.Connected)
            {
                ConnectionLog.WriteLine("NetworkConnection.Send: skip, since status = " + Status);
                return;
            }

            try
            {
                NetworkStream stream = socket.GetStream();
                stream.WriteTimeout = 1;

                if (stream.CanWrite)
                {
                    stream.Write(BitConverter.GetBytes(packet.Length), 0, 4);
                    stream.Write(packet, 0, packet.Length);
                }
                else
                {
                    ConnectionLog.WriteLine("Error: cannot send, because cannot write to network stream");
                }
            }
            catch (Exception error)
            {
                ConnectionLog.WriteLine("NetworkConnection.Send: " + error.Message);
                Close();
            }
        }

        #endregion

        #region Receiving

        /// <summary>
        /// Returns the number of available complete packets.
        /// If non-zero, call GetPacket to retrieve the next incoming packet.
        /// </summary>
        public int Available()
        {
            if (Status != ConnectionStatus.Connected)
                return 0;

            Update();

            return incoming.Count;
        }

        /// <summary>
        /// If a packet is available, this returns the first available packet.
        /// Otherwise, returns null.
        /// Use Available first to check whether a packet is available.
        /// </summary>
        public byte[] GetPacket()
        {
            if (Status != ConnectionStatus.Connected)
                return null;

            if (Available() > 0)
                return incoming.Dequeue();

            return null;
        }

        #endregion
    }
}