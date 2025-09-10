using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleNet.Transport.UDP;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleNet.Transport.Tests
{
    public class UDPLanTest : TransportTestBase
    {
        private const int Port = 7777;
        private List<ITransport> _servers;
        
        protected override IEnumerator SetUp()
        {
            _servers = new List<ITransport>();
            yield return SetupServersAndClient();
            yield return new WaitForSeconds(2f);
        }

        protected override IEnumerator Teardown()
        {
            if (_client != null)
            {
                _client.StopServerDiscovery();
                _client.Stop();
            }
            
            if (_servers != null)
            {
                foreach (var server in _servers)
                {
                    server.StopServerBroadcast();
                    server.Stop();
                }
                _servers.Clear();
            }
            yield return null;
        }


        private IEnumerator SetupServersAndClient()
        {
            for (int i = 0; i < 3; i++)
            {
                ITransport server = new UDPSolution();
                server.Setup(Port + i, true);
                server.Start();
                server.SetServerInfo(new ServerInfo(){ServerName = "Name"});
                server.BroadcastServerInfo();
                _servers.Add(server);
                yield return new WaitForSeconds(1f);
            }
            StartClient();
            _client.StartServerDiscovery(0.1f);
            yield return new WaitForSeconds(2f);
        }

        [Test]
        public void TestDiscoverServer()
        {
            Assert.IsNotEmpty(_client.GetDiscoveredServers(), "GetDiscoveredServers failed");
        }

        [Test] 
        public void TestDiscoverMultipleServers()
        {
            List<ServerInfo> discoveredServers = _client.GetDiscoveredServers();

            Assert.GreaterOrEqual(discoveredServers.Count, 2, "Expected multiple servers, but found less.");
            Assert.AreEqual(discoveredServers.Count, new HashSet<ServerInfo>(discoveredServers).Count, "Duplicate servers detected.");
        }

        [UnityTest]
        public IEnumerator TestServerTimeout()
        {
            List<ServerInfo> initialServers = _client.GetDiscoveredServers();
            Assert.IsNotEmpty(initialServers, "No servers discovered initially");
            
            var serverToStop = _servers[0];
            serverToStop.StopServerBroadcast();
            serverToStop.Stop();
            _servers.RemoveAt(0);
            
            yield return new WaitForSeconds(6f);
            
            List<ServerInfo> remainingServers = _client.GetDiscoveredServers();
            
            Assert.Less(remainingServers.Count, initialServers.Count, "Server was not removed after timeout");
            Assert.AreEqual(initialServers.Count - 1, remainingServers.Count, "Expected exactly one server to be removed");
            
            foreach (var server in remainingServers)
            {
                Assert.AreNotEqual(serverToStop.GetServerInfo(), server, 
                    "Stopped server is still in the discovered servers list");
            }
        }

        [UnityTest]
        public IEnumerator TestServerInfoUpdate()
        {
            List<ServerInfo> initialServers = _client.GetDiscoveredServers();
            Assert.IsNotEmpty(initialServers, "No servers discovered initially");
            
            var server = _servers[0];
            var initialServerInfo = server.GetServerInfo();
            
            var newServerInfo = new ServerInfo
            {
                Address = initialServerInfo.Address,
                Port = initialServerInfo.Port,
                ServerName = "Updated Server Name",
                CurrentPlayers = 5,
                MaxPlayers = 10,
                Ping = initialServerInfo.Ping,
                GameMode = "New Game Mode",
                CustomData = new Dictionary<string, string> { { "key", "value" } }
            };
            server.SetServerInfo(newServerInfo);
            yield return new WaitForSeconds(5f);
            
            List<ServerInfo> updatedServers = _client.GetDiscoveredServers();
            
            var updatedServerInfo = updatedServers.Find(s => s.Equals(initialServerInfo));
            Assert.IsNotNull(updatedServerInfo, "Server not found in updated list");
            
            Assert.AreEqual(newServerInfo.ServerName, updatedServerInfo.ServerName, "Server name was not updated");
            Assert.AreEqual(newServerInfo.CurrentPlayers, updatedServerInfo.CurrentPlayers, "Current players was not updated");
            Assert.AreEqual(newServerInfo.MaxPlayers, updatedServerInfo.MaxPlayers, "Max players was not updated");
            Assert.AreEqual(newServerInfo.GameMode, updatedServerInfo.GameMode, "Game mode was not updated");
            Assert.IsTrue(updatedServerInfo.CustomData.ContainsKey("key"), "Custom data was not updated");
            Assert.AreEqual("value", updatedServerInfo.CustomData["key"], "Custom data value was not updated");
        }
    }
}
