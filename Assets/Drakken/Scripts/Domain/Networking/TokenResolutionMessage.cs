using System;
using Drakken.Domain.Tokens;
using Drakken.Domain.Tokens.Logic;
using Unity.Netcode;

namespace Drakken.Domain.Networking
{
    public struct TokenResolutionMessage : INetworkSerializable
    {
        public bool Success;
        public string TokenId;
        public int TokenInstanceId;
        public int SourceClientIndex;
        public TokenResolution Resolution;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Success);
            serializer.SerializeValue(ref TokenId);
            serializer.SerializeValue(ref TokenInstanceId);
            serializer.SerializeValue(ref SourceClientIndex);

            if (serializer.IsReader)
            {
                var tokenRegistry = GameEntrypoint.Singleton.TokenRegistry;
                var entry = tokenRegistry.GetEntryOrThrow(TokenId);
                Resolution = (TokenResolution)Activator.CreateInstance(entry.ResolutionType);
            }

            Resolution.NetworkSerialize(serializer);
        }
    }
}
