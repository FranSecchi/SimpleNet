using System.Collections.Concurrent;
using System.Collections.Generic;
using SimpleNet.Serializer;
using SimpleNet.Messages;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using UnityEngine;

namespace SimpleNet.Network.Tests
{
    public class NetManagerTest : MonoBehaviour
    {
        private static NetManagerTest _manager;
        public static ITransport Transport;
        public static int Port = 7777;
        private static ServerInfo _serverInfo;
        public static List<int> allPlayers;
        private bool _isHost = false;
        private bool _running = false;
        
        [SerializeField] public string serverName = "Net_Server";
        [SerializeField] public int maxPlayers = 10;
        [SerializeField] public bool useLAN = false;
        [SerializeField] public bool debugLog = false;
        [SerializeField] public float lanDiscoveryInterval = 1f;
        private float _lastLanDiscovery;
        private List<ServerInfo> _discoveredServers = new List<ServerInfo>();
        
        public static bool IsHost => _manager._isHost;
        public static string ServerName => _manager.serverName;
        public static int MaxPlayers => _manager.maxPlayers;
        public static ServerInfo ServerInfo => _serverInfo;
        public static bool UseLan
        {
            get => _manager.useLAN;
            set => _manager.useLAN = value;
        }
        public static bool DebugLog
        {
            get => _manager.debugLog;
            set => _manager.debugLog = value;
        }
        public string address = "localhost";
        public static void SetTransport(ITransport transport)
        {
            Transport = transport;
        }
        private void Awake()
        {
            if (_manager != null)
                Destroy(this);
            else _manager = this;
            Transport ??= new UDPSolution();
            allPlayers = new List<int>();
            DontDestroyOnLoad(this);
        }
        private void Update()
        {
            if (useLAN && !IsHost)
            {
                if (Time.time - _lastLanDiscovery >= lanDiscoveryInterval)
                {
                    _discoveredServers = Transport.GetDiscoveredServers();
                    _lastLanDiscovery = Time.time;
                }
            }
        }
        private void OnDestroy()
        {
            StopNet();
        }

        private void OnApplicationQuit()
        {
            StopNet();
        }

        public static void StartHost(ServerInfo serverInfo = null)
        {
            StopNet();
            ITransport.OnDataReceived += Receive;
            if(serverInfo != null) _serverInfo = serverInfo;
            else
            {
                _serverInfo = new ServerInfo()
                {
                    CurrentPlayers = 0,
                    MaxPlayers = _manager.maxPlayers,
                    Address = Transport.GetLocalIPAddress(),
                    ServerName = _manager.serverName,
                    GameMode = "Unknown",
                };
            }
            Transport.Setup(Port, true, _serverInfo);
            _manager._isHost = true;
            _manager._running = true;
            NetHostTest.StartHost();
            if (UseLan)
            {
                Transport.BroadcastServerInfo();
            }
        }
        public static void StartClient()
        {
            StopNet();
            ITransport.OnDataReceived += Receive;
            Transport.Setup(Port, false);
            _manager._isHost = false;
            _manager._running = true;
            if (!_manager.useLAN)
                NetClientTest.Connect(_manager.address);
            else
            {
                Transport.StartServerDiscovery(_manager.lanDiscoveryInterval);
                _manager._discoveredServers.Clear();
            }
        }
        
        public static void ConnectTo(string address)
        {
            StopNet();
            ITransport.OnDataReceived += Receive;
            Transport.Setup(Port, false);
            _manager._isHost = false;
            NetClientTest.Connect(address);
        }
        public static void StopNet()
        {
            if (!_manager._running) return;
            if (UseLan) StopLan();
            if (IsHost) NetHostTest.Stop();
            else NetClientTest.Disconnect();
            allPlayers.Clear();
            Messager.ClearHandlers();
            ITransport.OnDataReceived -= Receive;
            
            _manager._running = false;
        }

        public static int ConnectionId()
        {
            if (!IsHost) return NetClientTest.Connection.Id;
            return -1;
        }
        public static void Send(NetMessage netMessage)
        {
            if(IsHost)
                NetHostTest.Send(netMessage);
            else NetClientTest.Send(netMessage);
        }
        
        public static void StopLan()
        {
            if (!IsHost)
            {
                Transport.StopServerDiscovery();
                _manager._discoveredServers.Clear();
            }
            else Transport.StopServerBroadcast();
        }
        public static List<ServerInfo> GetDiscoveredServers()
        {
            return _manager._discoveredServers;
        }

        public static ServerInfo GetServerInfo()
        {
            if(!_manager._running) return null;
            return Transport.GetServerInfo();
        }
        public static ConnectionInfo GetConnectionInfo(int clientId = 0)
        {
            if(!_manager._running) return null;
            return Transport.GetConnectionInfo(clientId);
        }
        public static ConnectionState? GetConnectionState(int clientId = 0)
        {
            if(!_manager._running) return null;
            return Transport.GetConnectionState(clientId);
        }

