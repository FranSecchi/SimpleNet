using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SimpleNet.Utilities;
using static SimpleNet.Transport.ITransport;

namespace SimpleNet.Transport.UDP
{
    internal class UDPSolution : ITransport
    {
        private APeer _aPeer;
        private Thread _pollingThread;
        private bool _isRunning;
        
        public bool IsHost;
        private LANDiscovery _lanDiscovery;
        private LANBroadcast _lanBroadcaster;
        private List<ServerInfo> _lanServers;
        private int _bandwidthLimit;

        public UDPSolution()
        {
            _lanServers = new List<ServerInfo>();
        }

        public void Setup(int port, bool isServer, ServerInfo serverInfo = null)
        {
            if(_isRunning) Disconnect();
            _aPeer = isServer ? new AHost(port) : new AClient(port);

            if (isServer && serverInfo == null)
            {
                serverInfo = new ServerInfo()
                {
                    Address = GetLocalIPAddress(),
                    Port = port,
                    ServerName = "New_NetServer",
                    MaxPlayers = 10
                };
            }
            _aPeer.ServerInfo = serverInfo;
            IsHost = isServer;
            _aPeer.MaxPlayers = serverInfo?.MaxPlayers ?? 10;
        }

        public void Start()
        {
            _aPeer.Start();
            StartThread();
        }

        public void Stop()
        {
            _isRunning = false;

            if (_pollingThread != null && _pollingThread.IsAlive)
            {
                _pollingThread.Join();
            }
            _lanDiscovery?.StopDiscovery();
            _lanBroadcaster?.StopBroadcast();
            _aPeer.Stop();
            _lanServers.Clear();
        }

        public void Connect(string address)
        {
            if(IsHost)
            {
                DebugQueue.AddMessage("[SERVER] Cannot connect to a client as a server.", DebugQueue.MessageType.Warning);
                return;
            }

            _aPeer.Connect(address);
        }

        public void Disconnect()
        {
            _aPeer.Disconnect();
        }

        public void Kick(int id)
        {
            if (!IsHost) 
            {
                DebugQueue.AddMessage("[Client] Client cannot kick other clients.", DebugQueue.MessageType.Warning);
                return;
            }

            _aPeer.Kick(id);
        }
        public void Send(byte[] data)
        {
            _aPeer.Send(data);
        }

        public void SendTo(int id, byte[] data)
        {
            if (!IsHost) 
            {
                DebugQueue.AddMessage("[Client] Client cannot send data to other clients. Use ITransport.Send instead.", DebugQueue.MessageType.Warning);
                return;
            }
            _aPeer.SendTo(id, data);
        }

        public byte[] Receive()
        {
            return _aPeer.Receive();
        }

        public List<ServerInfo> GetDiscoveredServers()
        {
            return new List<ServerInfo>(_lanServers);
        }

        public ConnectionInfo GetConnectionInfo(int clientId)
        {
            return _aPeer.ConnectionInfo.TryGetValue(clientId <= 0 ? 0 : clientId, out var info) ? info : null;
        }

        public void SetConnectionId(int clientId, int connectionId)
        {
            if (_aPeer.ConnectionInfo.TryGetValue(clientId <= 0 ? 0 : clientId, out var info))
            {
                info.Id = connectionId;
                _aPeer.ConnectionInfo[clientId] = info;
            }
        }

        public ConnectionState GetConnectionState(int clientId)
        {
            return _aPeer.ConnectionInfo.TryGetValue(clientId, out var info) ? info.State : ConnectionState.Disconnected;
        }

        public void SetServerInfo(ServerInfo serverInfo)
        {
            if (serverInfo != null)
            {
                ServerInfo info = _aPeer.ServerInfo;
                if (serverInfo.Address == null)
                {
                    serverInfo.Address = info.Address;
                    serverInfo.Port = info.Port;
                }
                _aPeer.ServerInfo = serverInfo;
                if (IsHost)
                {
                    _lanBroadcaster?.SetServerInfo(_aPeer.ServerInfo);
                }
            }
        }
        public ServerInfo GetServerInfo()
        {
            return _aPeer.ServerInfo;
        }
        public void UpdateServerInfo(Dictionary<string, string> customData)
        {
            foreach (var kvp in customData)
            {
                _aPeer.ServerInfo.CustomData[kvp.Key] = kvp.Value;
            }
            if (IsHost)
            {
                _lanBroadcaster?.SetServerInfo(_aPeer.ServerInfo);
            }
        }
        
        public void SetBandwidthLimit(int bytesPerSecond)
        {
            _bandwidthLimit = bytesPerSecond;
            _aPeer?.SetBandwidthLimit(bytesPerSecond);
        }

        public void StartServerDiscovery(float discoveryInterval, int discoveryPort = -1)
        {
            if (!IsHost)
            {
                _lanServers = new List<ServerInfo>();
                _lanDiscovery = new LANDiscovery();
                _lanDiscovery.OnServerFound += serverInfo =>
                {
                    if(_lanServers.Contains(serverInfo))
                    {
                        _lanServers[_lanServers.IndexOf(serverInfo)] = serverInfo;
                    }
                    else
                    {
                        _lanServers.Add(serverInfo);
                        DebugQueue.AddMessage($"Found new server at {serverInfo.Address} | {serverInfo.Port}");
                    }
                    TriggerOnLanServersUpdate(serverInfo);
                };
                _lanDiscovery.OnServerLost += serverInfo =>
                {
                    DebugQueue.AddMessage($"Lost server at {serverInfo.Address} | {serverInfo.Port}");
                    _lanServers.Remove(serverInfo);
                    TriggerOnLanServersUpdate(serverInfo);
                };
                if(discoveryPort == -1) _lanDiscovery.StartDiscovery();
                else _lanDiscovery.StartDiscovery(discoveryPort);
            }
        }
        public void BroadcastServerInfo()
        {
            if (IsHost)
            {
                _lanBroadcaster = new LANBroadcast();
                _lanBroadcaster.StartBroadcast();
                _lanBroadcaster.SetServerInfo(_aPeer.ServerInfo);
            }
        }
        public void StopServerDiscovery()
        {
            _lanDiscovery?.StopDiscovery();
        }
        public void StopServerBroadcast()
        {
            _lanBroadcaster?.StopBroadcast();
        }

        public string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
        private void StartThread()
        {
            _pollingThread = new Thread(PollNetwork)
            {
                IsBackground = true // Ensures it stops when Unity closes
            };
            _pollingThread.Start();
        }
        private void PollNetwork()
        {
            _isRunning = true;
            while (_isRunning)
            {
                _aPeer.Poll();
                Thread.Sleep(15); // Prevents excessive CPU usage
            }
        }
    }
}