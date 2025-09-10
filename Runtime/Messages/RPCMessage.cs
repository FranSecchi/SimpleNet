using System.Collections.Generic;
using MessagePack;

namespace SimpleNet.Messages
{
    /// <summary>
    /// Message for invoking a remote procedure call.
    /// </summary>
    [MessagePackObject]
    public class RPCMessage : NetMessage
    {
        [Key(1)] public int SenderID { get; private set; }
        [Key(2)] public int ObjectId;
        [Key(3)] public string MethodName;
        [Key(4)] public object[] Parameters;

        public RPCMessage() { }
        /// <param name="SenderID">The ID of the client sending the RPC message.</param>
        /// <param name="objectId">The ID of the network object to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="target">Optional list of target client IDs to receive this message.</param>
        /// <param name="parameters">The parameters to pass to the method being invoked.</param>
        public RPCMessage(int SenderID, int objectId, string methodName, List<int> target = null, params object[] parameters) : base(target)
        {
            this.SenderID = SenderID;
            this.ObjectId = objectId;
            this.MethodName = methodName;
            this.Parameters = parameters;
        }

        public override string ToString()
        {
            return $"RPCMessage(SenderID: {SenderID}, ObjectId: {ObjectId}, Method: {MethodName}, Parameters: {string.Join(", ", Parameters ?? new object[0])})";
        }
    }
}