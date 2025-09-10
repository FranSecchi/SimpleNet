using UnityEngine;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Synchronization;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using SimpleNet.Utilities;
using System.Collections.Generic;
using MessagePack;
namespace SimpleNet.Synchronization.Tests
{
    public class TestRPCBehaviour : NetBehaviour
    {
        public int lastReceivedValue;
        public string lastReceivedMessage;
        public bool serverToClientCalled;
        public bool clientToServerCalled;
        public bool targetModeAllCalled;
        public int targetModeAllCallCount;
        public bool targetModeSpecificCalled;
        public int targetModeSpecificCallCount;
        public bool targetModeOthersCalled;
        public int targetModeOthersCallCount;
        public ComplexData lastReceivedComplexData;

        [MessagePackObject]
        public class ComplexData
        {
            [Key(0)] public int Id { get; set; }
            [Key(1)] public string Name { get; set; }
            [Key(2)] public List<string> Tags { get; set; }
            [Key(3)] public Dictionary<string, int> Stats { get; set; }
        }

        [NetRPC]
        public void TestRPC(int value, string message)
        {
            lastReceivedValue = value;
            lastReceivedMessage = message;
        }

        [NetRPC]
        public void ComplexDataRPC(ComplexData data)
        {
            lastReceivedComplexData = data;
        }

        [NetRPC(Direction.ServerToClient)]
        public void ServerToClientRPC()
        {
            serverToClientCalled = true;
        }

        [NetRPC(Direction.ClientToServer)]
        public void ClientToServerRPC()
        {
            clientToServerCalled = true;
        }

        [NetRPC(Direction.Bidirectional, Send.All)]
        public void TargetModeAllRPC()
        {
            targetModeAllCalled = true;
            targetModeAllCallCount++;
        }

        [NetRPC(Direction.Bidirectional, Send.Specific)]
        public void TargetModeSpecificRPC(List<int> targetId)
        {
            targetModeSpecificCalled = true;
            targetModeSpecificCallCount++;
        }

        [NetRPC(Direction.Bidirectional, Send.Others)]
        public void TargetModeOthersRPC()
        {
            targetModeOthersCalled = true;
            targetModeOthersCallCount++;
        }

        public void CallTestRPC(int value, string message)
        {
            CallRPC("TestRPC", value, message);
        }

        public void CallServerToClientRPC()
        {
            CallRPC("ServerToClientRPC");
        }

        public void CallClientToServerRPC()
        {
            CallRPC("ClientToServerRPC");
        }

        public void CallTargetModeAllRPC()
        {
            CallRPC("TargetModeAllRPC");
        }

        public void CallTargetModeSpecificRPC(List<int> targetId)
        {
            CallRPC("TargetModeSpecificRPC", targetId);
        }

        public void CallTargetModeOthersRPC()
        {
            CallRPC("TargetModeOthersRPC");
        }

        public void CallComplexDataRPC(ComplexData data)
        {
            CallRPC("ComplexDataRPC", data);
        }
    }
}