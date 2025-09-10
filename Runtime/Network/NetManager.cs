using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SimpleNet.Synchronization;
using SimpleNet.Serializer;
using SimpleNet.Messages;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using SimpleNet.Utilities;
using UnityEngine;

[assembly: InternalsVisibleTo("SimpleNet.Editor")]
[assembly: InternalsVisibleTo("SimpleNet.Tests")]
[assembly: InternalsVisibleTo("SimpleNet.Tests.Editor")]

namespace SimpleNet.Network
{
    /// <summary>
    /// Main network manager class that handles network initialization, connection management, and message routing.
    /// </summary>
    public class NetManager : MonoBehaviour
    {
        private static NetManager _manager;
        private static ServerInfo _serverInfo;
        
        private readonly Queue<Action> mainThreadActions = new();
        private List<ServerInfo> _discoveredServers = new List<ServerInfo>();
        private float _lastStateUpdate;
        private float _lastLanDiscovery;
        private bool _isHost = false;
        private bool _running = false;
        internal static ITransport Transport;

        /// <summary>
        /// Registry containing all network prefabs that can be spawned.
        /// </summary>
        public NetPrefabRegistry NetPrefabs;
        
        /// <summary>
        /// The port number used for network communication.
        /// </summary>
        public static int Port = 7777;

        /// <summary>
        /// List of all connected player IDs.
        /// </summary>
        public static List<int> AllPlayers;
        
        /// <summary>
        /// The address of the server to connect to.
        /// </summary>
        [Tooltip("The address of the server to connect to")]
        public string address = "localhost";
        /// <summary>
        /// The name of the server.
        /// </summary>
        [Tooltip("The name of the server that will be displayed to clients")]
        [SerializeField] public string serverName = "Net_Server";

        /// <summary>
        /// Maximum number of players allowed on the server.
        /// </summary>
        [Tooltip("Maximum number of players that can connect to the server")]
        [SerializeField] public int maxPlayers = 10;

        /// <summary>
        /// Whether to use Rollback for synchronizing.
        /// </summary>
        [Tooltip("Enable Rollback")]
        [SerializeField] public bool useRollback = false;
        /// <summary>
        /// Maximum time in seconds to save states
        /// </summary>
        [Tooltip("Maximum time in seconds to save states")]
        [SerializeField] public float rollbackWindow = 0.1f;
        /// <summary>
        /// Maximum GameObject states to save at the same time
        /// </summary>
        [Tooltip("Maximum GameObject states to save at the same time")]
        [SerializeField] public int maxStates = 20;
        /// <summary>
        /// Whether to use LAN discovery for server finding.
        /// </summary>
        [Tooltip("Enable LAN server discovery")]
        [SerializeField] public bool useLAN = false;

        /// <summary>
        /// Interval between LAN server discovery broadcasts in seconds.
        /// </summary>
        [Tooltip("Time between LAN server discovery broadcasts (in seconds)")]
        [SerializeField] public float lanDiscoveryInterval = 0.1f;

        /// <summary>
        /// Interval between state updates in seconds.
        /// </summary>
        [Tooltip("Time between network state updates (in seconds)")]
        [SerializeField] public float stateUpdateInterval = 0.05f;


        /// <summary>
        /// Gets the list of network prefabs.
        /// </summary>
        public static NetPrefabRegistry PrefabsList => _manager.NetPrefabs;

        /// <summary>
        /// Gets whether this instance is acting as a host.
        /// </summary>
        public static bool IsHost => _manager._isHost;

        /// <summary>
        /// Gets the current server name.
        /// </summary>
        public static string ServerName => _manager.serverName;

        /// <summary>
        /// Gets the maximum number of players allowed.
        /// </summary>
        public static int MaxPlayers => _manager.maxPlayers;

        /// <summary>
        /// Gets the current number of connected players.
        /// </summary>
        public static int PlayerCount => AllPlayers.Count;

        /// <summary>
        /// Gets whether the network system is currently running.
        /// </summary>
        public static bool Running => _manager._running;
        public static bool Rollback => _manager.useRollback;

        /// <summary>
        /// Gets whether the client is currently connected to a server.
        /// </summary>
        public static bool Connected => GetConnectionState() == ConnectionState.Connected;

        /// <summary>
        /// Gets whether the network manager is active.
        /// </summary>
        public static bool Active => _manager != null;

        /// <summary>
        /// Gets or sets whether LAN discovery is enabled.
        /// </summary>
        public static bool UseLan
        {
            get => _manager.useLAN;
            set => _manager.useLAN = value;
        }


        /// <summary>
        /// Sets the transport layer implementation for network communication.
        /// </summary>
        /// <param name="transport">The transport implementation to use.</param>
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
            AllPlayers = new List<int>();
            Init();
            _running = false;
            DontDestroyOnLoad(this);
        }
        /// <summary>
        /// Enqueues an action to be executed on the main thread.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        public static void EnqueueMainThread(Action action)
        {
            lock (_manager.mainThreadActions)
            {
                _manager.mainThreadActions.Enqueue(action);
            }
        }

