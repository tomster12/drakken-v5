using System;
using Drakken.Gameplay.Tokens;
using Drakken.Gameplay.Tokens.Logic;
using Unity.Netcode;

namespace Drakken.Domain.Networking
{
    public struct TokenIntentMessage : INetworkSerializable
    {
        public string TokenId;
        public int InstanceId;
        public TokenIntent Intent;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeString(ref TokenId);
            serializer.SerializeValue(ref InstanceId);

            if (serializer.IsReader)
            {
                var tokenRegistry = GameEntrypoint.Singleton.TokenRegistry;
                var entry = tokenRegistry.GetEntryOrThrow(TokenId);
                Intent = (TokenIntent)Activator.CreateInstance(entry.IntentType);
            }

            Intent.NetworkSerialize(serializer);
        }
    }
}
