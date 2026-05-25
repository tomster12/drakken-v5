using Unity.Netcode;

namespace Drakken.Domain.Tokens
{
    public abstract class TokenIntent : INetworkSerializable
    {
        public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { }
    }
}
