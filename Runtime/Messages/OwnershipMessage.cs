using System.Collections.Generic;
using MessagePack;

namespace SimpleNet.Messages
{
    /// <summary>
    /// Message for transferring object ownership.
    /// </summary>
    [MessagePackObject]
    public class OwnershipMessage : NetMessage
    {
        [Key(1)]public int netObjectId;
        [Key(2)]public int newOwnerId;

        public OwnershipMessage(){}
        
        /// <param name="netObjectId">The ID of the network object whose ownership is being transferred.</param>
        /// <param name="newOwnerId">The ID of the client that will become the new owner of the object.</param>
        /// <param name="target">Optional list of target client IDs to receive this message.</param>
        public OwnershipMessage(int netObjectId, int newOwnerId, List<int> target = null) : base(target)
        {
            this.netObjectId = netObjectId;
            this.newOwnerId = newOwnerId;
        }

        public override string ToString()
        {
            return $"{base.ToString()} ObjectID:{netObjectId}, NewOwner:{newOwnerId}";
        }
    }
} 