        public static void SetServerInfo(ServerInfo serverInfo)
        {
            _serverInfo = serverInfo;
            Transport.SetServerInfo(serverInfo);
            if(IsHost) NetHostTest.UpdatePlayers(ConnectionId());
        }
        public static void SetServerName(string serverName)
        {
            if (_manager._running && IsHost)
            {
                _serverInfo.ServerName = serverName;
                Transport.SetServerInfo(_serverInfo);
            }
        }

        public static List<ConnectionInfo> GetClients()
        {
            if(!IsHost) return null;
            List<ConnectionInfo> clients = new List<ConnectionInfo>();
            for (int i = 0; i < allPlayers.Count - 1; i++)
            {
                ConnectionInfo client = Transport.GetConnectionInfo(i);
                if(client != null) clients.Add(client);
            }
            return clients;
        }
        private static void Receive(int id)
        {
            byte[] data = Transport.Receive();
            
            if (data != null && data.Length != 0)
            {
                NetMessage msg = NetSerializer.Deserialize<NetMessage>(data);
                Messager.HandleMessage(msg);
            }
        }

    }
    public static class NetClientTest
    {
        public static NetConnTest Connection;
        public static void Connect(string address)
        {
            if (Connection != null) return;
            Messager.RegisterHandler<ConnMessage>(OnConnected);
            NetManagerTest.Transport.Start();
            NetManagerTest.Transport.Connect(address);
        }
        public static void Disconnect()
        {
            NetManagerTest.Transport.Stop();
            Connection = null;
        }

        private static void OnConnected(ConnMessage connection)
        {
            if(Connection != null) Connection = new NetConnTest(connection.CurrentConnected, false);
            NetManagerTest.allPlayers = connection.AllConnected;
            NetManagerTest.SetServerInfo(connection.ServerInfo);
        }

        public static void Send(NetMessage netMessage)
        {
            NetManagerTest.Transport.Send(NetSerializer.Serialize(netMessage));
        }
    }
    public static class NetHostTest
    {
        public static ConcurrentDictionary<int, NetConnTest> Clients = new();
        private static readonly object Lock = new object();
        public static void StartHost()
        {
            NetManagerTest.Transport.Start();
            NetManagerTest.allPlayers.Add(-1);
            ITransport.OnClientConnected += OnClientConnected;
            ITransport.OnClientDisconnected += OnClientDisconnected;
            Messager.RegisterHandler<ConnMessage>(OnConnMessage);
        }
        
        
        private static void OnConnMessage(ConnMessage obj)
        {
            if(!obj.AllConnected.Count.Equals(NetManagerTest.allPlayers.Count))
                UpdatePlayers(obj.CurrentConnected);
        }
        private static void OnClientConnected(int id)
        {
            if (Clients.TryAdd(id, new NetConnTest(id, true)))
            {
                NetManagerTest.allPlayers.Add(id);
                UpdatePlayers(id);
            }
        }

        
        private static void OnClientDisconnected(int id)
        {
            if(Clients.TryRemove(id, out _))
            {
                NetManagerTest.allPlayers.Remove(id);
                UpdatePlayers(id);
            }
        }
        
        public static void UpdatePlayers(int id)
        {
            if (Clients.Count == 0) return;
            NetMessage msg = new ConnMessage(id, NetManagerTest.allPlayers, NetManagerTest.ServerInfo);
            Send(msg);
        }
        public static void Stop()
        {
            foreach (var client in Clients.Values)
            {
                client.Disconnect();
            }

            NetManagerTest.Transport.Stop();
            ITransport.OnClientConnected -= OnClientConnected;
            ITransport.OnClientDisconnected -= OnClientDisconnected;
            Clients.Clear();
        }

        public static void Kick(int id)
        {
            if (Clients.TryGetValue(id, out NetConnTest client))
            {
                client.Disconnect();
            }
        }

        public static void Send(NetMessage netMessage)
        {
            if (netMessage.target == null)
            {
                foreach (var client in Clients.Values)
                {
                    client.Send(netMessage);
                }
            }
            else
            {
                foreach (int targetId in netMessage.target)
                {
                    if (Clients.TryGetValue(targetId, out NetConnTest client))
                    {
                        client.Send(netMessage);
                    }
                }
            }
        }
    }
    public class NetConnTest
    {
        public int Id { get; private set; }
        public bool IsHost { get; private set; }
        private readonly ITransport _transport;
        public NetConnTest(int id, bool isHost)
        {
            Id = id;
            IsHost = isHost;
            _transport = NetManagerTest.Transport;
        }

        public void Disconnect()
        {
            _transport?.Kick(Id);
        }
        public void Send(NetMessage netMessage)
        {
            byte[] data = NetSerializer.Serialize(netMessage);
            if(IsHost)
                _transport.SendTo(Id, data);
            else _transport.Send(data);
        }
    }
}

