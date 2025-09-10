using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleNet.Transport.UDP;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleNet.Transport.Tests
{
    public class UDPHostTest : TransportTestBase
    {
        private List<int> _connectedClients;
        
        private const int Port = 7777;
        private const string TestMessage = "Hello, Server!";
        
        protected override IEnumerator SetUp()
        {
            StartHost();
            _connectedClients = new List<int>();
            yield return new WaitForSeconds(0.2f);
        }

        protected override IEnumerator Teardown()
        {
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestServerUp()
        {
            Assert.IsNotNull(_server, "Server instance is null.");
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator TestMultipleClients()
        {
            List<ITransport> clients = new List<ITransport>();
            for (int i = 0; i < 5; i++)
            {
                ITransport client = new UDPSolution();
                client.Setup(Port, false);
                client.Start();
                client.Connect("localhost");
                clients.Add(client);
            }
            yield return new WaitForSeconds(0.2f);
            
            Assert.IsTrue(_connectedClients.Count == 5, "There should be 5 clients.");
            
            foreach (var client in clients)
            {
                client.Stop();
            }
        }
        
        [UnityTest]
        public IEnumerator TestMessageServerToClient()
        {
            List<ITransport> clients = new List<ITransport>();
            for (int i = 0; i < 5; i++)
            {
                ITransport client = new UDPSolution();
                client.Setup(Port, false);
                client.Start();
                client.Connect("localhost");
                clients.Add(client);
            }
            yield return new WaitForSeconds(0.2f);
            for (int i = 0; i < 5; i++)
            {
                _server.SendTo(_connectedClients[i], System.Text.Encoding.ASCII.GetBytes(TestMessage));
                yield return new WaitForSeconds(0.2f);
                string receivedMessage = System.Text.Encoding.ASCII.GetString(clients[i].Receive());
                Assert.AreEqual(TestMessage, receivedMessage, "Message did not match.");
            }
            
            foreach (var client in clients)
            {
                client.Stop();
            }
        }
        
        [UnityTest]
        public IEnumerator TestMessageServerToAllClient()
        {
            List<ITransport> clients = new List<ITransport>();
            for (int i = 0; i < 5; i++)
            {
                ITransport client = new UDPSolution();
                client.Setup(Port, false);
                client.Start();
                client.Connect("localhost");
                clients.Add(client);
            }
            yield return new WaitForSeconds(0.2f);
            
            _server.Send(System.Text.Encoding.ASCII.GetBytes(TestMessage));
        
            yield return new WaitForSeconds(0.2f);

            int count = 0;
            for (int i = 0; i < 5; i++)
            {
                count += System.Text.Encoding.ASCII.GetString(clients[i].Receive()) != "" ? 1 : 0;
            }
            
            Assert.AreEqual(count, 5, "Messages dropped: "+(5-count)+".");
            
            foreach (var client in clients)
            {
                client.Stop();
            }
        }

        [UnityTest]
        public IEnumerator TestKickClient()
        {  
            List<ITransport> clients = new List<ITransport>();
            for (int i = 0; i < 5; i++)
            {
                ITransport client = new UDPSolution();
                client.Setup(Port, false);
                client.Start();
                client.Connect("localhost");
                clients.Add(client);
                yield return new WaitForSeconds(0.2f);
            }
            _server.Kick(2);
            yield return new WaitForSeconds(2f);
            
            Assert.IsTrue(_connectedClients.Count == 4, "There should be 4 clients.");
            foreach (var client in clients)
            {
                client.Stop();
            }
        }
        
        protected override void OnClientConnected(int id)
        {
            if (_connectedClients.Contains(id))
            {
                return;
            }
            _connectedClients.Add(id);
        }
        protected override void OnClientDisconnected(int id)
        {
            if (!_connectedClients.Contains(id))
            {
                return;
            }
            _connectedClients.Remove(id);
        }
    }
}
