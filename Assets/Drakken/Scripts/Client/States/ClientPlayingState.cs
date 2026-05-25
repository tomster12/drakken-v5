using Drakken.Common.Utility;
using System.Threading.Tasks;
using Drakken.Domain;

namespace Drakken.Client.States
{
    public class ClientPlayingState : ClientState
    {
        public override Task Enter()
        {
            Log.Info("ClientStatePlaying", "Entered playing state");

            var allDice = GameState.Clients[Match.clientIndex].Dice;
            for (int i = 0; i < allDice.Count; i++)
            {
                var dice = allDice[i];
                Log.Info("ClientStatePlaying", $"Client dice {i}: sides={dice.Sides} value={dice.Value}");
            }

            return Task.CompletedTask;
        }
    }
}
