using System.Collections.Generic;
using Unity.Netcode;

public static class NetworkSerializationUtils
{
    public static void SerializeList<TItem, TReaderWriter>(
        this BufferSerializer<TReaderWriter> serializer,
        List<TItem> list
    ) where TItem : class, INetworkSerializable, new()
      where TReaderWriter : IReaderWriter
    {
        int count = list.Count;
        serializer.SerializeValue(ref count);

        if (serializer.IsReader)
        {
            list.Clear();
            for (int i = 0; i < count; i++)
            {
                TItem item = new();
                serializer.SerializeValue(ref item);
                list.Add(item);
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                var item = list[i];
                serializer.SerializeValue(ref item);
            }
        }
    }
}
