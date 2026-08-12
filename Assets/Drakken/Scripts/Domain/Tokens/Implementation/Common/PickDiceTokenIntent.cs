using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client.World;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens.Logic;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace Drakken.Domain.Tokens.Implementation.Common
{
    public class PickDiceTokenIntent : TokenIntent
    {
        public List<int> TargetDiceInstanceIds = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeList(ref TargetDiceInstanceIds);
        }

        // Picks `count` random dice, for opponent AI / debug harnesses that don't drive real UI selection
        public static PickDiceTokenIntent PickRandom(List<DiceInstance> dice, int count)
        {
            return new PickDiceTokenIntent
            {
                TargetDiceInstanceIds = dice
                    .Select(d => d.InstanceId)
                    .OrderBy(_ => UnityEngine.Random.value)
                    .Take(count)
                    .ToList()
            };
        }
    }

    public class PickDiceTokenIntentPicker : TokenIntentPicker<PickDiceTokenIntent>
    {
        private readonly TargetOwner targetOwner;
        private readonly int count;

        public PickDiceTokenIntentPicker(TargetOwner targetOwner, int count = 1)
        {
            this.targetOwner = targetOwner;
            this.count = count;
        }

        protected override async Task<PickDiceTokenIntent> PickIntent(TokenVisualContext context, int clientIndex)
        {
            var pickableDiceViews = targetOwner == TargetOwner.Opponent
                ? context.Client.SceneObjects.Player(1 - clientIndex).DiceViews.Values
                : context.Client.SceneObjects.Player(clientIndex).DiceViews.Values;

            var selected = new List<DiceView>();
            var selectionCompleteTcs = new TaskCompletionSource<bool>();

            void OnDiceClicked(DiceView diceView)
            {
                if (selected.Contains(diceView))
                {
                    selected.Remove(diceView);
                    diceView.SetSelected(false);
                }
                else if (selected.Count < count)
                {
                    selected.Add(diceView);
                    diceView.SetSelected(true);
                }

                if (selected.Count == count) selectionCompleteTcs.TrySetResult(true);
            }

            foreach (var diceView in pickableDiceViews)
            {
                diceView.SetPickable(true);
                diceView.OnClicked.AddListener(OnDiceClicked);
            }

            // Wait for either selecting enough, or clicking off to cancel. Whichever wins, stop the other's polling loop
            var cancelClickCts = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(
                selectionCompleteTcs.Task,
                WaitForCancelClick(pickableDiceViews, cancelClickCts.Token));

            cancelClickCts.Cancel();
            bool cancelledSelection = completedTask != selectionCompleteTcs.Task;

            foreach (var diceView in pickableDiceViews)
            {
                diceView.SetPickable(false);
                diceView.OnClicked.RemoveListener(OnDiceClicked);
            }

            if (cancelledSelection) return null;

            return new PickDiceTokenIntent
            {
                TargetDiceInstanceIds = selected.Select(diceView => diceView.DiceInstance.InstanceId).ToList()
            };
        }

        private static async Task WaitForCancelClick(IEnumerable<DiceView> pickableDiceViews, CancellationToken ct)
        {
            var mouse = Mouse.current;

            while (!ct.IsCancellationRequested)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !pickableDiceViews.Any(d => d.IsHovered))
                {
                    return;
                }

                await Task.Yield();
            }
        }
    }
}
