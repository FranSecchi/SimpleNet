using System.Collections;
using System.Collections.Generic;
using SimpleNet.Messages;
using NUnit.Framework;
using SimpleNet.Serializer;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleNet.Network.Tests
{
    public class ClientManagerTests : NetworkTestBase
    {
        private List<int> clientIds = new List<int>();

        protected override IEnumerator SetUp()
        {
            StartClient(true);
            StartHost(false);
            clientIds.Add(-1);
            yield return null;
        }

        protected override IEnumerator Teardown()
        {
            clientIds.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestStartClient()
        {
            yield return new WaitForSeconds(0.5f);

            NetManager.StartClient();
            yield return WaitClientConnection(0);

            Assert.IsTrue(NetManager.AllPlayers.Count == 2, "Client did not start correctly");
        }


        [UnityTest]
        public IEnumerator TestStopClient()
        {
            yield return new WaitForSeconds(0.5f);

            NetManager.StartClient();
            yield return new WaitForSeconds(0.5f);

            NetManager.StopNet();
            yield return new WaitForSeconds(0.5f);
            
            Assert.IsTrue(NetManager.AllPlayers.Count == 0, "Client did not stop correctly");
        }
        
        [UnityTest]
        public IEnumerator TestMultipleClients()
        {
            yield return new WaitForSeconds(0.5f);
            NetManager.StartClient();
            yield return new WaitForSeconds(0.5f);
            List<ITransport> clients = new List<ITransport>();
            yield return WaitClientConnection(0);
            for (int i = 1; i < 4; i++)
            {
                ITransport client = new UDPSolution();
                client.Setup(NetManager.Port, false);
                client.Start();
                clients.Add(client);
                yield return new WaitForSeconds(0.2f);
                client.Connect("localhost");
                
                yield return WaitClientConnection(i);
            }
            
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(NetManager.AllPlayers.Count, 5, "Client did not add 5 players");
            Assert.IsTrue(NetManager.AllPlayers.Contains(3), "Client did not add correctly");
            foreach (ITransport client in clients)
            {
                client.Stop();
            }
        }

        [UnityTest]
        public IEnumerator TestGetServerInfo()
        {
            yield return new WaitForSeconds(0.5f);

            NetManager.StartClient();
            yield return new WaitForSeconds(0.5f);
            yield return WaitClientConnection(0);
            
            var clientInfo = NetManager.GetServerInfo();
            Assert.IsNotNull(clientInfo.Address, "Client end should not be null after connecting");
            Assert.AreEqual(clientInfo.Address, "127.0.0.1", $"EndPoint not matching {clientInfo.Address} | \"127.0.0.1\"");
            Assert.AreEqual(clientInfo.MaxPlayers, 10, $"Max players not matching {clientInfo.MaxPlayers} | {10}");
            Assert.AreEqual(clientInfo.Port, NetManager.Port, $"Server name not matching {clientInfo.ServerName} | {NetManager.Port}");
        }

        [UnityTest]
        public IEnumerator TestGetConnectionInfo()
        {
            yield return new WaitForSeconds(0.5f);

            NetManager.StartClient();
            yield return new WaitForSeconds(0.5f);
            yield return WaitClientConnection(0);
            
            var connectionInfo = NetManager.GetConnectionInfo();
            Assert.IsNotNull(connectionInfo, "Connection info should not be null after connecting");
            Assert.IsTrue(connectionInfo.Id == 0, "Connection info should have valid connection ID");
            Assert.NotNull(connectionInfo.Ping, "Connection info should have valid connection PING");
        }

        [UnityTest]
        public IEnumerator TestGetConnectionState()
        {
            yield return new WaitForSeconds(0.5f);

            NetManager.StartClient();
            yield return new WaitForSeconds(0.5f);
            yield return WaitClientConnection(0);
            
            var connectionState = NetManager.GetConnectionState();
            Assert.IsNotNull(connectionState, "Connection state should not be null after connecting");
            Assert.AreEqual(ConnectionState.Connected, connectionState, "Connection state should be Connected");
        }

        private IEnumerator WaitClientConnection(int i)
        {
            clientIds.Add(i);
            yield return new WaitForSeconds(0.5f);
            NetMessage rcv = new ConnMessage(i, clientIds, new ServerInfo(){Address = "127.0.0.1", Port = NetManager.Port, MaxPlayers = 10});
            host.Send(NetSerializer.Serialize(rcv));
            yield return new WaitForSeconds(0.5f);
        }
    }
}
