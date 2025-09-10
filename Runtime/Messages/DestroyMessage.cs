using System.Collections.Generic;
using MessagePack;

namespace SimpleNet.Messages
{
    /// <summary>
    /// Message for destroying a network object.
    /// </summary>
    [MessagePackObject]
    public class DestroyMessage : NetMessage
    {
        [Key(1)] public int netObjectId;
        [Key(2)] public int requesterId;

        public DestroyMessage() { }

        /// <param name="netObjectId">The ID of the network object to be destroyed.</param>
        /// <param name="requesterId">The ID of the client requesting the object destruction.</param>
        /// <param name="target">Optional list of target client IDs to receive this message.</param>
        public DestroyMessage(int netObjectId, int requesterId, List<int> target = null) : base(target)
        {
            this.netObjectId = netObjectId;
            this.requesterId = requesterId;
        }

        public override string ToString()
        {
            return $"{base.ToString()} NetObjectId:{netObjectId}, Requester:{requesterId}";
        }
    }
}