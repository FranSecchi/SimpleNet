using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Serializer;
using SimpleNet.Transport;

namespace SimpleNet.Synchronization.Tests
{
    public class ClientSyncTests : SynchronizationTestBase
    {
        private List<int> clientIds = new List<int>();

        protected override IEnumerator SetUp()
        {
            StartHost(false);
            yield return new WaitForSeconds(0.2f);
            StartClient(true);
            yield return new WaitForSeconds(0.2f);
            
            yield return WaitConnection();
            NetMessage answerMsg =  new SceneLoadMessage(TEST_SCENE_NAME, -1);
            _server.Send(NetSerializer.Serialize(answerMsg));
            yield return new WaitForSeconds(0.5f);
            yield return WaitMessage(typeof(SceneLoadMessage), _server);
        }

        protected override IEnumerator Teardown()
        {
            clientIds.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReceiveStateUpdate()
        {
            var hostObjects = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(hostObjects.Length, 0, "No NetBehaviour objects found in host scene");
            yield return WaitSpawnSync(hostObjects, false);
            yield return new WaitForSeconds(0.2f);
            
            Dictionary<string, object> changes = new Dictionary<string, object> 
            { 
                { "health", 75 },
                { "id", 42 },
                { "msg", "updated" }
            };
            NetMessage spw = new SyncMessage(-1, hostObjects[0].NetID, 0, changes);
            _server.Send(NetSerializer.Serialize(spw));
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(75, hostObjects[0].health, "Health not updated correctly");
            Assert.AreEqual(42, hostObjects[0].id, "ID not updated correctly");
            Assert.AreEqual("updated", hostObjects[0].msg, "Message not updated correctly");
        }


        [UnityTest]
        public IEnumerator HandleOwnershipChange()
        {
            var hostObjects = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(hostObjects.Length, 0, "No NetBehaviour objects found in host scene");
            foreach (NetBehaviour hostObj in hostObjects)
            {
                NetMessage msg = new SpawnMessage(-1, hostObj.gameObject.name, hostObj.transform.position, sceneId:hostObj.GetComponent<SceneObjectId>().SceneId);
                _server.Send(NetSerializer.Serialize(msg));
                yield return new WaitForSeconds(0.2f);
            }
            
            foreach (var obj in hostObjects)
            {
                Assert.IsFalse(obj.IsOwned, "Object not owned");
                Assert.IsFalse(obj.NetObject.Owned, "Object not owned");
                Assert.AreEqual(-1, obj.NetObject.OwnerId, "Wrong owner assigned");
            }
            
            NetMessage ownerMsg = new OwnershipMessage(hostObjects[0].NetObject.NetId, CLIENT_ID);
            _server.Send(NetSerializer.Serialize(ownerMsg));

            float startTime = Time.time;
            while (hostObjects[0].NetObject.OwnerId != CLIENT_ID && Time.time - startTime < 1f)
            {
                yield return null;
            }
            var obj1 = hostObjects[0];
            Assert.IsTrue(obj1.IsOwned, "Ownership not updated correctly");
            Assert.IsTrue(obj1.NetObject.Owned, "Ownership not updated correctly");
            Assert.AreEqual(CLIENT_ID, obj1.NetObject.OwnerId, "Ownership not updated correctly");
        }

        protected override IEnumerator WaitConnection()
        {
            clientIds.Add(0);
            yield return new WaitForSeconds(0.5f);
            NetMessage rcv = new ConnMessage(0, clientIds, new ServerInfo(){Address = "127.0.0.1", Port = NetManager.Port, MaxPlayers = 10});
            _server.Send(NetSerializer.Serialize(rcv));
            yield return new WaitForSeconds(0.5f);
        }
    }
} 