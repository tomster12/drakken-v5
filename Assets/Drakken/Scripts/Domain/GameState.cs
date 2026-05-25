using Drakken.Common.Utility;
using Unity.Netcode;

namespace Drakken.Domain
{
    public class GameState : INetworkSerializable
    {
        public GameClientState[] Clients;
        public int TurnClientIndex;
        public int Turn;
        public int Round;
        public GameClientState CurrentClient => Clients[TurnClientIndex];
        public GameClientState NextClient => Clients[1 - TurnClientIndex];

        public GameState()
        {
            Clients = new GameClientState[2] { new(), new() };
            TurnClientIndex = 0;
            Turn = 1;
            Round = 1;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            Assert.True(Clients.Length == 2);
            serializer.SerializeValue(ref Clients[0]);
            serializer.SerializeValue(ref Clients[1]);
            serializer.SerializeValue(ref TurnClientIndex);
            serializer.SerializeValue(ref Turn);
            serializer.SerializeValue(ref Round);
        }
    }
}
