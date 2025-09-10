using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Serializer;

namespace SimpleNet.Synchronization.Tests
{
    public class NetDestroyTests : SynchronizationTestBase
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
        public IEnumerator HostDestroySynchronizationTest()
        {
            yield return WaitConnection();
            
            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 0, "Object not spawned");

            TestObj spawnedObj = objs[0];

            yield return WaitSpawnSync(objs);

            NetManager.Destroy(spawnedObj.NetObject.NetId);
            yield return WaitMessage(typeof(DestroyMessage));
            DestroyMessage destroyMsg = (DestroyMessage)received;
            
            Assert.AreEqual(spawnedObj.NetObject.NetId, destroyMsg.netObjectId, "Wrong object ID in destroy message");
            
            objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, objs.Length, "Object not destroyed");
        }


        [UnityTest]
        public IEnumerator ClientDestroyRequestTest()
        {
            yield return WaitConnection();
            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 1, "Object not spawned");
            
            yield return WaitSpawnSync(objs);
            
            Vector3 spawnPos = new Vector3(4, 5, 6);
            NetMessage clientSpawnMsg = new SpawnMessage(CLIENT_ID, "TestObj", spawnPos, owner: CLIENT_ID);
            _client.Send(NetSerializer.Serialize(clientSpawnMsg));
            yield return new WaitForSeconds(0.5f);
            
            objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 2, "Object not spawned");
            
            TestObj spawnedObj = null;
            foreach (var obj in objs)
            {
                if (Vector3.Distance(obj.transform.position, spawnPos) < 0.01f)
                {
                    spawnedObj = obj;
                    break;
                }
            }
            Assert.NotNull(spawnedObj, "Spawned object not found");
            Assert.IsFalse(spawnedObj.IsOwned, "Object not owned");
            Assert.IsFalse(spawnedObj.NetObject.Owned, "Object not owned");
            Assert.AreEqual(CLIENT_ID, spawnedObj.NetObject.OwnerId, "Object not owned");
            
            NetMessage clientDestroyMsg = new DestroyMessage(spawnedObj.NetObject.NetId, CLIENT_ID);
            _client.Send(NetSerializer.Serialize(clientDestroyMsg));
            yield return new WaitForSeconds(0.5f);
            
            objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(2, objs.Length, "Object not destroyed");
        }

        [UnityTest]
        public IEnumerator UnauthorizedDestroyTest()
        {
            yield return WaitConnection();
            
            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 1, "Object not spawned");
            yield return WaitSpawnSync(objs);
            
            Vector3 spawnPos = new Vector3(1, 2, 3);
            SpawnMessage hostSpawnMsg = new SpawnMessage(NetManager.ConnectionId(), "TestObj", spawnPos);
            NetScene.Spawn(hostSpawnMsg);
            
            yield return WaitMessage(typeof(SpawnMessage));
            _client.Send(NetSerializer.Serialize(received));
            yield return new WaitForSeconds(0.2f);
            
            
            objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 2, "Object not spawned");
            TestObj spawnedObj = null;
            foreach (var obj in objs)
            {
                if (Vector3.Distance(obj.transform.position, spawnPos) < 0.01f)
                {
                    spawnedObj = obj;
                    break;
                }
            }
            Assert.NotNull(spawnedObj, "Spawned object not found");
            
            NetMessage clientDestroyMsg = new DestroyMessage(spawnedObj.NetObject.NetId, CLIENT_ID);
            _client.Send(NetSerializer.Serialize(clientDestroyMsg));
            yield return new WaitForSeconds(0.5f);
            
            objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 1, "Object was destroyed by unauthorized client");
        }
    }
} 