using Unity.Netcode;

namespace Drakken.Gameplay.Tokens.Logic
{
    // Read-only data a token wants available to its own AnimateToken before any dice-level replay
    // starts (e.g. how many dice it's about to replace) - never applied to GameState, purely for
    // client-side choreography. Most tokens have nothing to say here and just use the base type.
    public abstract class TokenSummary : INetworkSerializable
    {
        public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { }
    }

    public class EmptyTokenSummary : TokenSummary { }
}
