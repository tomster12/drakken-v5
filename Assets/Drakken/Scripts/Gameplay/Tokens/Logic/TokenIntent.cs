using Unity.Netcode;

namespace Drakken.Gameplay.Tokens.Logic
{
    public abstract class TokenIntent : INetworkSerializable
    {
        public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { }
    }
}
