using SimpleNet.Messages;

namespace SimpleNet.Serializer
{
    /// <summary>
    /// Static utility class that provides serialization and deserialization functionality for network messages.
    /// Uses an underlying serializer implementation to handle the actual serialization operations.
    /// </summary>
    public static class NetSerializer
    {
        /// <summary>
        /// The underlying serializer implementation used for serialization operations. Set up here your own serialization strategy.
        /// </summary>
        public static ISerialize Serializer = new MPSerializer();

        /// <summary>
        /// Serializes a network message into a byte array.
        /// </summary>
        /// <typeparam name="T">The type of the network message to serialize.</typeparam>
        /// <param name="data">The network message to serialize.</param>
        /// <returns>A byte array containing the serialized network message.</returns>
        public static byte[] Serialize<T>(T data) where T : NetMessage
        {
            return Serializer.Serialize(data);
        }

        /// <summary>
        /// Deserializes a byte array into a network message of type T.
        /// </summary>
        /// <typeparam name="T">The type of the network message to deserialize into.</typeparam>
        /// <param name="data">The byte array containing the serialized network message.</param>
        /// <returns>The deserialized network message of type T.</returns>
        public static T Deserialize<T>(byte[] data) where T : NetMessage
        {
            return Serializer.Deserialize<T>(data);
        }
    }
}
