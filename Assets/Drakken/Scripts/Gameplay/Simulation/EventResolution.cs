using Unity.Netcode;

namespace Drakken.Gameplay.Simulation
{
    public abstract class EventResolution : INetworkSerializable
    {
        public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { }
    }

    public class EmptyEventResolution : EventResolution { }
}
