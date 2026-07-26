using System;
using UnityEngine;

namespace Drakken.Client.GameObjects
{
    public class SceneLayout : MonoBehaviour
    {
        public ConnectingLayout Connecting;
        public DraftingLayout Drafting;
        public SharedLayout Shared;

        private void Awake()
        {
            Connecting.JoinButton.gameObject.SetActive(false);
            Connecting.ReadyButton.gameObject.SetActive(false);

            Drafting.DraftTokenRow.gameObject.SetActive(true);
            Drafting.DraftConfirmButton.gameObject.SetActive(false);

            Shared.MyDiceRow.gameObject.SetActive(true);
            Shared.OpponentDiceRow.gameObject.SetActive(true);
            Shared.MyHandRow.gameObject.SetActive(true);
            Shared.OpponentHandRow.gameObject.SetActive(true);
        }
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
