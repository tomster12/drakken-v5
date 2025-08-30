using Drakken.Common.Utility;
using Unity.Netcode;

namespace Drakken.Common.Data
{
    public class GameState : INetworkSerializable
    {
        public GamePlayer[] Players;
        public int TurnPlayerIndex;
        public int Turn;
        public int Round;
        public GamePlayer CurrentPlayer => Players[TurnPlayerIndex];
        public GamePlayer NextPlayer => Players[1 - TurnPlayerIndex];

        public GameState()
        {
            Players = new GamePlayer[2] { new(), new() };
            TurnPlayerIndex = 0;
            Turn = 1;
            Round = 1;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            Assert.True(Players.Length == 2);
            serializer.SerializeValue(ref Players[0]);
            serializer.SerializeValue(ref Players[1]);
            serializer.SerializeValue(ref TurnPlayerIndex);
            serializer.SerializeValue(ref Turn);
            serializer.SerializeValue(ref Round);
        }
    }
}
