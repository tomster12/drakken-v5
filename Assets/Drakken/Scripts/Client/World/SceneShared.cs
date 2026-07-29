using System.Collections.Generic;
using UnityEngine;

namespace Drakken.Client.World
{
    public class SceneShared
    {
        public List<TokenView> TokenViews { get; private set; } = new();
        public List<DiceView> MyDiceViews { get; private set; } = new();
        public List<DiceView> OpponentDiceViews { get; private set; } = new();

        public void Clear()
        {
            foreach (var view in TokenViews)
            {
                Object.Destroy(view.gameObject);
            }

            foreach (var view in MyDiceViews)
            {
                Object.Destroy(view.gameObject);
            }

            foreach (var view in OpponentDiceViews)
            {
                Object.Destroy(view.gameObject);
            }

            TokenViews.Clear();
            MyDiceViews.Clear();
            OpponentDiceViews.Clear();
        }
    }
}
