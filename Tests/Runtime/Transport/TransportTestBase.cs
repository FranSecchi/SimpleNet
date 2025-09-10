using UnityEngine.TestTools;
using System.Collections;
using SimpleNet.Transport.UDP;

namespace SimpleNet.Transport.Tests
{
    public abstract class TransportTestBase
    {
        private const int Port = 7777;
        
        protected ITransport _server;
        protected ITransport _client;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return SetUp();
        }


        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _server?.Stop();
            _client?.Stop();
            ITransport.OnClientConnected -= OnClientConnected;
            ITransport.OnClientDisconnected -= OnClientDisconnected;
            yield return Teardown();
        }

        protected abstract IEnumerator SetUp();
        protected abstract IEnumerator Teardown();
        protected virtual void OnClientConnected(int id){}
        protected virtual void OnClientDisconnected(int id){}
        protected void StartClient()
        {
            _client = new UDPSolution();
            _client.Setup(Port, false);
            _client.Start();
        }

        protected void StartHost()
        {
            _server = new UDPSolution();
            _server.Setup(Port, true);
            _server.Start();
            ITransport.OnClientConnected += OnClientConnected;
            ITransport.OnClientDisconnected += OnClientDisconnected;
        }
    }
    
} 