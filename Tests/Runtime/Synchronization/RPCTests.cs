using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Serializer;

namespace SimpleNet.Synchronization.Tests
{
    public class RPCTests : SynchronizationTestBase
    {
        private TestRPCBehaviour testObj;


        protected override IEnumerator SetUp()
        {
            TEST_SCENE_NAME = "TestScene2";
            StartHost(true);
            
            yield return new WaitForSeconds(0.2f);
            StartClient(false);
            yield return WaitConnection();
            var testObjs = GameObject.FindObjectsByType<NetBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            testObj = (TestRPCBehaviour)testObjs[0];
            yield return WaitSpawnSync(testObjs);
            yield return new WaitForSeconds(0.5f);
        }

        protected override IEnumerator Teardown()
        {
            if (testObj != null && testObj.NetObject != null)
            {
                RPCManager.Unregister(testObj.NetObject.NetId, testObj);
            }
            testObj = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestBidirectionalRPC()
        {
            NetMessage msg = new RPCMessage(0, testObj.NetObject.NetId, "TestRPC", null, 42, "test");
            _client.Send(NetSerializer.Serialize(msg));
            
            yield return WaitMessage(typeof(RPCMessage));
            Assert.IsTrue(received is RPCMessage, "Client did not receive RPC message");
            Assert.AreEqual(42, testObj.lastReceivedValue, "Server did not receive correct value from client RPC");
            Assert.AreEqual("test", testObj.lastReceivedMessage, "Server did not receive correct message from client RPC");

            testObj.lastReceivedValue = 0;
            testObj.lastReceivedMessage = "";
            
            testObj.CallTestRPC(100, "server_test");
            
            yield return WaitMessage(typeof(RPCMessage));
            Assert.IsTrue(received is RPCMessage, "Client did not receive RPC message");
            
            
            RPCMessage rpcMsg = (RPCMessage)received;
            Assert.AreEqual(100, rpcMsg.Parameters[0], "Client did not receive correct value from server RPC " + rpcMsg);
            Assert.AreEqual("server_test", rpcMsg.Parameters[1], "Client did not receive correct message from server RPC");
        }

        [UnityTest]
        public IEnumerator TestServerToClientRPC()
        {
            testObj.serverToClientCalled = false;
            
            testObj.CallServerToClientRPC();
            
            yield return WaitMessage(typeof(RPCMessage));
            Assert.IsTrue(received is RPCMessage, "Client did not receive RPC message");
        }

        [UnityTest]
        public IEnumerator TestClientToServerRPC()
        {
            testObj.clientToServerCalled = false;
            
            NetMessage msg = new RPCMessage(0, testObj.NetObject.NetId, "ClientToServerRPC");
            _client.Send(NetSerializer.Serialize(msg));
            
            float startTime = Time.time;
            while (!testObj.clientToServerCalled && Time.time - startTime < 1f)
            {
                yield return null;
            }
            
            Assert.IsTrue(testObj.clientToServerCalled, "Client-to-server RPC was not called on server");
        }

        [UnityTest]
        public IEnumerator TestTargetModeAll()
        {
            testObj.targetModeAllCalled = false;
            testObj.targetModeAllCallCount = 0;
            
            testObj.CallTargetModeAllRPC();
            
            yield return WaitMessage(typeof(RPCMessage));
            Assert.IsTrue(received is RPCMessage, "Client did not receive RPC message");
            
            float startTime = Time.time;
            while (!testObj.targetModeAllCalled && Time.time - startTime < 1f)
            {
                yield return null;
            }
            
            Assert.IsTrue(testObj.targetModeAllCalled, "TargetMode.All RPC was not called on server");
            Assert.AreEqual(1, testObj.targetModeAllCallCount, "TargetMode.All RPC was not called exactly once");
        }

        [UnityTest]
        public IEnumerator TestTargetModeSpecific()
        {
            testObj.targetModeSpecificCalled = false;
            testObj.targetModeSpecificCallCount = 0;
            
            var targetList = new List<int> { 0 }; 
            testObj.CallTargetModeSpecificRPC(targetList);
            
            yield return WaitMessage(typeof(RPCMessage));
            Assert.IsTrue(received is RPCMessage, "Client did not receive RPC message");
            
            
            testObj.targetModeSpecificCalled = false;
            testObj.targetModeSpecificCallCount = 0;
            
            var serverTargetList = new List<int> { NetManager.ConnectionId() };
            NetMessage msg = new RPCMessage(0, testObj.NetObject.NetId, "TargetModeSpecificRPC", null, serverTargetList);
            _client.Send(NetSerializer.Serialize(msg));
            
            float startTime = Time.time;
            while (!testObj.targetModeSpecificCalled && Time.time - startTime < 1f)
            {
                yield return null;
            }
            
            Assert.IsTrue(testObj.targetModeSpecificCalled, "TargetMode.Specific RPC was not called on server");
            Assert.AreEqual(1, testObj.targetModeSpecificCallCount, "TargetMode.Specific RPC was not called exactly once");
        }

        [UnityTest]
        public IEnumerator TestTargetModeOthers()
        {
            testObj.targetModeOthersCalled = false;
            testObj.targetModeOthersCallCount = 0;
            
            testObj.CallTargetModeOthersRPC();
            
            yield return WaitMessage(typeof(RPCMessage));
            Assert.IsTrue(received is RPCMessage, "Client did not receive RPC message");
            
            float startTime = Time.time;
            while (!testObj.targetModeOthersCalled && Time.time - startTime < 1f)
            {
                yield return null;
            }
            
            Assert.IsFalse(testObj.targetModeOthersCalled, "TargetMode.Others RPC was called on server when it shouldn't be");
            Assert.AreEqual(0, testObj.targetModeOthersCallCount, "TargetMode.Others RPC was called on server when it shouldn't be");
            
            // Test client sending to server
            testObj.targetModeOthersCalled = false;
            testObj.targetModeOthersCallCount = 0;
            
            NetMessage msg = new RPCMessage(0, testObj.NetObject.NetId, "TargetModeOthersRPC");
            _client.Send(NetSerializer.Serialize(msg));
            
            startTime = Time.time;
            while (!testObj.targetModeOthersCalled && Time.time - startTime < 1f)
            {
                yield return null;
            }
            
            Assert.IsTrue(testObj.targetModeOthersCalled, "TargetMode.Others RPC was not called on server");
            Assert.AreEqual(1, testObj.targetModeOthersCallCount, "TargetMode.Others RPC was not called exactly once");
        }

        [UnityTest]
        public IEnumerator TestComplexDataRPC()
        {
            var complexData = new TestRPCBehaviour.ComplexData
            {
                Id = 42,
                Name = "TestObject",
                Tags = new List<string> { "tag1", "tag2", "tag3" },
                Stats = new Dictionary<string, int>
                {
                    { "health", 100 },
                    { "mana", 50 },
                    { "stamina", 75 }
                }
            };

            testObj.CallComplexDataRPC(complexData);
            
            yield return WaitMessage(typeof(RPCMessage));
            Assert.IsTrue(received is RPCMessage, "Client did not receive RPC message");
            
            float startTime = Time.time;
            while (testObj.lastReceivedComplexData == null && Time.time - startTime < 1f)
            {
                yield return null;
            }
            
            Assert.IsNotNull(testObj.lastReceivedComplexData, "Complex data was not received");
            Assert.AreEqual(complexData.Id, testObj.lastReceivedComplexData.Id, "Complex data Id mismatch");
            Assert.AreEqual(complexData.Name, testObj.lastReceivedComplexData.Name, "Complex data Name mismatch");
            Assert.AreEqual(complexData.Tags.Count, testObj.lastReceivedComplexData.Tags.Count, "Complex data Tags count mismatch");
            Assert.AreEqual(complexData.Stats.Count, testObj.lastReceivedComplexData.Stats.Count, "Complex data Stats count mismatch");
            
            testObj.lastReceivedComplexData = null;
            
            var clientComplexData = new TestRPCBehaviour.ComplexData
            {
                Id = 123,
                Name = "ClientObject",
                Tags = new List<string> { "client", "test" },
                Stats = new Dictionary<string, int>
                {
                    { "score", 1000 },
                    { "level", 5 }
                }
            };
            
            NetMessage msg = new RPCMessage(0, testObj.NetObject.NetId, "ComplexDataRPC", null, clientComplexData);
            _client.Send(NetSerializer.Serialize(msg));
            
            startTime = Time.time;
            while (testObj.lastReceivedComplexData == null && Time.time - startTime < 1f)
            {
                yield return null;
            }
            
            Assert.IsNotNull(testObj.lastReceivedComplexData, "Complex data was not received from client");
            Assert.AreEqual(clientComplexData.Id, testObj.lastReceivedComplexData.Id, "Client complex data Id mismatch");
            Assert.AreEqual(clientComplexData.Name, testObj.lastReceivedComplexData.Name, "Client complex data Name mismatch");
            Assert.AreEqual(clientComplexData.Tags.Count, testObj.lastReceivedComplexData.Tags.Count, "Client complex data Tags count mismatch");
            Assert.AreEqual(clientComplexData.Stats.Count, testObj.lastReceivedComplexData.Stats.Count, "Client complex data Stats count mismatch");
        }
    }
} 