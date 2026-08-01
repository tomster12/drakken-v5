using System.Collections.Generic;
using UnityEngine;

namespace Drakken.Client.World
{
    public class SceneShared
    {
        public List<TokenView> MyTokenViews { get; private set; } = new();
        public List<TokenView> OpTokenViews { get; private set; } = new();
        public List<DiceView> MyDiceViews { get; private set; } = new();
        public List<DiceView> OpDiceViews { get; private set; } = new();

        public void OnDisconnect()
        {
            foreach (var view in MyTokenViews)
                Object.Destroy(view.gameObject);

            foreach (var view in OpTokenViews)
                Object.Destroy(view.gameObject);

            foreach (var view in MyDiceViews)
                Object.Destroy(view.gameObject);

            foreach (var view in OpDiceViews)
                Object.Destroy(view.gameObject);

            MyTokenViews.Clear();
            OpTokenViews.Clear();
            MyDiceViews.Clear();
            OpDiceViews.Clear();
        }
    }
}
