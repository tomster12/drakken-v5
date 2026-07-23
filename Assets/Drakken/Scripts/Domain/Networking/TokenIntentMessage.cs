using Unity.Netcode;

namespace Drakken.Networking
{
    public struct TokenIntentMessage : INetworkSerializable
    {
        public string TokenId;
        public int InstanceId;
        public string IntentJson;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeString(ref TokenId);
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeString(ref IntentJson);
        }
    }
}
