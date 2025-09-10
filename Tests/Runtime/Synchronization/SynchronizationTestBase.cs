using System;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Serializer;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;

namespace SimpleNet.Synchronization.Tests
{
    public abstract class SynchronizationTestBase
    {
        private NetPrefabRegistry prefabs;
        protected ITransport _server;
        protected ITransport _client;
        protected NetMessage received;
        protected string TEST_SCENE_NAME = "TestScene";
        protected const int CLIENT_ID = 0;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            new GameObject().AddComponent<NetManager>();
            RegisterPrefab();
            yield return SetUp();

        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TEST_SCENE_NAME = "TestScene";
            NetManager.StopNet();
            StateManager.Clear();
            Messager.ClearHandlers();
            _client?.Stop();
            _server?.Stop();
            var objects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var obj in objects)
            {
                GameObject.DestroyImmediate(obj);
            }
            received = null;
            yield return Teardown();
            SceneManager.LoadScene("SampleScene");
            yield return new WaitForSeconds(0.2f);
        }

        protected abstract IEnumerator SetUp();
        protected abstract IEnumerator Teardown();
        protected void StartClient(bool manager)
        {
            if(manager) NetManager.StartClient();
            else
            {
                _client = new UDPSolution();
                _client.Setup(NetManager.Port, false);
                _client.Start();
            }
        }

        protected void StartHost(bool manager)
        {
            if(manager) NetManager.StartHost();
            else
            {
                _server = new UDPSolution();
                _server.Setup(NetManager.Port, true,  new ServerInfo(){Address = "127.0.0.1", Port = NetManager.Port, MaxPlayers = 10});
                _server.Start();
                ITransport.OnClientConnected += OnClientConnected;
                ITransport.OnClientDisconnected += OnClientDisconnected;
            }
        }
        protected virtual void OnClientConnected(int id){}
        protected virtual void OnClientDisconnected(int id){}
        private void RegisterPrefab()
        {
            var prefab = Resources.Load<GameObject>("TestObj");
            prefabs = ScriptableObject.CreateInstance<NetPrefabRegistry>();
            prefabs.prefabs.Add(prefab);
            NetScene.RegisterPrefabs(prefabs.prefabs);
        }
        protected IEnumerator WaitMessage(Type expectedType, ITransport client = null)
        {
            if(client == null) client = _client;
            byte[] data = null;
            float startTime = Time.time;
            NetMessage msg = null;
            while (Time.time - startTime < 1f)
            {
                data = client.Receive();
                if (data != null)
                {
                    msg = NetSerializer.Deserialize<NetMessage>(data);
                    if (msg.GetType() == expectedType)
                    {
                        received = msg;
                        yield break;
                    }
                }
                yield return null;
            }
            
            Assert.IsTrue(msg != null && msg.GetType() == expectedType, 
                $"Expected message of type {expectedType.Name} but got {(msg == null ? "null" : msg.GetType().Name)}");
        }

        protected virtual IEnumerator WaitConnection()
        {
            yield return new WaitForSeconds(0.2f);
            _client.Connect("localhost");
            yield return new WaitForSeconds(0.2f);
            NetManager.LoadScene(TEST_SCENE_NAME);
            yield return WaitMessage(typeof(SceneLoadMessage), _client);
            
            SceneLoadMessage scnMsg = (SceneLoadMessage)received;
            Assert.AreEqual(TEST_SCENE_NAME, scnMsg.sceneName, "Wrong scene name");
            scnMsg.isLoaded = true; scnMsg.requesterId = CLIENT_ID;
            NetMessage answerMsg = scnMsg;
            _client.Send(NetSerializer.Serialize(answerMsg));
            yield return new WaitForSeconds(0.5f);
        }
        protected IEnumerator WaitSpawnSync(NetBehaviour[] objs, bool client = true)
        {
            if(client)
            {
                List<int> ids = new List<int>();
                foreach (NetBehaviour hostObj in objs)
                {
                    if(ids.Contains(hostObj.NetID)) continue;
                    yield return WaitMessage(typeof(SpawnMessage));
                    SpawnValidationMessage spw = new SpawnValidationMessage(hostObj.NetID, CLIENT_ID);
                    received = spw;
                    _client.Send(NetSerializer.Serialize(received));
                    ids.Add(hostObj.NetID);
                    yield return new WaitForSeconds(0.2f);
                }

                for (int i = 0; i < ids.Count; i++)
                {
                    yield return WaitMessage(typeof(SpawnValidationMessage));
                    NetMessage spwMsg = (SpawnValidationMessage)received;
                    
                    _client.Send(NetSerializer.Serialize(spwMsg));
                }
            }
            else
            {
                int c = 0;
                foreach (NetBehaviour hostObj in objs)
                {
                    SpawnMessage msg = new SpawnMessage(-1, hostObj.gameObject.name, hostObj.transform.position, sceneId:hostObj.GetComponent<SceneObjectId>().SceneId);
                    msg.netObjectId = c;
                    received = msg;
                    _server.Send(NetSerializer.Serialize(received));
                    c++;
                }
                yield return new WaitForSeconds(0.2f);
                for (int i = 0; i < objs.Length; i++)
                {
                    yield return WaitMessage(typeof(SpawnValidationMessage), _server);
                    SpawnValidationMessage spwMsg = (SpawnValidationMessage)received;
                    spwMsg.requesterId = -1;
                    received = spwMsg;
                    _server.Send(NetSerializer.Serialize(received));
                }
            }
        }
    }
} 