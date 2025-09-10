using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace SimpleNet.Messages
{
    /// <summary>
    /// Message for reconciling object state.
    /// </summary>
    [MessagePackObject]
    public class ReconcileMessage : NetMessage
    {
        [Key(1)] public int ObjectId;
        [Key(2)] public int ComponentId;
        [Key(3)] public DateTime Timestamp;
        [Key(4)] public Dictionary<string, object> Values;

        public ReconcileMessage(){}
        /// <param name="objectId">The ID of the network object.</param>
        /// <param name="componentId">The ID of the component.</param>
        /// <param name="timestamp">The timestamp in which the true state is situated.</param>
        /// <param name="values">The updated changes to be reconciled.</param>
        /// <param name="target">The player Id who should reconcile the state.</param>
        public ReconcileMessage(int objectId, int componentId, DateTime timestamp, Dictionary<string, object> values, int target) : base(new List<int>{target})
        {
            this.ObjectId = objectId;
            this.ComponentId = componentId;
            this.Timestamp = timestamp;
            this.Values = values;
        }
        public override string ToString()
        {
            string changes = Values != null ? string.Join(", ", Values.Select(kv => $"{kv.Key}={kv.Value}")) : "none";
            return $"{base.ToString()} [{Timestamp:HH:mm:ss.fff}] ObjectID:{ObjectId}, ComponentID:{ComponentId}, Changes:{changes}";
        }
    }
}