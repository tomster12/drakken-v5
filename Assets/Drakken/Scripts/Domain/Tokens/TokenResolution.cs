using Unity.Netcode;

namespace Drakken.Domain.Tokens
{
    public abstract class TokenResolution : INetworkSerializable
    {
        public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {}
    }
}