        internal static void Init()
        {
            if(_manager.NetPrefabs != null) NetScene.RegisterPrefabs(_manager.NetPrefabs.prefabs);
            if(Rollback) RollbackManager.Initialize(_manager.rollbackWindow, _manager.maxStates);
        }
        private void Update()
        {
            lock (mainThreadActions)
            {
                while (mainThreadActions.Count > 0)
                {
                    var action = mainThreadActions.Dequeue();
                    action?.Invoke();
                }
            }
            
            if (_running)
            {
                float currentTime = Time.time;
                if (currentTime - _lastStateUpdate >= stateUpdateInterval)
                {
                    StateManager.SendUpdateStates();
                    _lastStateUpdate = currentTime;
                    if(useRollback) RollbackManager.Update();
                }
            }
        }

        private void OnDestroy()
        {
            StopNet();
            _manager.mainThreadActions.Clear();
            Messager.ClearHandlers();
            NetScene.CleanUp();
            if(useRollback) RollbackManager.Clear();
        }

        private void OnApplicationQuit()
        {
            StopNet();
            _manager.mainThreadActions.Clear();
            Messager.ClearHandlers();
            NetScene.CleanUp();
            if(useRollback) RollbackManager.Clear();
        }

        /// <summary>
        /// Starts a new network host with optional server information.
        /// </summary>
        /// <param name="serverInfo">Optional server information. If null, default values will be used.</param>
        public static void StartHost(ServerInfo serverInfo = null)
        {
            StopNet();
            ITransport.OnDataReceived += Receive;
            if(serverInfo != null)
            {
                if (serverInfo.Address == null)
                    serverInfo.Address = Transport.GetLocalIPAddress();
                _serverInfo = serverInfo;
            }
            else
            {
                _serverInfo = new ServerInfo()
                {
                    CurrentPlayers = 0,
                    MaxPlayers = _manager.maxPlayers,
                    Address = Transport.GetLocalIPAddress(),
                    Port = Port,
                    ServerName = _manager.serverName,
                    GameMode = "Unknown",
                };
            }
            Transport.Setup(Port, true, _serverInfo);
            _manager._isHost = true;
            _manager._running = true;
            NetHost.StartHost();
            if (UseLan)
            {
                Transport.BroadcastServerInfo();
            }
        }
        /// <summary>
        /// Starts the network client and connects to the server.
        /// </summary>
        public static void StartClient()
        {
            StopNet();
            ITransport.OnDataReceived += Receive;
            Transport.Setup(Port, false);
            _manager._isHost = false;
            _manager._running = true;
            if (!_manager.useLAN)
                NetClient.Connect(_manager.address);
            else
            {
                Transport.StartServerDiscovery(_manager.lanDiscoveryInterval);
                ITransport.OnLanServerUpdate += UpdateLanServers;
                _manager._discoveredServers.Clear();
            }
        }

        /// <summary>
        /// Connects to a specific server address.
        /// </summary>
        /// <param name="address">The address of the server to connect to.</param>
        public static void ConnectTo(string address)
        {
            StopNet();
            ITransport.OnDataReceived += Receive;
            Transport.Setup(Port, false);
            _manager._isHost = false;
            _manager._running = true;
            NetClient.Connect(address);
        }
        /// <summary>
        /// Stops all network operations and cleans up resources.
        /// </summary>
        public static void StopNet()
        {
            if (!_manager._running) return;
            if (UseLan) StopLan();
            if (IsHost) NetHost.Stop();
            else NetClient.Disconnect();
            AllPlayers.Clear();
            NetScene.CleanUp();
            Messager.ClearHandlers();
            ITransport.OnDataReceived -= Receive;
            
            _manager._running = false;
        }

        /// <summary>
        /// Gets the current connection ID.
        /// </summary>
        /// <returns>The connection ID if connected as a client, -1 if hosting.</returns>
        public static int ConnectionId()
        {
            if (!IsHost) return Connected ? NetClient.Connection.Id : -2;
            return -1;
        }
        /// <summary>
        /// Sends a network message to the connected peers.
        /// </summary>
        /// <param name="netMessage">The message to send.</param>
        public static void Send(NetMessage netMessage)
        {
            if (!_manager._running) return;
            if(IsHost)
                NetHost.Send(netMessage);
            else NetClient.Send(netMessage);
        }
        
