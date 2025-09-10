using System.Collections;
using System.Collections.Generic;
using MessagePack;
using NUnit.Framework;
using SimpleNet.Network;
using SimpleNet.Serializer;
using SimpleNet.Messages;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleNet.Messaging.Tests
{
    public class NetworkMessageTests
    {
        private NetManager _manager;
        private List<ITransport> _clients;
        private TestMsg received;
        [SetUp]
        public void Setup()
        {
            _manager = new GameObject().AddComponent<NetManager>();
            NetManager.SetTransport(new UDPSolution());
            _manager.address = "localhost";
            received = null;
        }
        
        [UnityTest]
        public IEnumerator Server_SendMsg()
        {
            yield return new WaitForSeconds(0.5f);
            StartHost();
            yield return new WaitForSeconds(0.5f);
            
            Messager.RegisterHandler<ConnMessage>(OnConnMsg);
            Messager.RegisterHandler<TestMsg>(OnReceived);
            yield return ConnectClients();
            yield return new WaitForSeconds(0.5f);
            
            foreach (ITransport client in _clients)
            {
                while (client.Receive() != null) { }
            }
            
            TestMsg testMsg = new TestMsg(34, "Hello World");
            
            NetManager.Send(testMsg);
            yield return new WaitForSeconds(0.5f);
            
            foreach (ITransport client in _clients)
            {
                byte[] data = null;
                float startTime = Time.time;
                while (data == null && Time.time - startTime < 1f)
                {
                    data = client.Receive();
                    if (data == null)
                    {
                        yield return null;
                    }
                }
                Assert.IsNotNull(data, "No message received from client");
                NetMessage msg = NetSerializer.Deserialize<NetMessage>(data);
                Messager.HandleMessage(msg);
                
                byte[] extraData = client.Receive();
                Assert.IsNull(extraData, "Extra message received from client");
            }
            
            Assert.IsNotNull(received, "Received message is null");
            Assert.IsTrue(testMsg.ObjectID == received.ObjectID, "Object ID is incorrect");
            Assert.IsTrue(testMsg.msg.Equals(received.msg), "Message content is incorrect");
        }
        [UnityTest]
        public IEnumerator Server_ReceiveMsg()
        {
            yield return new WaitForSeconds(0.5f);
            StartHost();
            yield return new WaitForSeconds(0.5f);
            
            Messager.RegisterHandler<ConnMessage>(OnConnMsg);
            Messager.RegisterHandler<TestMsg>(OnReceived);
            yield return ConnectClients();
            yield return new WaitForSeconds(0.5f);
            
            TestMsg testMsg = new TestMsg(34, "Hello World");
            byte[] data = NetSerializer.Serialize((NetMessage) testMsg);
            foreach (ITransport client in _clients)
            {
                client.Send(data);
            }
            yield return new WaitForSeconds(0.5f);
            
            Assert.IsNotNull(received, "Received message is null");
            Assert.IsTrue(testMsg.ObjectID == received.ObjectID, "Object ID is incorrect");
            Assert.IsTrue(testMsg.msg.Equals(received.msg), "Object ID is incorrect");
        }
        [UnityTest]
        public IEnumerator Client_SendMsg()
        {
            yield return new WaitForSeconds(0.5f);
            StartClient();
            yield return new WaitForSeconds(0.5f);
            
            TestMsg testMsg = new TestMsg(34, "Hello World");
            
            Messager.RegisterHandler<TestMsg>(OnReceived);
            NetManager.Send(testMsg);
            yield return new WaitForSeconds(0.5f);
            
            foreach (ITransport client in _clients)
            {
                byte[] data = client.Receive();
                Assert.IsNotNull(data, "Server - Received message is null");
                NetMessage msg = NetSerializer.Deserialize<NetMessage>(data);
                Messager.HandleMessage(msg);
            }
            
            Assert.IsNotNull(received, "Received message is null");
            Assert.IsTrue(testMsg.ObjectID == received.ObjectID, "Object ID is incorrect");
            Assert.IsTrue(testMsg.msg.Equals(received.msg), "Object ID is incorrect");
        }
        [UnityTest]
        public IEnumerator Client_ReceiveMsg()
        {
            yield return new WaitForSeconds(0.5f);
            StartClient();
            yield return new WaitForSeconds(0.5f);
            
            Messager.RegisterHandler<TestMsg>(OnReceived);
            
            TestMsg testMsg = new TestMsg(34, "Hello World");
            byte[] data = NetSerializer.Serialize((NetMessage) testMsg);
            foreach (ITransport client in _clients)
            {
                client.Send(data);
            }
            yield return new WaitForSeconds(0.5f);
            
            Assert.IsNotNull(received, "Received message is null");
            Assert.IsTrue(testMsg.ObjectID == received.ObjectID, "Object ID is incorrect");
            Assert.IsTrue(testMsg.msg.Equals(received.msg), "Object ID is incorrect");
        }
        
        [TearDown]
        public void TearDown()
        {
            if (_clients != null)
            {
                foreach (ITransport client in _clients)
                {
                    ITransport.OnClientConnected -= id => TransportOnOnClientConnected(id, client);
                    client.Stop();
                }
                _clients.Clear();
            }
            NetManager.StopNet();
            if(_manager != null) GameObject.DestroyImmediate(_manager.gameObject);
            Messager.ClearHandlers();
            received = null;
        }
        private void OnReceived(TestMsg obj)
        {
            received = obj;
        }
        
        private void StartClient()
        {
            Messager.RegisterHandler<ConnMessage>(OnConnMsg);
            _clients = new List<ITransport>();
            ITransport server = new UDPSolution();
            server.Setup(NetManager.Port, true);
            server.Start();
            ITransport.OnClientConnected += id => TransportOnOnClientConnected(id, server);
            _clients.Add(server);
            NetManager.StartClient();
        }

        private void TransportOnOnClientConnected(int id, ITransport server)
        {
            NetMessage msg = new ConnMessage(id, NetManager.AllPlayers, NetManager.GetServerInfo());
            server.Send(NetSerializer.Serialize(msg));
        }

        private void OnConnMsg(ConnMessage obj)
        {
            
        }
        private void StartHost()
        {
            NetManager.StartHost();
            _clients = new List<ITransport>();
            for (int i = 0; i < 5; i++)
            {
                ITransport client = new UDPSolution();
                client.Setup(NetManager.Port, false);
                client.Start();
                _clients.Add(client);
            }
        }
        private IEnumerator ConnectClients()
        {
            yield return new WaitForSeconds(0.5f);
            foreach (ITransport client in _clients)
            {
                client.Connect(_manager.address);
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    public class TestMsg : NetMessage
    {
        [Key(1)] public int ObjectID;
        [Key(2)] public string msg;

        public TestMsg(){}
        public TestMsg(int i, string msg, List<int> s = null) : base(s)
        {
            ObjectID = i;
            this.msg = msg;
        }

        public override string ToString()
        {
            return $"{base.ToString()} ObjectID:{ObjectID}, Message:\"{msg}\"";
        }
    }
}