using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using SimpleNet.Utilities;
using static SimpleNet.Transport.ITransport;

namespace SimpleNet.Transport.UDP
{
    internal abstract class APeer : INetEventListener
    {
        protected readonly NetManager Peer;
        protected ServerInfo _serverInfo;
        protected readonly int Port;
        private readonly ConcurrentQueue<byte[]> _packetQueue = new ConcurrentQueue<byte[]>();
        protected Dictionary<int, ConnectionInfo> _connectionInfo;
        protected int _bandwidthLimit;

        protected APeer(int port)
        {
            Peer = new NetManager(this);
            _serverInfo = new ServerInfo
            {
                CustomData = new Dictionary<string, string>()
            };
            _connectionInfo = new Dictionary<int, ConnectionInfo>();
            Port = port;
        }

        public ServerInfo ServerInfo { get; set; }
        public bool UseDebug { get; set; }
        public Dictionary<int, ConnectionInfo> ConnectionInfo  => _connectionInfo;
        public int MaxPlayers { get; set; }

        public abstract void Start();
        public abstract void Connect(string address);
        public abstract void Kick(int id);
        public abstract void OnPeerConnected(NetPeer peer);
        public abstract void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo);
        public abstract void Send(byte[] data);
        public abstract void OnConnectionRequest(ConnectionRequest request);

        public virtual void SetBandwidthLimit(int bytesPerSecond)
        {
            _bandwidthLimit = bytesPerSecond;
            Peer.UpdateTime = 15; // Update more frequently for better bandwidth control
            Peer.MaxConnectAttempts = 10;
            Peer.ReconnectDelay = 500;
            Peer.DisconnectTimeout = 5000;
        }

        public void Disconnect()
        {
            DebugQueue.AddMessage($"All Peers disconnected");

            Peer.DisconnectAll();
        }

        public void SendTo(int id, byte[] data)
        {
            if (!Peer.TryGetPeerById(id, out NetPeer peer)) return;
            peer.Send(data, DeliveryMethod.Sequenced);
            if(!_connectionInfo.ContainsKey(id))
                UpdateConnectionInfo(id, ConnectionState.Connected);
            _connectionInfo[id].BytesSent += data.Length;
            DebugQueue.AddMessage($"[SERVER] Sent message to client {id}");
        }

        public byte[] Receive()
        {
            if (_packetQueue.TryDequeue(out byte[] packet))
            {
                return packet;
            }
            return null;
        }


        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            DebugQueue.AddMessage("Data received from peer " + peer.Address + "|" + peer.Port + ":" + peer.Id);
            byte[] data = reader.GetRemainingBytes();
            _packetQueue.Enqueue(data);
            _connectionInfo[peer.Id].BytesReceived += data.Length;
            TriggerOnDataReceived(peer.Id);
            reader.Recycle();
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }
        
        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            UpdateConnectionInfo(peer.Id, ConnectionState.Connected, latency);
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            if(UseDebug)             
                DebugQueue.AddMessage($"Network error: {socketError} from {endPoint}", DebugQueue.MessageType.Error);
            // Find the client ID associated with this endpoint
            foreach (NetPeer peer in Peer.ConnectedPeerList)
            {
                if (peer.Address.Equals(endPoint.Address))
                {
                    UpdateConnectionInfo(peer.Id, ConnectionState.Disconnected);
                    break;
                }
            }
        }

        protected void UpdateConnectionInfo(int clientId, ConnectionState state, int ping = 0, float packetLoss = 0)
        {
            if (!_connectionInfo.ContainsKey(clientId))
            {
                _connectionInfo[clientId] = new ConnectionInfo
                {
                    Id = clientId,
                    State = state,
                    ConnectedSince = DateTime.Now,
                    BytesReceived = 0,
                    BytesSent = 0,
                    Ping = ping,
                    PacketLoss = packetLoss
                };
            }
            else
            {
                var info = _connectionInfo[clientId];
                if(info.State != state) TriggerOnConnectionStateChanged(_connectionInfo[clientId]);
                info.State = state;
                info.Ping = ping;
                info.PacketLoss = packetLoss;
                _connectionInfo[clientId] = info;
            }
        }
        public void Poll()
        {
            Peer.PollEvents();
        }

        public void Stop()
        {
            _connectionInfo.Clear();
            Peer.DisconnectAll();
            Peer.Stop();
        }
    }
}
