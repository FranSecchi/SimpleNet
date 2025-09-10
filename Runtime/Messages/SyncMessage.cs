using System.Collections.Generic;
using MessagePack;
using System.Linq;

namespace SimpleNet.Messages
{
    /// <summary>
    /// Message for synchronizing object properties.
    /// </summary>
    [MessagePackObject]
    public class SyncMessage : NetMessage
    {
        [Key(1)] public int SenderId;   
        [Key(2)]public int ObjectID;
        [Key(3)]public int ComponentId;
        [Key(4)] public Dictionary<string, object> changedValues;

        public SyncMessage(){}
        /// <param name="senderId">The ID of the client sending the sync message.</param>
        /// <param name="objectID">The ID of the network object being synchronized.</param>
        /// <param name="componentID">The ID of the component being synchronized.</param>
        /// <param name="changes">Dictionary containing the changed properties and their new values.</param>
        /// <param name="target">Optional list of target client IDs to receive this message.</param>
        public SyncMessage(int senderId, int objectID, int componentID, Dictionary<string, object> changes, List<int> target = null) : base(target)
        {
            SenderId = senderId;
            ObjectID = objectID;
            changedValues = changes;
            ComponentId = componentID;
        }

        public override string ToString()
        {
            string changes = changedValues != null ? string.Join(", ", changedValues.Select(kv => $"{kv.Key}={kv.Value}")) : "none";
            return $"{base.ToString()} ObjectID:{ObjectID}, ComponentID:{ComponentId}, Changes:{changes}";
        }
    }
}
