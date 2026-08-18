using Unity.Netcode;

namespace Drakken.Domain.Dice.Logic
{
    public abstract class EffectResolution : INetworkSerializable
    {
        public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { }
    }

    public class EmptyEffectResolution : EffectResolution { }
}
