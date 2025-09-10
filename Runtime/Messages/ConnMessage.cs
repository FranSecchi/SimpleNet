using System.Collections.Generic;
using MessagePack;
using SimpleNet.Transport;

namespace SimpleNet.Messages
{
    /// <summary>
    /// Message for connection status updates.
    /// </summary>
    [MessagePackObject]
    public class ConnMessage : NetMessage
    {
        [Key(1)]public int CurrentConnected;
        [Key(2)]public List<int> AllConnected;
        [Key(3)]public ServerInfo ServerInfo;
        
        public ConnMessage(){}

        /// <param name="currentConnected">The ID of the currently connected client.</param>
        /// <param name="allConnected">List of IDs for all currently connected clients.</param>
        /// <param name="serverInfo">Information about the server connection.</param>
        /// <param name="target">Optional list of target client IDs to receive this message.</param>
        public ConnMessage(int currentConnected, List<int> allConnected, ServerInfo serverInfo, List<int> target = null) : base(target)
        {
            this.CurrentConnected = currentConnected;
            this.AllConnected = allConnected;
            this.ServerInfo = serverInfo;
        }

        public override string ToString()
        {
            string allConnStr = AllConnected != null ? string.Join(",", AllConnected) : "none";
            return $"{base.ToString()} Current:{CurrentConnected}, AllConnected:[{allConnStr}], ServerInfo:{ServerInfo}";
        }
    }
}
