using Drakken.Domain;
using Unity.Netcode;

namespace Drakken.Domain.Networking
{
    public class GameSimulationMatchTrace : INetworkSerializable
    {
        public GameSimulationTrace P1 = new();
        public GameSimulationTrace P2 = new();

        public GameSimulationTrace Player(int clientIndex) => clientIndex == 0 ? P1 : P2;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref P1);
            serializer.SerializeValue(ref P2);
        }
    }
}
