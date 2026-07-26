using Drakken.Common.Utility;
using Drakken.Domain.Tokens;
using System.Collections.Generic;
using Unity.Netcode;

namespace Drakken.Domain
{
    public class GameStateClient : INetworkSerializable
    {
        public List<DiceInstance> Dice = new();
        public List<TokenInstance> Hand = new();
        public int Score;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeList(Dice);
            serializer.SerializeList(Hand);
            serializer.SerializeValue(ref Score);
        }

    }
}
