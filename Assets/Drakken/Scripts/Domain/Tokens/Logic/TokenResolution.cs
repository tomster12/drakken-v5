using System.Collections.Generic;
using Drakken.Domain.Networking;
using Unity.Netcode;

namespace Drakken.Domain.Tokens.Logic
{
    public abstract class TokenResolution : INetworkSerializable
    {
        // Dice destroyed as a consequence of a blocked modification (e.g. a Glass dice breaking) -
        // populated via DiceModifications.CanModify. This is about dice being removed, not about
        // whether a token's own effect was cancelled - each token's own resolution fields already
        // say that (e.g. ForgeTokenResolution.CombinedDiceInstance being null means the merge was
        // cancelled). The framework removes these dice from GameState automatically; individual
        // tokens don't need to handle that part.
        public List<int> SideEffectsDestroyedDiceInstanceIds = new();

        public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeList(ref SideEffectsDestroyedDiceInstanceIds);
        }
    }
}
