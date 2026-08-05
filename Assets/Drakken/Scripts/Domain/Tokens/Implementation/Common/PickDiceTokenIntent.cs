using System.Threading.Tasks;
using Drakken.Domain.Tokens.Logic;
using Unity.Netcode;

namespace Drakken.Domain.Tokens.Implementation.Common
{
    public class PickDiceTokenIntent : TokenIntent
    {
        public int TargetDiceInstanceId;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref TargetDiceInstanceId);
        }
    }

    public class PickDiceTokenIntentPicker : TokenIntentPicker<PickDiceTokenIntent>
    {
        private readonly TargetOwner targetOwner;

        public PickDiceTokenIntentPicker(TargetOwner targetOwner)
        {
            this.targetOwner = targetOwner;
        }

        protected override async Task<PickDiceTokenIntent> PickIntent(TokenVisualContext context, int clientIndex)
        {
            var pickableDiceViews = targetOwner == TargetOwner.Opponent
                ? context.SceneObjects.Player(1 - clientIndex).DiceViews
                : context.SceneObjects.Player(clientIndex).DiceViews;

            return new PickDiceTokenIntent { TargetDiceInstanceId = pickableDiceViews[0].DiceInstance.InstanceId };
        }
    }
}
