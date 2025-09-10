using System.Collections.Generic;
using MessagePack;

namespace SimpleNet.Messages
{
    /// <summary>
    /// Base class for instantiating messages with specified target clients.
    /// </summary>
    [MessagePackObject]
    public abstract class NetMessage
    {
        [Key(0)]public List<int> target;

        protected NetMessage(){}
        /// <param name="target">Optional list of target client IDs to receive this message. If null, the message will be sent to all clients.</param>
        protected NetMessage(List<int> target = null)
        {
            this.target = target;
        }

        public override string ToString()
        {
            string targetStr = target != null ? string.Join(",", target) : "all";
            return $"{GetType().Name}[Target:{targetStr}]";
        }
    }
}
