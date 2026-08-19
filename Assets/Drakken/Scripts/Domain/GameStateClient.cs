using Drakken.Domain.Networking;
using Drakken.Gameplay.Tokens;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

namespace Drakken.Domain
{
    public class GameStateClient : INetworkSerializable
    {
        public List<DiceInstance> Dice = new();
        public List<TokenInstance> Tokens = new();
        public int Score;


        public int GetDiceTotal()
        {
            int total = 0;

            foreach (var dice in Dice)
            {
                total += dice.Value;
            }

            return total;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeList(ref Dice);
            serializer.SerializeList(ref Tokens);
            serializer.SerializeValue(ref Score);
        }

        public GameStateClient Clone()
        {
            return new GameStateClient
            {
                Dice = Dice.Select(dice => dice.Clone()).ToList(),
                Tokens = Tokens.Select(token => token.Clone()).ToList(),
                Score = Score,
            };
        }
    }
}
