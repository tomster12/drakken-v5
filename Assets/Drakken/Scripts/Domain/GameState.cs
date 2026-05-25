using Unity.Netcode;

namespace Drakken.Domain
{
    public class GameState : INetworkSerializable
    {
        public GameStateClient[] Clients { get; set; } = new GameStateClient[2] { new(), new() };
        public int TurnClientIndex;
        public int Turn = 1;
        public int Round = 1;
        public GameStateClient CurrentClient => Clients[TurnClientIndex];
        public GameStateClient OpponentClient => Clients[1 - TurnClientIndex];

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Clients[0]);
            serializer.SerializeValue(ref Clients[1]);
            serializer.SerializeValue(ref TurnClientIndex);
            serializer.SerializeValue(ref Turn);
            serializer.SerializeValue(ref Round);
        }

    }
}
