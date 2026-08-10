using Drakken.Domain;
using Unity.Netcode;

namespace Drakken.Domain.Networking
{
    public class MatchDiceSimulationTraces : INetworkSerializable
    {
        public DiceSimulationTraces P1 = new();
        public DiceSimulationTraces P2 = new();

        public DiceSimulationTraces Player(int clientIndex) => clientIndex == 0 ? P1 : P2;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref P1);
            serializer.SerializeValue(ref P2);
        }
    }
}
