using System;
using TMPro;
using Unity.Profiling;
using UnityEngine;

namespace Drakken.Client.GameObjects
{
    public class SceneLayout : MonoBehaviour
    {
        public static SceneLayout Singleton { get; private set; }

        private void Awake() => Singleton = this;

        public ConnectingLayout Connecting;
        public DraftingLayout Drafting;
        public SharedLayout Shared;
    }

    [Serializable]
    public class ConnectingLayout
    {

        public PhysicalButton JoinButton;
        public PhysicalButton ReadyButton;
    }

    [Serializable]
    public class DraftingLayout
    {
        public Transform DraftTokenRow;
        public Transform DraftConfirmAnchor;
        public PhysicalButton DraftConfirmButton;
    }

    [Serializable]
    public class SharedLayout
    {

        public Transform MyDiceRow;
        public Transform OpponentDiceRow;
        public Transform MyHandRow;
        public Transform OpponentHandRow;
        public float TokenSpacing = 0.4f;
        public float DiceSpacing = 0.35f;
    }
}
