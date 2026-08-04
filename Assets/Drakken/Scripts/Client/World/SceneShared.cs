using System;
using UnityEngine;

namespace Drakken.Client.World
{
    public class SceneObjects
    {
        public TokenView[] MyTokenViews { get; set; } = new TokenView[0];
        public TokenView[] OpTokenViews { get; set; } = new TokenView[0];
        public DiceView[] MyDiceViews { get; set; } = new DiceView[0];
        public DiceView[] OpDiceViews { get; set; } = new DiceView[0];

        public void OnDisconnect()
        {
            foreach (var view in MyTokenViews)
                GameObject.Destroy(view.gameObject);

            foreach (var view in OpTokenViews)
                GameObject.Destroy(view.gameObject);

            foreach (var view in MyDiceViews)
                GameObject.Destroy(view.gameObject);

            foreach (var view in OpDiceViews)
                GameObject.Destroy(view.gameObject);

            MyTokenViews = new TokenView[0];
            OpTokenViews = new TokenView[0];
            MyDiceViews = new DiceView[0];
            OpDiceViews = new DiceView[0];
        }
    }
}
