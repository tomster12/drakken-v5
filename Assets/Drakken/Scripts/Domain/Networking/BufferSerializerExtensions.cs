using System.Collections.Generic;
using System.Text;
using Unity.Netcode;

namespace Drakken.Domain.Networking
{
    public static class BufferSerializerExtensions
    {
        public static void SerializeList<TItem, TReaderWriter>(this BufferSerializer<TReaderWriter> serializer, ref List<TItem> list)
            where TReaderWriter : IReaderWriter
            where TItem : INetworkSerializable, new()
        {
            if (serializer.IsReader)
            {
                int count = 0;
                serializer.SerializeValue(ref count);
                list = new(count);

                for (int i = 0; i < count; i++)
                {
                    TItem item = new();
                    serializer.SerializeValue(ref item);
                    list.Add(item);
                }
            }
            else
            {
                int count = list.Count;
                serializer.SerializeValue(ref count);

                for (int i = 0; i < count; i++)
                {
                    var item = list[i];
                    serializer.SerializeValue(ref item);
                }
            }
        }

        public static void SerializeList<TReaderWriter>(this BufferSerializer<TReaderWriter> serializer, ref List<int> list)
            where TReaderWriter : IReaderWriter
        {
            if (serializer.IsReader)
            {
                int count = 0;
                serializer.SerializeValue(ref count);
                list = new(count);

                for (int i = 0; i < count; i++)
                {
                    int item = new();
                    serializer.SerializeValue(ref item);
                    list.Add(item);
                }
            }
            else
            {
                int count = list.Count;
                serializer.SerializeValue(ref count);

                for (int i = 0; i < count; i++)
                {
                    var item = list[i];
                    serializer.SerializeValue(ref item);
                }
            }
        }

        public static void SerializeString<TReaderWriter>(this BufferSerializer<TReaderWriter> serializer, ref string value)
            where TReaderWriter : IReaderWriter
        {
            if (serializer.IsWriter)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
                int len = bytes.Length;
                serializer.SerializeValue(ref len);
                for (int i = 0; i < len; i++) serializer.SerializeValue(ref bytes[i]);
            }
            else
            {
                int len = 0;
                serializer.SerializeValue(ref len);
                byte[] bytes = new byte[len];
                for (int i = 0; i < len; i++) serializer.SerializeValue(ref bytes[i]);
                value = Encoding.UTF8.GetString(bytes);
            }
        }
    }
}
