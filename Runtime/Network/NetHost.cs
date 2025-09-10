using System;
using System.Collections.Concurrent;
using System.Linq;
using SimpleNet.Messages;
using SimpleNet.Synchronization;
using SimpleNet.Transport;
using SimpleNet.Utilities;

namespace SimpleNet.Network
{
    internal static class NetHost
    {
        internal static ConcurrentDictionary<int, NetConn> Clients = new();
        internal static void StartHost()
        {
            NetManager.Transport.Start();
            NetManager.AllPlayers.Add(-1);
            NetScene.Init();
            RPCManager.Init();
            ITransport.OnClientConnected += OnClientConnected;;
            ITransport.OnClientDisconnected += OnClientDisconnected;
            Messager.RegisterHandler<SyncMessage>(OnSyncMessage);
            Messager.RegisterHandler<ConnMessage>(OnConnMessage);
        }
        private static void OnConnMessage(ConnMessage obj)
        {
            if(!obj.AllConnected.Count.Equals(NetManager.AllPlayers.Count))
                UpdatePlayers(obj.CurrentConnected);
        }


        private static void OnClientDisconnected(int id)
        {
            if(Clients.TryRemove(id, out _))
            {
                DebugQueue.AddMessage($"Client {id} disconnected. Clients count: {Clients.Count}", DebugQueue.MessageType.Network);
                NetManager.AllPlayers.Remove(id);
                UpdatePlayers(id);
                NetManager.EnqueueMainThread(() => NetScene.DisconnectClient(id));
            }
        }


        private static void OnClientConnected(int id)
        {
            if (Clients.TryAdd(id, new NetConn(id, true)))
            {
                DebugQueue.AddMessage($"Client {id} connected. Clients count: {Clients.Count}", DebugQueue.MessageType.Network);
                NetManager.AllPlayers.Add(id);
                UpdatePlayers(id);
                //NetScene.SendScene(id);
            }
        }

        internal static void UpdatePlayers(int id)
        {
            if (Clients.Count == 0) return;
            
            ServerInfo info = NetManager.GetServerInfo();
            info.CurrentPlayers = NetManager.PlayerCount;
            NetManager.Transport.SetServerInfo(info);
            NetMessage msg = new ConnMessage(id, NetManager.AllPlayers, info);
            Send(msg);
        }

        internal static void Stop()
        {
            foreach (var client in Clients.Values)
            {
                client.Disconnect();
            }

            NetManager.Transport.Stop();
            ITransport.OnClientConnected -= OnClientConnected;
            ITransport.OnClientDisconnected -= OnClientDisconnected;
            Clients.Clear();
        }

        internal static void Kick(int id)
        {
            if (Clients.TryGetValue(id, out NetConn client))
            {
                client.Disconnect();
            }
        }

        internal static void Send(NetMessage netMessage)
        {
            if (netMessage.target == null)
            {
                foreach (var client in Clients.Values)
                {
                    if(netMessage is not SyncMessage) DebugQueue.AddNetworkMessage(netMessage, false);
                    client.Send(netMessage);
                }
            }
            else
            {
                foreach (int targetId in netMessage.target)
                {
                    if (Clients.TryGetValue(targetId, out NetConn client))
                    {
                        if(netMessage is not SyncMessage) DebugQueue.AddNetworkMessage(netMessage, false);
                        client.Send(netMessage);
                    }
                }
            }
        }
        private static void OnSyncMessage(SyncMessage obj)
        {
            StateManager.SetSync(obj);
            if(obj.target == null)
            {
                obj.target = Clients.Keys.ToList();
            }
            obj.target.Remove(obj.SenderId);
            Send(obj);
            if (NetManager.Rollback)
            {
                NetMessage msg = new ReconcileMessage(obj.ObjectID, obj.ComponentId, DateTime.UtcNow, obj.changedValues, obj.SenderId);
                Send(msg);
            }
        }
        

    }
}
