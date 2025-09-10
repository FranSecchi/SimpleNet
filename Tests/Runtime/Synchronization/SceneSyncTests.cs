using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Serializer;

namespace SimpleNet.Synchronization.Tests
{
    public class SceneSyncTests : SynchronizationTestBase
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
        public IEnumerator SceneSynchronizationTest()
        {
            yield return WaitConnection();
            
            NetManager.LoadScene(TEST_SCENE_NAME);
            yield return new WaitForSeconds(0.5f);

            var hostObjects = GameObject.FindObjectsByType<TestObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(hostObjects.Length, 0, "No NetBehaviour objects found in host scene");
            yield return WaitMessage(typeof(SceneLoadMessage));
            
            SceneLoadMessage scnMsg = (SceneLoadMessage)received;
            Assert.AreEqual(TEST_SCENE_NAME, scnMsg.sceneName, "Wrong scene name");
            scnMsg.isLoaded = true; 
            scnMsg.requesterId = CLIENT_ID;
            NetMessage answerMsg = scnMsg;
            _client.Send(NetSerializer.Serialize(answerMsg));
            yield return new WaitForSeconds(0.5f);
            
            yield return WaitSpawnSync(hostObjects);

            
            TestObj testComponent = hostObjects[0];
            testComponent.Set(42, 100, "test");
            StateManager.SendUpdateStates();
            
            yield return WaitMessage(typeof(SyncMessage));

            SyncMessage syncMsg = (SyncMessage)received;
            Assert.AreEqual(testComponent.NetObject.NetId, syncMsg.ObjectID, "Wrong object ID in sync message");
            Assert.Greater(syncMsg.changedValues.Count, 0, "No state changes in sync message");
        }

        protected override IEnumerator WaitConnection()
        {
            yield return new WaitForSeconds(0.2f);
            _client.Connect("localhost");
            yield return new WaitForSeconds(1f);
        }
    }
} 