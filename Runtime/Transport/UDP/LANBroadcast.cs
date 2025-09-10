using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace SimpleNet.Transport.UDP
{
    internal class LANBroadcast
    {
        private UdpClient _udpClient;
        private Thread _broadcastThread;
        private bool _isRunning;
        private const int DiscoveryPort = 8888;
        private const string DiscoveryMessage = "NetPackage_Discovery";
        private ServerInfo _serverInfo;
        private readonly object _serverInfoLock = new object();

        public void StartBroadcast()
        {
            if (_isRunning) return;

            try
            {
                _udpClient = new UdpClient();
                _udpClient.EnableBroadcast = true;
                _isRunning = true;
                _broadcastThread = new Thread(BroadcastLoop)
                {
                    IsBackground = true
                };
                _broadcastThread.Start();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start LAN broadcast: {e.Message}");
            }
        }

        public void StopBroadcast()
        {
            _isRunning = false;
            _udpClient?.Close();
            _broadcastThread?.Join();
        }
        public void SetServerInfo(ServerInfo serverInfo)
        {
            lock (_serverInfoLock)
            {
                _serverInfo = serverInfo;
            }
        }

        private void BroadcastLoop()
        {
            while (_isRunning)
            {
                try
                {
                    ServerInfo infoToBroadcast = null;
                    lock (_serverInfoLock)
                    {
                        if (_serverInfo != null)
                            infoToBroadcast = _serverInfo;
                    }

                    if (infoToBroadcast != null)
                    {
                        var message = BuildBroadcastMessage(infoToBroadcast);
                        var data = Encoding.ASCII.GetBytes(message);
                        _udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
                    }
                    Thread.Sleep(1000); // Broadcast every second
                }
                catch (Exception e)
                {
                    if (_isRunning)
                    {
                        Debug.LogError($"Error in broadcast loop: {e.Message}");
                        _isRunning = false;
                        return;
                    }
                }
            }
        }

        private string BuildBroadcastMessage(ServerInfo info)
        {
            var message = new StringBuilder();
            message.Append(DiscoveryMessage);
            message.Append("|");
            message.Append(info.ServerName);
            message.Append("|");
            message.Append(info.CurrentPlayers);
            message.Append("|");
            message.Append(info.MaxPlayers);
            message.Append("|");
            message.Append(info.GameMode);
            message.Append("|");
            message.Append(info.Address);
            message.Append("|");
            message.Append(info.Port);

            // Add custom data
            if (info.CustomData != null)
            {
                foreach (var kvp in info.CustomData)
                {
                    message.Append("|");
                    message.Append(kvp.Key);
                    message.Append("|");
                    message.Append(kvp.Value);
                }
            }

            return message.ToString();
        }
    }
}
