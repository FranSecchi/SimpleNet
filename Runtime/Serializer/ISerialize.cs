using System;

namespace SimpleNet.Serializer
{
    /// <summary>
    /// Interface defining serialization and deserialization operations.
    /// </summary>
    public interface ISerialize
    {
        /// <summary>
        /// Serializes an object of type T into a byte array.
        /// </summary>
        /// <typeparam name="T">The type of the object to serialize.</typeparam>
        /// <param name="data">The object to serialize.</param>
        /// <returns>A byte array containing the serialized data.</returns>
        public byte[] Serialize<T>(T data);

        /// <summary>
        /// Deserializes a byte array into an object of type T.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize into.</typeparam>
        /// <param name="data">The byte array containing the serialized data.</param>
        /// <returns>The deserialized object of type T.</returns>
        public T Deserialize<T>(byte[] data);

        /// <summary>
        /// Deserializes a byte array into an object of the specified type.
        /// </summary>
        /// <param name="data">The byte array containing the serialized data.</param>
        /// <param name="type">The type of the object to deserialize into.</param>
        /// <returns>The deserialized object.</returns>
        object Deserialize(byte[] data, Type type);
    }
}
