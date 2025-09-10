using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SimpleNet.Transport.UDP;
using SimpleNet.Utilities;
using static SimpleNet.Transport.ITransport;

namespace SimpleNet.Transport.TCP
{
    /// <summary>
    /// High-performance TCP transport implementation using native .NET sockets
    /// </summary>
    public class TCPSolution : ITransport
    {
        private Socket _listenerSocket;
        private Socket _clientSocket;
        private readonly ConcurrentQueue<byte[]> _packetQueue = new ConcurrentQueue<byte[]>();
        private readonly Dictionary<int, ConnectionInfo> _connectionInfo = new Dictionary<int, ConnectionInfo>();
        private readonly Dictionary<int, Socket> _clientSockets = new Dictionary<int, Socket>();
        private readonly object _lockObject = new object();
        
        private bool _isServer;
        private bool _isRunning;
        private int _port;
        private int _nextClientId = -1;
        private int _assignedClientId = -1; // client-side: server-assigned id after handshake
        private ServerInfo _serverInfo;
        private Thread _pollingThread;
        private CancellationTokenSource _cancellationTokenSource;
        
        // Server discovery and broadcasting
        private LANDiscovery _lanDiscovery;
        private LANBroadcast _lanBroadcaster;
        private List<ServerInfo> _lanServers;
        private int _bandwidthLimit;

