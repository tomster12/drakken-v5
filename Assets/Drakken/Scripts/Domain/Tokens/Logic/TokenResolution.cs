using Unity.Netcode;

namespace Drakken.Domain.Tokens.Logic
{
    public abstract class TokenResolution : INetworkSerializable
    {
        public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {}
    }
}
