using System;
using MessagePack;

namespace SimpleNet.Serializer
{
    public class MPSerializer : ISerialize
    {
        private MessagePackSerializerOptions options;

        public MPSerializer()
        {
            options = MessagePackSerializerOptions.Standard.WithResolver(
                MessagePack.Resolvers.CompositeResolver.Create(
                    MessagePack.Resolvers.StandardResolver.Instance, 
                    MessagePack.Resolvers.TypelessObjectResolver.Instance
                )
            );
        }
        public byte[] Serialize<T>(T data)
        {
            return MessagePackSerializer.Serialize(data, options);
        }
        public T Deserialize<T>(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Cannot deserialize: input data is null or empty.");
            }
            return MessagePackSerializer.Deserialize<T>(data, options);
        }
        public object Deserialize(byte[] data, Type type)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Cannot deserialize: input data is null or empty.");
            }
            return MessagePackSerializer.Deserialize(type, data, options);
        }
    }
}