        public TCPSolution()
        {
            _lanServers = new List<ServerInfo>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Setup(int port, bool isServer, ServerInfo serverInfo = null)
        {
            if (_isRunning) 
                Stop();

            _port = port;
            _isServer = isServer;

            if (isServer && serverInfo == null)
            {
                serverInfo = new ServerInfo()
                {
                    Address = GetLocalIPAddress(),
                    Port = port,
                    ServerName = "TCP_NetServer",
                    MaxPlayers = 10,
                    CustomData = new Dictionary<string, string>()
                };
            }
            
            _serverInfo = serverInfo;
        }

        public void Start()
        {
            if (_isServer)
            {
                StartServer();
            }
            else
            {
                StartClient();
            }
            
            StartPollingThread();
        }

        private void StartServer()
        {
            try
            {
                _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
                _listenerSocket.Listen(100); // Allow up to 100 pending connections
                
                DebugQueue.AddMessage($"[TCP SERVER] Listening on port {_port}");
                _nextClientId = -1; // ensure first accepted client gets id 0
                _isRunning = true;
                
                // Start accepting connections asynchronously
                _ = AcceptConnectionsAsync();
            }
            catch (Exception ex)
            {
                DebugQueue.AddMessage($"[TCP SERVER] Failed to start: {ex.Message}", DebugQueue.MessageType.Error);
                throw;
            }
        }

        private void StartClient()
        {
            _isRunning = true;
            DebugQueue.AddMessage("[TCP CLIENT] Client initialized");
        }

        private async Task AcceptConnectionsAsync()
        {
            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var clientSocket = await _listenerSocket.AcceptAsync();
                    var clientId = Interlocked.Increment(ref _nextClientId);
                    
                    lock (_lockObject)
                    {
                        _clientSockets[clientId] = clientSocket;
                    }
                    
                    DebugQueue.AddMessage($"[TCP SERVER] Client {clientId} connected from {clientSocket.RemoteEndPoint}");
                    
                    // Send assigned client id to the client as 4-byte little-endian int
                    try
                    {
                        var idBytes = BitConverter.GetBytes(clientId);
                        clientSocket.Send(idBytes);
                    }
                    catch (Exception ex)
                    {
                        DebugQueue.AddMessage($"[TCP SERVER] Failed to send client id {clientId}: {ex.Message}", DebugQueue.MessageType.Warning);
                    }

                    // Update connection info
                    UpdateConnectionInfo(clientId, ConnectionState.Connected);
                    TriggerOnClientConnected(clientId);
                    
                    // Start handling this client
                    _ = HandleClientAsync(clientId, clientSocket);
                }
                catch (ObjectDisposedException)
                {
                    // Socket was closed, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        DebugQueue.AddMessage($"[TCP SERVER] Error accepting connection: {ex.Message}", DebugQueue.MessageType.Error);
                    }
                }
            }
        }

        private async Task HandleClientAsync(int clientId, Socket clientSocket)
        {
            var buffer = new byte[4096];
            
            try
            {
                while (_isRunning && clientSocket != null && clientSocket.Connected && !_cancellationTokenSource.IsCancellationRequested)
                {
                    var bytesReceived = await clientSocket.ReceiveAsync(buffer, SocketFlags.None);
                    
                    if (bytesReceived == 0)
                    {
                        // Client disconnected
                        break;
                    }
                    
                    // Copy received data
                    var data = new byte[bytesReceived];
                    Array.Copy(buffer, data, bytesReceived);
                    _packetQueue.Enqueue(data);
                    
                    // Update connection info
                    if (_connectionInfo.TryGetValue(clientId, out var info))
                    {
                        info.BytesReceived += bytesReceived;
                    }
                    
                    TriggerOnDataReceived(clientId);
                    DebugQueue.AddMessage($"[TCP SERVER] Received {bytesReceived} bytes from client {clientId}");
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected during shutdown/stop; suppress noisy errors
            }
            catch (SocketException se)
            {
                // Ignore expected socket errors that occur during shutdown
                if (_isRunning && !_cancellationTokenSource.IsCancellationRequested)
                {
                    DebugQueue.AddMessage($"[TCP SERVER] Socket error handling client {clientId}: {se.Message}", DebugQueue.MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                if (_isRunning && !_cancellationTokenSource.IsCancellationRequested)
                {
                    DebugQueue.AddMessage($"[TCP SERVER] Error handling client {clientId}: {ex.Message}", DebugQueue.MessageType.Error);
                }
            }
            finally
            {
                // Clean up client connection
                lock (_lockObject)
                {
                    _clientSockets.Remove(clientId);
                }
                
                UpdateConnectionInfo(clientId, ConnectionState.Disconnected);
                TriggerOnClientDisconnected(clientId);
                
                try
                {
                    clientSocket.Close();
                }
                catch { }
                
                DebugQueue.AddMessage($"[TCP SERVER] Client {clientId} disconnected");
            }
        }

        public void Connect(string address)
        {
            if (_isServer)
            {
                DebugQueue.AddMessage("[TCP SERVER] Cannot connect to a client as a server.", DebugQueue.MessageType.Warning);
                return;
            }

            try
            {
                _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _clientSocket.Connect(address, _port);
                
                DebugQueue.AddMessage($"[TCP CLIENT] Connected to {address}:{_port}");
                
                // Start receiving data from server
                _ = HandleServerDataAsync();
            }
            catch (Exception ex)
            {
                DebugQueue.AddMessage($"[TCP CLIENT] Failed to connect: {ex.Message}", DebugQueue.MessageType.Error);
                UpdateConnectionInfo(_assignedClientId == -1 ? 0 : _assignedClientId, ConnectionState.Disconnected);
            }
        }

        private async Task HandleServerDataAsync()
        {
            var buffer = new byte[4096];
            
            try
            {
                while (_isRunning && _clientSocket?.Connected == true && !_cancellationTokenSource.IsCancellationRequested)
                {
                    var bytesReceived = await _clientSocket.ReceiveAsync(buffer, SocketFlags.None);
                    
                    if (bytesReceived == 0)
                    {
                        // Server disconnected
                        break;
                    }
                    
                    // If this is the first packet, treat it as the assigned client id handshake
                    if (_assignedClientId == -1 && bytesReceived >= 4)
                    {
                        _assignedClientId = BitConverter.ToInt32(buffer, 0);
                        UpdateConnectionInfo(_assignedClientId, ConnectionState.Connected);

                        // If there is extra payload beyond the 4-byte id, enqueue it
                        var remaining = bytesReceived - 4;
                        if (remaining > 0)
                        {
                            var dataAfterId = new byte[remaining];
                            Array.Copy(buffer, 4, dataAfterId, 0, remaining);
                            _packetQueue.Enqueue(dataAfterId);
                            if (_connectionInfo.TryGetValue(_assignedClientId, out var infoAfterId))
                            {
                                infoAfterId.BytesReceived += remaining;
                            }
                        }
                        continue;
                    }

                    // Copy received data (normal payload)
                    var data = new byte[bytesReceived];
                    Array.Copy(buffer, data, bytesReceived);
                    _packetQueue.Enqueue(data);
                    
                    // Update connection info
                    if (_connectionInfo.TryGetValue(_assignedClientId == -1 ? 0 : _assignedClientId, out var info))
                    {
                        info.BytesReceived += bytesReceived;
                    }
                    
                    TriggerOnDataReceived(_assignedClientId == -1 ? 0 : _assignedClientId);
                    DebugQueue.AddMessage($"[TCP CLIENT] Received {bytesReceived} bytes from server");
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected during shutdown/stop; suppress
            }
            catch (SocketException se)
            {
                if (_isRunning && !_cancellationTokenSource.IsCancellationRequested)
                {
                    DebugQueue.AddMessage($"[TCP CLIENT] Socket error receiving data: {se.Message}", DebugQueue.MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                if (_isRunning && !_cancellationTokenSource.IsCancellationRequested)
                {
                    DebugQueue.AddMessage($"[TCP CLIENT] Error receiving data: {ex.Message}", DebugQueue.MessageType.Error);
                }
            }
            finally
            {
                var idForDisconnect = _assignedClientId == -1 ? 0 : _assignedClientId;
                UpdateConnectionInfo(idForDisconnect, ConnectionState.Disconnected);
                DebugQueue.AddMessage("[TCP CLIENT] Disconnected from server");
            }
        }

        public void Disconnect()
        {
            if (_isServer)
            {
                lock (_lockObject)
                {
                    foreach (var kvp in _clientSockets)
                    {
                        try
                        {
                            kvp.Value.Close();
                        }
                        catch { }
                    }
                    _clientSockets.Clear();
                }
            }
            else
            {
                try
                {
                    _clientSocket?.Close();
                }
                catch { }
            }
            
            DebugQueue.AddMessage("[TCP] All connections closed");
        }

        public void Kick(int id)
        {
            if (!_isServer)
            {
                DebugQueue.AddMessage("[TCP CLIENT] Client cannot kick other clients.", DebugQueue.MessageType.Warning);
                return;
            }

            lock (_lockObject)
            {
                if (_clientSockets.TryGetValue(id, out var socket))
                {
                    try
                    {
                        socket.Close();
                        _clientSockets.Remove(id);
                        UpdateConnectionInfo(id, ConnectionState.Disconnected);
                        TriggerOnClientDisconnected(id);
                        DebugQueue.AddMessage($"[TCP SERVER] Client {id} kicked");
                    }
                    catch (Exception ex)
                    {
                        DebugQueue.AddMessage($"[TCP SERVER] Error kicking client {id}: {ex.Message}", DebugQueue.MessageType.Error);
                    }
                }
            }
        }

        public void Send(byte[] data)
        {
            if (_isServer)
            {
                // Send to all connected clients
                lock (_lockObject)
                {
                    var disconnectedClients = new List<int>();
                    
                    foreach (var kvp in _clientSockets)
                    {
                        try
                        {
                            kvp.Value.Send(data);
                            
                            if (_connectionInfo.TryGetValue(kvp.Key, out var info))
                            {
                                info.BytesSent += data.Length;
                            }
                        }
                        catch
                        {
                            disconnectedClients.Add(kvp.Key);
                        }
                    }
                    
                    // Remove disconnected clients
                    foreach (var clientId in disconnectedClients)
                    {
                        _clientSockets.Remove(clientId);
                        UpdateConnectionInfo(clientId, ConnectionState.Disconnected);
                        TriggerOnClientDisconnected(clientId);
                    }
                }
                
                DebugQueue.AddMessage("[TCP SERVER] Sent message to all clients");
            }
            else
            {
                // Send to server
                try
                {
                    if (_clientSocket?.Connected == true)
                    {
                        _clientSocket.Send(data);
                        
                        if (_connectionInfo.TryGetValue(_assignedClientId == -1 ? 0 : _assignedClientId, out var info))
                        {
                            info.BytesSent += data.Length;
                        }
                        
                        DebugQueue.AddMessage("[TCP CLIENT] Sent message to server");
                    }
                }
                catch (Exception ex)
                {
                    DebugQueue.AddMessage($"[TCP CLIENT] Error sending data: {ex.Message}", DebugQueue.MessageType.Error);
                }
            }
        }

        public void SendTo(int id, byte[] data)
        {
            if (!_isServer)
            {
                DebugQueue.AddMessage("[TCP CLIENT] Client cannot send data to other clients. Use ITransport.Send instead.", DebugQueue.MessageType.Warning);
                return;
            }

            lock (_lockObject)
            {
                if (_clientSockets.TryGetValue(id, out var socket))
                {
                    try
                    {
                        socket.Send(data);
                        
                        if (_connectionInfo.TryGetValue(id, out var info))
                        {
                            info.BytesSent += data.Length;
                        }
                        
                        DebugQueue.AddMessage($"[TCP SERVER] Sent message to client {id}");
                    }
                    catch (Exception ex)
                    {
                        DebugQueue.AddMessage($"[TCP SERVER] Error sending to client {id}: {ex.Message}", DebugQueue.MessageType.Error);
                        _clientSockets.Remove(id);
                        UpdateConnectionInfo(id, ConnectionState.Disconnected);
                        TriggerOnClientDisconnected(id);
                    }
                }
            }
        }

        public byte[] Receive()
        {
            if (_packetQueue.TryDequeue(out byte[] packet))
            {
                return packet;
            }
            return Array.Empty<byte>();
        }

        public List<ServerInfo> GetDiscoveredServers()
        {
            return new List<ServerInfo>(_lanServers);
        }

        public ConnectionInfo GetConnectionInfo(int clientId)
        {
            return _connectionInfo.TryGetValue(clientId, out var info) ? info : null;
        }

        public void SetConnectionId(int clientId, int connectionId)
        {
            if (_connectionInfo.TryGetValue(clientId, out var info))
            {
                info.Id = connectionId;
                _connectionInfo[clientId] = info;
            }
        }

        public ConnectionState GetConnectionState(int clientId)
        {
            return _connectionInfo.TryGetValue(clientId, out var info) ? info.State : ConnectionState.Disconnected;
        }

        public void SetServerInfo(ServerInfo serverInfo)
        {
            if (serverInfo != null)
            {
                if (_serverInfo == null)
                {
                    _serverInfo = serverInfo;
                }
                else
                {
                    var merged = new ServerInfo
                    {
                        Address = !string.IsNullOrEmpty(serverInfo.Address) ? serverInfo.Address : _serverInfo.Address,
                        Port = serverInfo.Port > 0 ? serverInfo.Port : _serverInfo.Port,
                        ServerName = !string.IsNullOrEmpty(serverInfo.ServerName) ? serverInfo.ServerName : _serverInfo.ServerName,
                        CurrentPlayers = serverInfo.CurrentPlayers != 0 ? serverInfo.CurrentPlayers : _serverInfo.CurrentPlayers,
                        MaxPlayers = serverInfo.MaxPlayers != 0 ? serverInfo.MaxPlayers : _serverInfo.MaxPlayers,
                        GameMode = !string.IsNullOrEmpty(serverInfo.GameMode) ? serverInfo.GameMode : _serverInfo.GameMode,
                        Ping = serverInfo.Ping != 0 ? serverInfo.Ping : _serverInfo.Ping,
                        CustomData = new Dictionary<string, string>()
                    };

                    if (_serverInfo.CustomData != null)
                    {
                        foreach (var kvp in _serverInfo.CustomData)
                        {
                            merged.CustomData[kvp.Key] = kvp.Value;
                        }
                    }
                    if (serverInfo.CustomData != null)
                    {
                        foreach (var kvp in serverInfo.CustomData)
                        {
                            merged.CustomData[kvp.Key] = kvp.Value;
                        }
                    }

                    _serverInfo = merged;
                }
                if (_isServer)
                {
                    _lanBroadcaster?.SetServerInfo(_serverInfo);
                }
            }
        }

        public ServerInfo GetServerInfo()
        {
            return _serverInfo;
        }

        public void UpdateServerInfo(Dictionary<string, string> customData)
        {
            if (_serverInfo?.CustomData != null)
            {
                foreach (var kvp in customData)
                {
                    _serverInfo.CustomData[kvp.Key] = kvp.Value;
                }
                
                if (_isServer)
                {
                    _lanBroadcaster?.SetServerInfo(_serverInfo);
                }
            }
        }

        public void SetBandwidthLimit(int bytesPerSecond)
        {
            _bandwidthLimit = bytesPerSecond;
            // TCP doesn't need explicit bandwidth limiting as it's handled by the protocol
            DebugQueue.AddMessage($"[TCP] Bandwidth limit set to {bytesPerSecond} bytes/second");
        }

        public void StartServerDiscovery(float discoveryInterval, int discoveryPort = -1)
        {
            if (!_isServer)
            {
                _lanServers = new List<ServerInfo>();
                _lanDiscovery = new LANDiscovery();
                _lanDiscovery.OnServerFound += serverInfo =>
                {
                    if (_lanServers.Contains(serverInfo))
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
                
                if (discoveryPort == -1) 
                    _lanDiscovery.StartDiscovery();
                else 
                    _lanDiscovery.StartDiscovery(discoveryPort);
            }
        }

        public void BroadcastServerInfo()
        {
            if (_isServer)
            {
                _lanBroadcaster = new LANBroadcast();
                _lanBroadcaster.StartBroadcast();
                _lanBroadcaster.SetServerInfo(_serverInfo);
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

        public void Stop()
        {
            _isRunning = false;
            _cancellationTokenSource?.Cancel();

            if (_pollingThread != null && _pollingThread.IsAlive)
            {
                _pollingThread.Join(1000); // Wait up to 1 second
            }

            _lanDiscovery?.StopDiscovery();
            _lanBroadcaster?.StopBroadcast();
            
            Disconnect();
            
            try
            {
                _listenerSocket?.Close();
            }
            catch { }
            
            try
            {
                _clientSocket?.Close();
            }
            catch { }
            
            _connectionInfo.Clear();
            _lanServers.Clear();
            
            DebugQueue.AddMessage("[TCP] Transport stopped");
        }

        private void StartPollingThread()
        {
            _pollingThread = new Thread(PollNetwork)
            {
                IsBackground = true,
                Name = "TCPTransport_Polling"
            };
            _pollingThread.Start();
        }

        private void PollNetwork()
        {
            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    Thread.Sleep(15); // Prevent excessive CPU usage
                }
                catch (Exception ex)
                {
                    DebugQueue.AddMessage($"[TCP] Polling error: {ex.Message}", DebugQueue.MessageType.Error);
                }
            }
        }

        private void UpdateConnectionInfo(int clientId, ConnectionState state, int ping = 0, float packetLoss = 0)
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
                if (info.State != state) 
                    TriggerOnConnectionStateChanged(_connectionInfo[clientId]);
                info.State = state;
                info.Ping = ping;
                info.PacketLoss = packetLoss;
                _connectionInfo[clientId] = info;
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
        }
    }
}
