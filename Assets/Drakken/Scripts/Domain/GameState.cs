using Unity.Netcode;

namespace Drakken.Domain
{
    public class GameState : INetworkSerializable
    {
        public GameStateClient[] Clients { get; set; } = new GameStateClient[2] { new(), new() };
        public int TurnClientIndex;
        public GamePhase Phase = GamePhase.NotStarted;
        public int Turn = 1;
        public int Round = 1;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Clients[0]);
            serializer.SerializeValue(ref Clients[1]);
            serializer.SerializeValue(ref TurnClientIndex);
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref Turn);
            serializer.SerializeValue(ref Round);
        }
        
        public GameState Clone()
        {
            return new GameState
            {
                Clients = new[] { Clients[0].Clone(), Clients[1].Clone() },
                TurnClientIndex = TurnClientIndex,
                Phase = Phase,
                Turn = Turn,
                Round = Round,
            };
        }

        public DiceInstance GetDiceInstance(int instanceId)
        {
            foreach (var client in Clients)
            {
                var instance = client.Dice.Find(d => d.InstanceId == instanceId);
                if (instance != null) return instance;
            }
            return null;
        }
    }

    public enum GamePhase { NotStarted, Drafting, Playing }
}
