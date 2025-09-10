using System;
using System.Collections.Generic;
using MessagePack;
using NUnit.Framework;
using SimpleNet.Serializer;

namespace SimpleNet.Messaging.Tests
{
    public class SerializerTests
    {
        private object objectTest;
        private ISerialize _Serializer;
        
        [SetUp]
        public void SetUp()
        {
            _Serializer = new MPSerializer();
        }

        [Test]
        public void StringTest()
        {
            objectTest = "Hello World";
            byte[] pck = _Serializer.Serialize(objectTest);
            
            Assert.IsNotNull(pck, "Serialized object is null");
            Assert.IsTrue(objectTest.Equals(_Serializer.Deserialize<object>(pck)), "Serialized object is incorrect");
        }
        [Test]
        public void IntTest()
        {
            objectTest = 5678;
            byte[] pck = _Serializer.Serialize(objectTest);
            
            Assert.IsNotNull(pck, "Serialized object is null");
            Assert.IsTrue(objectTest.Equals(_Serializer.Deserialize<object>(pck)), "Serialized object is incorrect");
        }
        [Test]
        public void ObjectTest()
        {
            MessageTest msg = new MessageTest(456, "Message");
            byte[] pck = _Serializer.Serialize(msg);
            MessageTest msg2 = _Serializer.Deserialize<MessageTest>(pck);
            
            Assert.IsNotNull(pck, "Serialized object is null");
            Assert.IsTrue(msg.health.Equals(msg2.health) && msg.msg.Equals(msg2.msg), "Serialized object is incorrect");
        }
        [Test]
        public void ListTest()
        {
            List<int> list = new List<int>();
            list.Add(5678);
            byte[] pck = _Serializer.Serialize(list);
            List<int> list2 = _Serializer.Deserialize<List<int>>(pck);
            Assert.IsNotNull(pck, "Serialized object is null");
            Assert.IsNotNull(list2, "Deserialized list is null");
            Assert.IsTrue(list2.Count == list.Count, "Deserialized list count is incorrect");
            Assert.IsTrue(list2.Contains(5678), "Deserialized object is incorrect");
        }
        [Test]
        public void ObjectListTest()
        {
            List<MessageTest> list = new List<MessageTest>();
            list.Add(new MessageTest(5678, "Message")
            );
            byte[] pck = _Serializer.Serialize(list);
            List<MessageTest> list2 = _Serializer.Deserialize<List<MessageTest>>(pck);
            
            Assert.IsNotNull(pck, "Serialized object is null");
            Assert.IsNotNull(list2, "Deserialized list is null");
            Assert.IsTrue(list2.Count == list.Count, "Deserialized list count is incorrect");
            Assert.IsTrue(list2[0].msg.Equals(list[0].msg), "Deserialized object is incorrect");
        }
        [Test]
        public void NestedListTest()
        {
            List<MessageTest> list = new List<MessageTest>();
            list.Add(new MessageTest(5678, "Message", new List<int>(){0,1,2}));
            
            byte[] pck = _Serializer.Serialize(list);
            List<MessageTest> list2 = _Serializer.Deserialize<List<MessageTest>>(pck);
            
            Assert.IsNotNull(pck, "Serialized object is null");
            Assert.IsNotNull(list2, "Deserialized list is null");
            Assert.IsTrue(list2.Count == list.Count, "Deserialized list count is incorrect");
            Assert.IsTrue(list2[0].list[0].Equals(list[0].list[0]), "Deserialized object is incorrect");
        }
        [Test]
        public void DictionaryTest()
        {
            Dictionary<int,MessageTest> dic = new Dictionary<int,MessageTest>();
            dic[0] = new MessageTest(5678, "Message", new List<int>(){0,1,2});
            
            byte[] pck = _Serializer.Serialize(dic);
            Dictionary<int,MessageTest> dic2 = _Serializer.Deserialize< Dictionary<int,MessageTest>>(pck);
            
            Assert.IsNotNull(pck, "Serialized object is null");
            Assert.IsNotNull(dic2, "Deserialized list is null");
            Assert.IsTrue(dic2.Count == dic.Count, "Deserialized list count is incorrect");
            Assert.IsTrue(dic[0].msg.Equals(dic2[0].msg), "Deserialized object is incorrect");
        }
        [Test]
        public void DateTimeTest()
        {
            DateTime originalDateTime = DateTime.UtcNow;
            byte[] pck = _Serializer.Serialize(originalDateTime);
            
            Assert.IsNotNull(pck, "Serialized DateTime is null");
            DateTime deserializedDateTime = _Serializer.Deserialize<DateTime>(pck);
            Assert.AreEqual(originalDateTime.Ticks, deserializedDateTime.Ticks, "DateTime serialization/deserialization failed");
        }
    }
    [MessagePackObject]
    public class MessageTest
    {
        [Key(0)] public int health;
        [Key(1)] public string msg;
        [Key(2)] public List<int> list;

        public MessageTest() { }

        public MessageTest(int health, string msg, List<int> list = null)
        {
            this.health = health;
            this.msg = msg;
            this.list = list;
        }
    }
}
