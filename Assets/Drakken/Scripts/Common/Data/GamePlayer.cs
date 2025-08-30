using Drakken.Common.Tokens;
using System.Collections.Generic;
using Unity.Netcode;

namespace Drakken.Common.Data
{
    public class GamePlayer : INetworkSerializable
    {
        public List<TokenInstance> Hand { get; set; } = new();
        public List<DiceInstance> Dice { get; set; } = new();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeList(Hand);
            serializer.SerializeList(Dice);
        }
    }
}
