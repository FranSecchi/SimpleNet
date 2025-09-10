using SimpleNet.Messages;
using SimpleNet.Synchronization;
using SimpleNet.Utilities;

namespace SimpleNet.Network
{
    internal static class NetClient
    {
        internal static NetConn Connection;
        internal static void Connect(string address)
        {
            if (Connection != null) return;
            NetScene.Init();
            RPCManager.Init();
            Messager.RegisterHandler<ConnMessage>(OnConnected);
            Messager.RegisterHandler<SyncMessage>(OnSync);
            NetManager.Transport.Start();
            NetManager.Transport.Connect(address);
        }

        private static void OnSync(SyncMessage obj)
        {
            StateManager.SetSync(obj);
        }
        internal static void Disconnect()
        {
            NetManager.Transport.Stop();
            Connection = null;
        }

        internal static void Send(NetMessage netMessage)
        {
            if(netMessage is not SyncMessage) DebugQueue.AddNetworkMessage(netMessage, false);
            Connection?.Send(netMessage);
        }
        private static void OnConnected(ConnMessage connection)
        {
            if(Connection == null)
            {
                Connection = new NetConn(connection.CurrentConnected, false);
                NetManager.Transport.SetConnectionId(0, Connection.Id);
            }
            NetManager.AllPlayers = connection.AllConnected;
            NetManager.SetServerInfo(connection.ServerInfo);
        }

    }
}
