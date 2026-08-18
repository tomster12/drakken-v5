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

        // Dice value changes caused by a settle-triggered dice/face effect (e.g. Bolster) - these
        // fire no matter which token's simulation caused the settle, so they're applied and
        // animated generically here rather than each token needing its own equivalent field.
        public List<DiceValueChange> SideEffectsValueChanges = new();

        public virtual void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeList(ref SideEffectsDestroyedDiceInstanceIds);
            serializer.SerializeList(ref SideEffectsValueChanges);
        }
    }

    public class DiceValueChange : INetworkSerializable
    {
        public int InstanceId;
        public int NewValue;
        public int SourceInstanceId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref NewValue);
            serializer.SerializeValue(ref SourceInstanceId);
        }
    }
}
