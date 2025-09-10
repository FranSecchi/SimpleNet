using SimpleNet.Utilities;
using System.Collections.Generic;
using LiteNetLib;

namespace SimpleNet.Transport.UDP
{
    internal class AHost : APeer
    {
        private int _connected;
        public AHost(int port) : base(port)
        {
        }

        public override void Start()
        {
            DebugQueue.AddMessage($"[SERVER] Listening on port {Port}.");
            Peer.Start(Port);
        }

        public override void Connect(string address)
        {
            DebugQueue.AddMessage("[SERVER] Cannot connect to a client as a server.", DebugQueue.MessageType.Warning);
        }

        public override void Kick(int id)
        {
            if (Peer.TryGetPeerById(id, out NetPeer peer))
            {
                peer.Disconnect();
                DebugQueue.AddMessage($"[SERVER] Client {id} kicked.", DebugQueue.MessageType.Warning);
            }
        }

        public override void Send(byte[] data)
        {
            List<NetPeer> peers = new List<NetPeer>(Peer.ConnectedPeerList);
            foreach (var peer in peers)
            {
                if(peer.ConnectionState == LiteNetLib.ConnectionState.Connected)
                {
                    if(!_connectionInfo.ContainsKey(peer.Id))
                        UpdateConnectionInfo(peer.Id, ConnectionState.Connected);
                    _connectionInfo[peer.Id].BytesSent += data.Length;
                    peer.Send(data, DeliveryMethod.Sequenced);
                }
            }
            DebugQueue.AddMessage("[SERVER] Sent message to all clients");
        }

        public override void OnConnectionRequest(ConnectionRequest request)
        {
            DebugQueue.AddMessage($"[SERVER] Requested connection from {request.RemoteEndPoint}.");
            if(_connected < MaxPlayers)
            {
                request.AcceptIfKey("Net_Key");
                _connected++;
            }
            else
                DebugQueue.AddMessage($"[SERVER] Requested connection denied from {request.RemoteEndPoint}. Max players: {MaxPlayers}", DebugQueue.MessageType.Warning);
        }


        public override void OnPeerConnected(NetPeer peer)
        {
            DebugQueue.AddMessage("[SERVER] Client connected: " + peer.Address + "|" + peer.Port + ":" + peer.Id);
            ITransport.TriggerOnClientConnected(peer.Id);
            _serverInfo.CurrentPlayers = _connected;
            UpdateConnectionInfo(peer.Id, ConnectionState.Connected, peer.Ping);
        }
        
        public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            DebugQueue.AddMessage($"Client disconnected. Reason: {disconnectInfo.Reason}", DebugQueue.MessageType.Warning);
            _connected--;
            _serverInfo.CurrentPlayers = _connected;
            ITransport.TriggerOnClientDisconnected(peer.Id);
            UpdateConnectionInfo(peer.Id, ConnectionState.Disconnected, peer.Ping);
        }
    }
}
