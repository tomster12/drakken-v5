using Drakken.Domain;
using Unity.Netcode;

namespace Drakken.Domain.Networking
{
    // Carries the two per-player physics trace windows produced alongside a drafting-phase broadcast.
    public class MatchDiceTraces : INetworkSerializable
    {
        public DicePhysicsTrace P1 = new();
        public DicePhysicsTrace P2 = new();

        public DicePhysicsTrace Player(int clientIndex) => clientIndex == 0 ? P1 : P2;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref P1);
            serializer.SerializeValue(ref P2);
        }
    }
}
