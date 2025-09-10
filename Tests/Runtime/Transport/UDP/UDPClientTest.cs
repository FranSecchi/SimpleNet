using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleNet.Transport.Tests
{
    public class UDPClientTest : TransportTestBase
    {
        private const string TestMessage = "Hello, Server!";
        private bool _connected = false;
        
        protected override IEnumerator SetUp()
        {
            StartHost();
            yield return new WaitForSeconds(0.2f);
            StartClient();
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTest]
        public IEnumerator TestClientConnected()
        {
            _client.Connect("localhost");

            yield return new WaitForSeconds(0.2f);
            
            Assert.IsTrue(_connected, "Client did not connect.");
        }
        
        [UnityTest]
        public IEnumerator TestClientDisconnected()
        {
            _client.Connect("localhost");
            yield return new WaitForSeconds(0.2f);
            
            _client.Disconnect();
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(!_connected, "Client did not disconnect.");
        }
        
        [UnityTest]
        public IEnumerator TestMessageClientToServer()
        {
            _client.Connect("localhost");
            yield return new WaitForSeconds(0.2f);
        
            _client.Send(System.Text.Encoding.ASCII.GetBytes(TestMessage));
        
            yield return new WaitForSeconds(0.2f);

            string receivedMessage = System.Text.Encoding.ASCII.GetString(_server.Receive());
            Assert.AreEqual(TestMessage, receivedMessage, "Received message.");
        }
        
        protected override IEnumerator Teardown()
        {
            base.TearDown();
            yield return null;
        }
        
        protected override void OnClientConnected(int id)
        {
            _connected = true;
        }
        protected override void OnClientDisconnected(int id)
        {
            _connected = false;
        }
    }
}
