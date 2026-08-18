using System.Collections.Generic;
using Drakken.Client;
using Drakken.Client.World;

namespace Drakken.Domain.Dice.Logic
{
    public class EffectAnimateContext
    {
        public readonly GameClient Client;
        private readonly ScenePlayerObjects existingViews;
        private readonly Dictionary<int, DiceView> liveViews;

        public EffectAnimateContext(GameClient client, ScenePlayerObjects existingViews, Dictionary<int, DiceView> liveViews)
        {
            Client = client;
            this.existingViews = existingViews;
            this.liveViews = liveViews;
        }

        public DiceView FindDiceView(int instanceId)
        {
            if (liveViews.TryGetValue(instanceId, out var view)) return view;
            return existingViews?.FindDiceView(instanceId);
        }
    }
}
