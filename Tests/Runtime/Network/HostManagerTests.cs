using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleNet.Network.Tests
{
    public class HostManagerTests : NetworkTestBase
    {
        protected override IEnumerator SetUp()
        {
            StartHost(true);
            StartClient(false);
            yield return null;
        }

        protected override IEnumerator Teardown()
        {
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestStartServer()
        {
            yield return new WaitForSeconds(0.2f);
            client.Connect("127.0.0.1");
            yield return new WaitForSeconds(0.2f);
            
            Assert.IsTrue(NetManager.AllPlayers.Count == 2, "Server did not add one player");
        }
        [UnityTest]
        public IEnumerator TestStopServer()
        {
            yield return new WaitForSeconds(0.2f);
            client.Connect("127.0.0.1");
            yield return new WaitForSeconds(0.2f);

            NetManager.StopNet();
            yield return new WaitForSeconds(0.2f);
            
            Assert.IsTrue(NetManager.AllPlayers.Count == 0, "Server did not stop correctly");
        }
        [UnityTest]
        public IEnumerator TestKickPlayer()
        {
            yield return new WaitForSeconds(0.2f);
            client.Connect("127.0.0.1");
            yield return new WaitForSeconds(0.2f);
            int key = NetManager.AllPlayers[1];
            NetHost.Kick(key);
            yield return new WaitForSeconds(0.2f);
            
            Assert.IsTrue(NetManager.AllPlayers.Count == 1, "Server did not kick correctly " + NetManager.AllPlayers.Count);
        }
        [UnityTest]
        public IEnumerator TestMultipleClients()
        {
            yield return new WaitForSeconds(0.2f);
            client.Connect("localhost");
            yield return new WaitForSeconds(0.2f);
            List<ITransport> clients = new List<ITransport>();
            for (int i = 0; i < 3; i++)
            {
                ITransport client = new UDPSolution();
                clients.Add(client);
                client.Setup(NetManager.Port, false);
                client.Start();
                client.Connect("localhost");
                yield return new WaitForSeconds(0.2f);
            }
            
            
            Assert.AreEqual(5 ,NetManager.AllPlayers.Count, "Server did not add 5 players");
            Assert.IsTrue(NetManager.AllPlayers.Contains(3), "Server did not add correctly");

            foreach (ITransport client in clients)
            {
                client.Stop();
            }
        }
        [UnityTest]
        public IEnumerator TestGetServerInfo()
        {
            NetManager.SetServerName("Net_Server");
            yield return new WaitForSeconds(0.2f);
            
            var serverInfo = NetManager.GetServerInfo();
            Assert.IsNotNull(serverInfo, "Server info should not be null for host");
            Assert.AreEqual(NetManager.MaxPlayers, serverInfo.MaxPlayers, "Server info should match configured max players");
            Assert.AreEqual(NetManager.ServerName, serverInfo.ServerName, "Server info should match configured server name");
        }

        [UnityTest]
        public IEnumerator TestGetConnectionInfo()
        {
            yield return new WaitForSeconds(0.2f);
            client.Connect("127.0.0.1");
            yield return new WaitForSeconds(0.2f);
            
            var hostConnectionInfo = NetManager.GetConnectionInfo();
            Assert.IsNotNull(hostConnectionInfo, "Host connection info should not be null");
            
            var clientId = NetManager.AllPlayers[1];
            var clientConnectionInfo = NetManager.GetConnectionInfo(clientId);
            Assert.IsNotNull(clientConnectionInfo, "Client connection info should not be null");
            Assert.AreEqual(clientId, clientConnectionInfo.Id, "Connection info ID should match client ID");
        }

        [UnityTest]
        public IEnumerator TestGetConnectionState()
        {
            yield return new WaitForSeconds(0.2f);
            client.Connect("127.0.0.1");
            yield return new WaitForSeconds(0.5f);
            
            var hostConnectionState = NetManager.GetConnectionState();
            Assert.IsNotNull(hostConnectionState, "Host connection state should not be null");
            Assert.AreEqual(ConnectionState.Connected, hostConnectionState, "Host should be in Connected state");
            
            var clientId = NetManager.AllPlayers[1];
            var clientConnectionState = NetManager.GetConnectionState(clientId);
            Assert.IsNotNull(clientConnectionState, "Client connection state should not be null");
            Assert.AreEqual(ConnectionState.Connected, clientConnectionState, "Client should be in Connected state");
        }
    }
}
