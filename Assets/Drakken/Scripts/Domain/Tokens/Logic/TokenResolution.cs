using System.Collections.Generic;
using Drakken.Domain.Networking;
using Unity.Netcode;

namespace Drakken.Domain.Tokens.Logic
{
    public abstract class TokenResolution : INetworkSerializable
    {
        public abstract IEnumerable<DiceSimulationTraces> Traces { get; }

        public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { }
    }
}
