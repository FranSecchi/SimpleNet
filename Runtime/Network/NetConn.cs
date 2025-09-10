using System.Collections.Generic;
using SimpleNet.Messages;
using SimpleNet.Transport;
using SimpleNet.Synchronization;
using SimpleNet.Serializer;

namespace SimpleNet.Network
{
    internal class NetConn
    {
        internal int Id { get; private set; }
        internal bool IsHost { get; private set; }
        internal List<int> Objects;
        private readonly ITransport _transport;
        internal NetConn(int id, bool isHost)
        {
            Id = id;
            IsHost = isHost;
            _transport = NetManager.Transport;
        }

        internal void Disconnect()
        {
            _transport?.Kick(Id);
        }
        internal void Send(NetMessage netMessage)
        {
            byte[] data = NetSerializer.Serialize(netMessage);
            if(IsHost)
                _transport.SendTo(Id, data);
            else _transport.Send(data);
        }
        internal void Own(NetObject netObject)
        {
            Objects.Add(netObject.NetId);
            netObject.GiveOwner(Id);
        }
        internal void Disown(NetObject netObject)
        {
            Objects.Remove(netObject.NetId);
        }
    }
}
