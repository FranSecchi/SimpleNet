using UnityEngine.TestTools;
using System.Collections;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using UnityEngine;

namespace SimpleNet.Network.Tests
{
    public abstract class NetworkTestBase
    {
        private int _port;
        private NetManager obj;
        protected ITransport client;
        protected ITransport host;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            obj = new GameObject().AddComponent<NetManager>();
            yield return SetUp();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NetManager.StopNet();
            client?.Stop();
            host?.Stop();
            GameObject.DestroyImmediate(obj.gameObject);
            yield return Teardown();
        }

        protected abstract IEnumerator SetUp();
        protected abstract IEnumerator Teardown();
        protected void StartClient(bool manager)
        {
            if(manager) NetManager.StartClient();
            else
            {
                client = new UDPSolution();
                client.Setup(NetManager.Port, false);
                client.Start();
            }
        }

        protected void StartHost(bool manager)
        {
            if(manager) NetManager.StartHost();
            else
            {
                host = new UDPSolution();
                host.Setup(NetManager.Port, true,  new ServerInfo(){Address = "127.0.0.1", Port = NetManager.Port, MaxPlayers = 10});
                host.Start();
            }
        }
    }
} 