        /// <summary>
        /// Spawns a network object at the specified position and rotation.
        /// </summary>
        /// <param name="prefab">The prefab to spawn.</param>
        /// <param name="position">The position to spawn at.</param>
        /// <param name="rotation">The rotation of the spawned object.</param>
        /// <param name="owner">The ID of the client that will own the object. Defaults to -1 (no owner).</param>
        public static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation = default, int owner = -1)
        {
            if (!_manager._running) return;
            SpawnMessage spm = new SpawnMessage(ConnectionId(), prefab.name, position, rotation, owner);
            spm.requesterId = ConnectionId();
            if (IsHost)
            {
                NetScene.Spawn(spm);
            }
            else
            {
                NetClient.Send(spm);
            }
        }

        /// <summary>
        /// Destroys a network object with the specified ID.
        /// </summary>
        /// <param name="netObjectId">The ID of the network object to destroy.</param>
        public static void Destroy(int netObjectId)
        {
            if (!_manager._running) return;
            var netObj = NetScene.GetNetObject(netObjectId);
            if (netObj != null && (netObj.Owned || IsHost))
            {
                DestroyMessage msg = new DestroyMessage(netObjectId, ConnectionId());
                if (IsHost)
                {
                    NetScene.Destroy(netObjectId);
                    NetHost.Send(msg);
                }
                else
                {
                    NetClient.Send(msg);
                }
            }
        }
        /// <summary>
        /// Stops LAN discovery and broadcasting.
        /// </summary>
        public static void StopLan()
        {
            if (!IsHost)
            {
                ITransport.OnLanServerUpdate -= UpdateLanServers;
                Transport.StopServerDiscovery();
                _manager._discoveredServers.Clear();
            }
            else Transport.StopServerBroadcast();
        }
        /// <summary>
        /// Gets the list of discovered LAN servers.
        /// </summary>
        /// <returns>A list of discovered server information.</returns>
        public static List<ServerInfo> GetDiscoveredServers()
        {
            return _manager._discoveredServers;
        }

        /// <summary>
        /// Gets the current server information.
        /// </summary>
        /// <returns>The server information if running, null otherwise.</returns>
        public static ServerInfo GetServerInfo()
        {
            if(!_manager._running) return null;
            _serverInfo = Transport.GetServerInfo();
            return _serverInfo;
        }
        /// <summary>
        /// Gets connection information for a specific client.
        /// </summary>
        /// <param name="clientId">The ID of the client to get information for. Defaults to 0.</param>
        /// <returns>The connection information if running, null otherwise.</returns>
        public static ConnectionInfo GetConnectionInfo(int clientId = 0)
        {
            if(!_manager._running) return null;
            return Transport.GetConnectionInfo(clientId);
        }
        /// <summary>
        /// Gets the connection state for a specific client.
        /// </summary>
        /// <param name="clientId">The ID of the client to get state for. Defaults to 0.</param>
        /// <returns>The connection state if running, null otherwise.</returns>
        public static ConnectionState? GetConnectionState(int clientId = 0)
        {
            if(!_manager._running) return null;
            return Transport.GetConnectionState(clientId);
        }

        /// <summary>
        /// Sets the server information and updates connected clients.
        /// </summary>
        /// <param name="serverInfo">The new server information.</param>
        public static void SetServerInfo(ServerInfo serverInfo)
        {
            _serverInfo = serverInfo;
            Transport.SetServerInfo(serverInfo);
            if(IsHost) NetHost.UpdatePlayers(ConnectionId());
        }
        /// <summary>
        /// Sets the server name and updates connected clients.
        /// </summary>
        /// <param name="serverName">The new server name.</param>
        public static void SetServerName(string serverName)
        {
            if (_manager._running && IsHost)
            {
                _serverInfo.ServerName = serverName;
                Transport.SetServerInfo(_serverInfo);
            }
        }

        /// <summary>
        /// Loads a scene on all connected clients.
        /// </summary>
        /// <param name="sceneName">The name of the scene to load.</param>
        public static void LoadScene(string sceneName)
        {
            if (!_manager._running) return;
            if(IsHost) NetScene.LoadScene(sceneName);
        }
        /// <summary>
        /// Gets information about all connected clients.
        /// </summary>
        /// <returns>A list of connection information for all clients, or null if not hosting.</returns>
        public static List<ConnectionInfo> GetClients()
        {
            if(!IsHost) return null;
            List<ConnectionInfo> clients = new List<ConnectionInfo>();
            for (int i = 0; i < AllPlayers.Count - 1; i++)
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
                if(msg is not SyncMessage) DebugQueue.AddNetworkMessage(msg);
                Messager.HandleMessage(msg);
            }
        }

        private static void UpdateLanServers(ServerInfo serverInfo)
        {
            var currentServers = Transport.GetDiscoveredServers();
            
            _manager._discoveredServers = new List<ServerInfo>(currentServers);
            DebugQueue.AddMessage($"Updated server list. Current servers: {string.Join(", ", _manager._discoveredServers)}");

        }

        public static List<NetObject> GetAllNetObjects()
        {
            return NetScene.GetAllNetObjects();
        }
    }
}
