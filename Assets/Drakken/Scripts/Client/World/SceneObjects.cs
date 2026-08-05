using System;
using UnityEngine;

namespace Drakken.Client.World
{
    [Serializable]
    public class SceneObjects
    {
        public ScenePlayerObjects P1 = new();
        public ScenePlayerObjects P2 = new();

        public ScenePlayerObjects Player(int clientIndex) => clientIndex == 0 ? P1 : P2;

        public void OnDisconnect()
        {
            P1.OnDisconnect();
            P2.OnDisconnect();
        }
    }

    public class ScenePlayerObjects
    {
        public TokenView[] TokenViews { get; set; } = new TokenView[0];
        public DiceView[] DiceViews { get; set; } = new DiceView[0];

        public void OnDisconnect()
        {
            foreach (var view in TokenViews)
                GameObject.Destroy(view.gameObject);

            foreach (var view in DiceViews)
                GameObject.Destroy(view.gameObject);

            TokenViews = new TokenView[0];
            DiceViews = new DiceView[0];
        }
    }
}
