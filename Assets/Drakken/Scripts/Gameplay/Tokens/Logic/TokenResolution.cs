using System.Collections.Generic;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Unity.Netcode;

namespace Drakken.Gameplay.Tokens.Logic
{
    public class TokenResolution : INetworkSerializable
    {
        public List<GameSimulationTrace> Traces = new();

        public TokenResolution() { }

        public TokenResolution(params GameSimulationTrace[] traces)
        {
            Traces = new List<GameSimulationTrace>(traces);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            => serializer.SerializeList(ref Traces);
    }
}
