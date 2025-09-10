using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Serializer;

namespace SimpleNet.Synchronization.Tests
{
    public class NetSpawnTests : SynchronizationTestBase
    {
        protected override IEnumerator SetUp()
        {
            StartHost(true);
            yield return new WaitForSeconds(0.2f);
            
            StartClient(false);
            yield return new WaitForSeconds(0.2f);
        }

        protected override IEnumerator Teardown()
        {
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpawnSynchronizationTest()
        {
            yield return WaitConnection(); 
            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            yield return WaitSpawnSync(objs);
            
            Vector3 spawnPos = new Vector3(1, 2, 3);
            SpawnMessage hostSpawnMsg = new SpawnMessage(NetManager.ConnectionId(), "TestObj", spawnPos);
            NetScene.Spawn(hostSpawnMsg);
            
            yield return new WaitForSeconds(0.2f);
            
            byte[] data = _client.Receive();
            Assert.NotNull(data, "Client did not receive spawn message");
            
            NetMessage receivedMsg = NetSerializer.Deserialize<NetMessage>(data);
            Assert.IsTrue(receivedMsg is SpawnMessage, "Wrong message type received");
            
            SpawnMessage spawnMsg = (SpawnMessage)receivedMsg;
            Assert.AreEqual(spawnPos, spawnMsg.position, "Wrong spawn position");
            
            objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 2, "Object not spawned");
            
            bool found = false;
            foreach (var obj in objs)
            {
                if (Vector3.Distance(obj.transform.position, spawnPos) < 0.01f)
                {
                    found = true;
                    Assert.NotNull(obj.NetObject, "NetObject not assigned");
                    
                    ObjectState state = StateManager.GetState(obj.NetObject.NetId);
                    Assert.NotNull(state, "Object state not registered");
                    break;
                }
            }
            Assert.IsTrue(found, "Spawned object not found at correct position");
        }

        [UnityTest]
        public IEnumerator ClientSpawnRequestTest()
        {
            yield return WaitConnection(); 
            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            yield return WaitSpawnSync(objs);
            
            int initialCount = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            
            Vector3 spawnPos = new Vector3(4, 5, 6);
            NetMessage clientSpawnMsg = new SpawnMessage(CLIENT_ID, "TestObj", spawnPos, owner: CLIENT_ID);
            _client.Send(NetSerializer.Serialize(clientSpawnMsg));
            yield return new WaitForSeconds(0.5f);
            
            objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(initialCount + 1, objs.Length, "Object not spawned");

            yield return WaitMessage(typeof(SpawnMessage));
            
            bool found = false;
            foreach (var obj in objs)
            {
                if (Vector3.Distance(obj.transform.position, spawnPos) < 0.01f)
                {
                    found = true;
                    Assert.NotNull(obj.NetObject, "NetObject not assigned");
                    Assert.IsFalse(obj.IsOwned, "Object not owned");
                    Assert.IsFalse(obj.NetObject.Owned, "Object not owned");
                    Assert.AreEqual(CLIENT_ID, obj.NetObject.OwnerId, "Wrong owner assigned");
                    
                    ObjectState state = StateManager.GetState(obj.NetObject.NetId);
                    Assert.NotNull(state, "Object state not registered");
                }
                else
                {
                    Assert.IsTrue(obj.IsOwned, "Object not owned");
                    Assert.IsTrue(obj.NetObject.Owned, "Object not owned");
                    Assert.AreEqual(NetManager.ConnectionId(), obj.NetObject.OwnerId, "Object not owned");
                }
            }
            Assert.IsTrue(found, "Spawned object not found at correct position");
        }

        [UnityTest]
        public IEnumerator SceneObjectSpawnTest()
        {
            yield return WaitConnection();           
            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            yield return WaitSpawnSync(objs);
            
            objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var obj in objs)
            {
                Assert.IsNotEmpty(obj.GetComponent<SceneObjectId>().SceneId, "Scene ID not set");
                ObjectState state = StateManager.GetState(obj.NetObject.NetId);
                Assert.NotNull(state, "Scene object state not registered");
            }
        }
    }
} 