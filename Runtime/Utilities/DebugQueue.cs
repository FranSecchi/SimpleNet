using System;
using System.Collections.Generic;
using SimpleNet.Messages;

namespace SimpleNet.Utilities
{
    public class DebugQueue
    {
        private static readonly Queue<DebugMessage> messageQueue = new Queue<DebugMessage>();
        private static readonly object messageQueueLock = new object();
        private static readonly object messageTypesLock = new object();
        private const int MaxMessages = 100;
        private static bool[] enabledMessageTypes;

        public class DebugMessage
        {
            public string Message { get; }
            public MessageType Type { get; }
            public DateTime Timestamp { get; }

            public DebugMessage(string message, MessageType type)
            {
                Message = message;
                Type = type;
                Timestamp = DateTime.UtcNow;
            }
        }

        public enum MessageType
        {
            Info,
            Warning,
            Error,
            Network,
            RPC,
            State,
            Rollback
        }

        static DebugQueue()
        {
            enabledMessageTypes = new bool[Enum.GetValues(typeof(MessageType)).Length];
            for (int i = 0; i < enabledMessageTypes.Length; i++)
            {
                enabledMessageTypes[i] = true;
            }
        }

        public static void SetMessageTypeEnabled(MessageType type, bool enabled)
        {
            lock (messageTypesLock)
            {
                enabledMessageTypes[(int)type] = enabled;
            }
        }

        public static bool IsMessageTypeEnabled(MessageType type)
        {
            lock (messageTypesLock)
            {
                return enabledMessageTypes[(int)type];
            }
        }

        public static void AddMessage(string message, MessageType type = MessageType.Info)
        {
            bool isEnabled;
            lock (messageTypesLock)
            {
                isEnabled = enabledMessageTypes[(int)type];
            }

            if (!isEnabled)
                return;

            lock (messageQueueLock)
            {
                messageQueue.Enqueue(new DebugMessage(message, type));
                while (messageQueue.Count > MaxMessages)
                {
                    messageQueue.Dequeue();
                }
            }
        }

        public static void AddNetworkMessage(NetMessage message, bool isReceived = true)
        {
            bool isEnabled;
            lock (messageTypesLock)
            {
                isEnabled = enabledMessageTypes[(int)MessageType.Network];
            }

            if (!isEnabled)
                return;

            string direction = isReceived ? "Received" : "Sent";
            AddMessage($"[{direction}] {message.GetType().Name}: {message}", MessageType.Network);
        }

        public static void AddRPC(string rpcName, int objectId, int senderId)
        {
            bool isEnabled;
            lock (messageTypesLock)
            {
                isEnabled = enabledMessageTypes[(int)MessageType.RPC];
            }

            if (!isEnabled)
                return;

            AddMessage($"[RPC] {rpcName} on object {objectId} from {senderId}", MessageType.RPC);
        }

        public static void AddStateChange(int objectId, int componentId, string stateName, object change)
        {
            bool isEnabled;
            lock (messageTypesLock)
            {
                isEnabled = enabledMessageTypes[(int)MessageType.State];
            }

            if (!isEnabled)
                return;

            AddMessage($"[State] Object {objectId}, component {componentId} changed {stateName} to {change}", MessageType.State);
        }

        public static void AddRollback(int objectId, float targetTime, string reason)
        {
            if (!IsMessageTypeEnabled(MessageType.Rollback))
                return;

            AddMessage($"[Rollback] Object {objectId} rolling back to time {targetTime:F3}s. Reason: {reason}", MessageType.Rollback);
        }

        public static List<DebugMessage> GetMessages()
        {
            lock (messageQueueLock)
            {
                return new List<DebugMessage>(messageQueue.ToArray());
            }
        }

        public static void ClearMessages()
        {
            lock (messageQueueLock)
            {
                messageQueue.Clear();
            }
        }
    }
} 