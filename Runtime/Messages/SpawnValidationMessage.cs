using MessagePack;

namespace SimpleNet.Messages
{
    [MessagePackObject]
    public class SpawnValidationMessage : NetMessage
    {
        [Key(1)]public int netObjectId;
        [Key(2)]public int requesterId;

        public SpawnValidationMessage(){}
        public SpawnValidationMessage(int netObjectId, int requesterId)
        {
            this.netObjectId = netObjectId;
            this.requesterId = requesterId;
        }

        public override string ToString()
        {
            return $"{base.ToString()}, Requester:{requesterId}, ID:{netObjectId}";
        }
    }
}