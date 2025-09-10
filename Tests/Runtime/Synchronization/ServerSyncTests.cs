using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Serializer;
using SimpleNet.Synchronization;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using SimpleNet.Utilities;

namespace SimpleNet.Synchronization.Tests
{
    public class ServerSyncTests : SynchronizationTestBase
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
        public IEnumerator SendSingleUpdate()
        {
            yield return WaitConnection();
            
            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 0, "Object not spawned");
            yield return WaitSpawnSync(objs);

            TestObj testComponent = objs[0];
            testComponent.Set(42, 100, "test");
            StateManager.SendUpdateStates();
            
            yield return WaitMessage(typeof(SyncMessage));

            SyncMessage syncMsg = (SyncMessage)received;
            Assert.AreEqual(testComponent.NetObject.NetId, syncMsg.ObjectID, "Wrong object ID in sync message");
            Assert.Greater(syncMsg.changedValues.Count, 0, "No state changes in sync message");
        }

        [UnityTest]
        public IEnumerator SendMultipleUpdate()
        {
            yield return WaitConnection();
            
            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 0, "Object not spawned");
            yield return WaitSpawnSync(objs);
            
            var comp1 = objs[0];
            var comp2 = objs[1];
            
            yield return new WaitForSeconds(0.2f);
            
            comp1.Set(100, 1000, "first_changed");
            comp2.Set(200, 2000, "second_changed");
            
            StateManager.SendUpdateStates();

            yield return WaitMessage(typeof(SyncMessage));
            SyncMessage syncMsg = (SyncMessage)received;
            Assert.Greater(syncMsg.changedValues.Count, 0, "No state changes in sync message");

            yield return WaitMessage(typeof(SyncMessage));
            syncMsg = (SyncMessage)received;
            Assert.Greater(syncMsg.changedValues.Count, 0, "No state changes in sync message for second component");
            
            Assert.AreNotEqual(comp1.GetComponent<SceneObjectId>().SceneId, comp2.GetComponent<SceneObjectId>().SceneId, $"Wrong scene ID in sync message {comp1.GetComponent<SceneObjectId>().SceneId}");
        }

        [UnityTest]
        public IEnumerator SendMultipleCompUpdate()
        {
            TEST_SCENE_NAME = "TestScene3";
            yield return WaitConnection();
            
            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 3, "Object not spawned: " + objs.Length);
            yield return WaitSpawnSync(objs);
            
            var comp1 = objs[0];
            var comp2 = objs[1];
            var comp3 = objs[2];
            var comp4 = objs[3];
            if (comp1.gameObject.name != comp2.gameObject.name)
            {
                if (comp1.gameObject.name == comp3.gameObject.name)
                {
                    (comp2, comp3) = (comp3, comp2);
                }
                else if (comp1.gameObject.name == comp4.gameObject.name)
                {
                    (comp2, comp4) = (comp4, comp2);
                }
            }
            Assert.AreEqual(comp1.GetComponent<SceneObjectId>().SceneId, comp2.GetComponent<SceneObjectId>().SceneId, $"Wrong scene ID in sync message {comp1.GetComponent<SceneObjectId>().SceneId}");
            Assert.AreEqual(comp3.GetComponent<SceneObjectId>().SceneId, comp4.GetComponent<SceneObjectId>().SceneId, $"Wrong scene ID in sync message {comp3.GetComponent<SceneObjectId>().SceneId}");

            yield return new WaitForSeconds(0.2f);
            
            comp1.Set(100, 1000, "first_changed");
            comp2.Set(200, 1000, "second_changed");
            
            StateManager.SendUpdateStates();

            yield return WaitMessage(typeof(SyncMessage));
            SyncMessage syncMsg = (SyncMessage)received;
            Assert.Greater(syncMsg.changedValues.Count, 0, "No state changes in sync message");

            yield return WaitMessage(typeof(SyncMessage));
            syncMsg = (SyncMessage)received;
            Assert.Greater(syncMsg.changedValues.Count, 0, "No state changes in sync message for second component");
            
            Assert.AreEqual(comp1.health, comp2.health, $"Wrong health sync message {comp1.health}");
            Assert.AreNotEqual(comp1.id, comp2.id, $"Wrong id sync message {comp1.id}");
        }
        [UnityTest]
        public IEnumerator ReceiveMultipleComponentUpdate()
        {
            TEST_SCENE_NAME = "TestScene3";
            yield return WaitConnection();

            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 3, "Object not spawned");
            yield return WaitSpawnSync(objs);
            var comp1 = objs[0];
            var comp2 = objs[1];
            var comp3 = objs[2];
            var comp4 = objs[3];
            if (comp1.gameObject.name != comp2.gameObject.name)
            {
                if (comp1.gameObject.name == comp3.gameObject.name)
                {
                    (comp2, comp3) = (comp3, comp2);
                }
                else if (comp1.gameObject.name == comp4.gameObject.name)
                {
                    (comp2, comp4) = (comp4, comp2);
                }
            }
            Assert.AreEqual(comp1.GetComponent<SceneObjectId>().SceneId, comp2.GetComponent<SceneObjectId>().SceneId, $"Wrong scene ID in sync message {comp1.GetComponent<SceneObjectId>().SceneId}");
            Assert.AreEqual(comp3.GetComponent<SceneObjectId>().SceneId, comp4.GetComponent<SceneObjectId>().SceneId, $"Wrong scene ID in sync message {comp3.GetComponent<SceneObjectId>().SceneId}");
            Assert.AreNotEqual(comp1.GetComponent<SceneObjectId>().SceneId, comp3.GetComponent<SceneObjectId>().SceneId, $"Wrong scene ID in sync message {comp3.GetComponent<SceneObjectId>().SceneId}");

            yield return new WaitForSeconds(0.2f);
            
            Dictionary<string, object> changes1 = new Dictionary<string, object> { { "health", 150 }, { "msg", "changed1" } };
            Dictionary<string, object> changes2 = new Dictionary<string, object> { { "health", 250 }, { "msg", "changed2" } };
            
            NetMessage syncMsg = new SyncMessage(CLIENT_ID, comp1.NetObject.NetId, 0, changes1);
            NetMessage syncMsg1 = new SyncMessage(CLIENT_ID, comp2.NetObject.NetId, 1, changes2);
            _client.Send(NetSerializer.Serialize(syncMsg));
            _client.Send(NetSerializer.Serialize(syncMsg1));

            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(150 == comp1.health || 150 == comp2.health, "Not applied");
            Assert.IsTrue(250 == comp1.health || 250 == comp2.health, "Not applied");
            Assert.IsTrue("changed1" == comp1.msg || "changed1" == comp2.msg, "Not updated");
            Assert.IsTrue("changed2" == comp1.msg || "changed2" == comp2.msg, "Not updated");
        }
        [UnityTest]
        public IEnumerator ReceiveSingleUpdate()
        {
            yield return WaitConnection();

            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 0, "Object not spawned");
            yield return WaitSpawnSync(objs);
            TestObj testComponent = objs[0];
            int initialHealth = testComponent.health;

            Dictionary<string, object> changes = new Dictionary<string, object> { { "health", 50 } };
            NetMessage syncMsg = new SyncMessage(CLIENT_ID, testComponent.NetObject.NetId, 0, changes);
            _client.Send(NetSerializer.Serialize(syncMsg));

            float startTime = Time.time;
            while (testComponent.health == initialHealth && Time.time - startTime < 1f)
            {
                yield return null;
            }

            Assert.AreEqual(50, testComponent.health, "State update not applied");
            Assert.AreNotEqual(initialHealth, testComponent.health, "Health value unchanged");
        }

        [UnityTest]
        public IEnumerator ReceiveMultipleUpdate()
        {
            yield return WaitConnection();

            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 0, "Object not spawned");
            yield return WaitSpawnSync(objs);
            var comp1 = objs[0];
            var comp2 = objs[1];
            yield return new WaitForSeconds(0.2f);
            
            Dictionary<string, object> changes1 = new Dictionary<string, object> { { "health", 150 }, { "msg", "changed1" } };
            Dictionary<string, object> changes2 = new Dictionary<string, object> { { "health", 250 }, { "msg", "changed2" } };
            
            NetMessage syncMsg = new SyncMessage(CLIENT_ID, comp1.NetObject.NetId, 0, changes1);
            NetMessage syncMsg1 = new SyncMessage(CLIENT_ID, comp2.NetObject.NetId, 0, changes2);
            _client.Send(NetSerializer.Serialize(syncMsg));
            _client.Send(NetSerializer.Serialize(syncMsg1));

            float startTime = Time.time;
            while ((comp1.health != 150 || comp2.health != 250) && Time.time - startTime < 1f)
            {
                yield return null;
            }

            Assert.AreEqual(150, comp1.health, "First component update not applied");
            Assert.AreEqual("changed1", comp1.msg, "First component message not updated");
            Assert.AreEqual(250, comp2.health, "Second component update not applied");
            Assert.AreEqual("changed2", comp2.msg, "Second component message not updated");
        }

        [UnityTest]
        public IEnumerator OwnershipChangeTest()
        {
            yield return WaitConnection();

            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 0, "Object not spawned");
            yield return WaitSpawnSync(objs);
            var testComponent = objs[0];

            testComponent.NetObject.GiveOwner(CLIENT_ID);
            yield return WaitMessage(typeof(OwnershipMessage));


            OwnershipMessage ownerMsg = (OwnershipMessage)received;
            Assert.AreEqual(CLIENT_ID, ownerMsg.newOwnerId, "Wrong owner ID in message");
            Assert.AreEqual(testComponent.NetObject.NetId, ownerMsg.netObjectId, "Wrong object ID in ownership message");
        }

        [UnityTest]
        public IEnumerator StateRecoveryAfterDisconnect()
        {
            yield return WaitConnection();

            var objs = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(objs.Length, 0, "Object not spawned");
            yield return WaitSpawnSync(objs);
            var testComponent = objs[0];
            testComponent.Set(999, 888, "test_before_disconnect");

            _client.Stop();
            yield return new WaitForSeconds(0.5f);

            _client = new UDPSolution();
            _client.Setup(NetManager.Port, false);
            _client.Start();
            yield return new WaitForSeconds(0.2f);
            _client.Connect("localhost");
            yield return new WaitForSeconds(0.2f);


            Assert.AreEqual(888, testComponent.health, "Health not recovered after reconnect");
            Assert.AreEqual(999, testComponent.id, "Id not recovered after reconnect");
            Assert.AreEqual("test_before_disconnect", testComponent.msg, "Message not recovered after reconnect");
        }
    }
} 