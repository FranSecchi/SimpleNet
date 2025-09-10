using LiteNetLib;
using SimpleNet.Utilities;

namespace SimpleNet.Transport.UDP
{
    internal class AClient : APeer
    {
        public AClient(int port) : base(port)
        {
        }

        public override void Connect(string address)
        {
            DebugQueue.AddMessage($"Connecting to: {address}:{Port}");
            Peer.Connect(address, Port, "Net_Key");
        }

        public override void Kick(int id)
        {
            DebugQueue.AddMessage("[CLIENT] Kicked from host", DebugQueue.MessageType.Warning);
            Peer.DisconnectPeer(Peer.FirstPeer);
        }

        public override void Start()
        {
            Peer.Start();
        }
        
        
        
        public override void Send(byte[] data)
        {
            if(!_connectionInfo.ContainsKey(0))
                UpdateConnectionInfo(0, ConnectionState.Connected);
            _connectionInfo[0].BytesSent += data.Length;
            Peer.FirstPeer.Send(data, DeliveryMethod.Sequenced);
            DebugQueue.AddMessage("[CLIENT] Sent message to host");
        }

        public override void OnConnectionRequest(ConnectionRequest request)
        {
            DebugQueue.AddMessage("[CLIENT] Connection request received. Clients should not receive requests", DebugQueue.MessageType.Warning);
        }


        public override void OnPeerConnected(NetPeer peer)
        {
            DebugQueue.AddMessage($"[CLIENT] Connected to server: "+ peer.Address + ":" + peer.Port);
            ITransport.TriggerOnClientConnected(peer.Id);
            UpdateConnectionInfo(peer.Id, ConnectionState.Connected, peer.Ping);
        }
        
        public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            DebugQueue.AddMessage($"Disconnected from server. Reason: {disconnectInfo.Reason}", DebugQueue.MessageType.Warning);
            UpdateConnectionInfo(peer.Id, ConnectionState.Disconnected, peer.Ping);
        }
    }
}
