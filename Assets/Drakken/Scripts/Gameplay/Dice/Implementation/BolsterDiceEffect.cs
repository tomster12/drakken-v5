using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Gameplay.Dice.Logic;
using Drakken.Gameplay.Simulation;
using Drakken.Domain.Networking;
using Drakken.Gameplay.Tokens.Logic;
using Drakken.Utility;
using Unity.Netcode;
using UnityEngine;
using Drakken.Domain;

namespace Drakken.Gameplay.Dice.Implementation
{
    public class BolsterDiceEffectResolution : EventResolution
    {
        public List<int> AffectedInstanceIds = new();
        public List<int> NewValues = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            serializer.SerializeList(ref AffectedInstanceIds);
            serializer.SerializeList(ref NewValues);
        }
    }

    public class BolsterDiceEffect : DiceEffectLogic<BolsterDiceEffectResolution>
    {
        private static readonly Color HighlightColor = Colors.Hex("#9cec92");
        private const float HighlightDuration = 0.9f;
        private const float LabelRiseHeight = 0.6f;

        public override int EffectId => DiceEffectIds.Bolster;

        public override void Execute(DiceEffectExecuteContext ctx)
        {
            int magnitude = ctx.SettledDice.Value;

            var targets = ctx.CandidatePool
                .Where(d => d.InstanceId != ctx.SettledDice.InstanceId)
                .OrderBy(_ => Random.value)
                .Take(magnitude);

            var resolution = new BolsterDiceEffectResolution();

            foreach (var dice in targets)
            {
                if (!TokenExecutionLogic.TryModify(dice, ctx.World)) continue;

                int newValue = dice.Faces[dice.CurrentSide].Value + 1;
                dice.Faces[dice.CurrentSide].Value = newValue;

                resolution.AffectedInstanceIds.Add(dice.InstanceId);
                resolution.NewValues.Add(newValue);
            }

            if (resolution.AffectedInstanceIds.Count > 0)
                ctx.World.RecordEvent(EffectId, EventKind.Dice, ctx.SettledDice.InstanceId, ctx.SettledDice.CurrentSide, resolution);
        }

        protected override void Apply(GameState gameState, BolsterDiceEffectResolution resolution, int clientIndex, int sourceInstanceId)
        {
            var client = gameState.Clients[clientIndex];

            for (int i = 0; i < resolution.AffectedInstanceIds.Count; i++)
            {
                var dice = client.Dice.Find(d => d.InstanceId == resolution.AffectedInstanceIds[i]);
                if (dice != null) dice.Faces[dice.CurrentSide].Value = resolution.NewValues[i];
            }
        }

        protected override async Task Animate(EventAnimateContext ctx, BolsterDiceEffectResolution resolution, int sourceInstanceId, CancellationToken ct)
        {
            var tasks = new List<Task>();

            foreach (var instanceId in resolution.AffectedInstanceIds)
            {
                var diceView = ctx.FindDiceView(instanceId);
                if (diceView == null) continue;

                diceView.RefreshLabels();

                tasks.Add(diceView.FlashHighlight(HighlightColor, HighlightDuration, ct));
                tasks.Add(ctx.Client.Vfx.SpawnFloatingLabel(
                    "+1",
                    HighlightColor,
                    diceView.transform.position + Vector3.up * LabelRiseHeight,
                    Quaternion.Euler(45f, ctx.Client.Match.ClientIndex == 1 ? 180f : 0f, 0f),
                    ct));

                _ = diceView.SetSettledFace(diceView.DiceInstance, diceView.DiceInstance.CurrentSide, ct);
            }

            await Task.WhenAll(tasks);
        }
    }
}
