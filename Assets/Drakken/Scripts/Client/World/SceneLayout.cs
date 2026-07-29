using System;
using UnityEngine;

namespace Drakken.Client.World
{
    public class SceneLayout : MonoBehaviour
    {
        public TitleLayout Title;
        public DraftingLayout Drafting;
        public SharedLayout Shared;

        private void Awake()
        {
            Title.Title.SetActive(false);
            Title.JoinButton.gameObject.SetActive(false);
            Title.ReadyButton.gameObject.SetActive(false);
            Title.OptionsButton.gameObject.SetActive(false);
            Title.ExitButton.gameObject.SetActive(false);
            Title.Clutter.SetActive(false);

            Drafting.DraftConfirmButton.gameObject.SetActive(false);

            Shared.Mat1.SetActive(false);
            Shared.Mat2.SetActive(false);
        }
    }

    [Serializable]
    public class TitleLayout
    {
        public Transform CameraPosition;
        public GameObject Title;
        public PhysicalButton JoinButton;
        public PhysicalButton ReadyButton;
        public PhysicalButton OptionsButton;
        public PhysicalButton ExitButton;
        public GameObject Clutter;
    }

    [Serializable]
    public class DraftingLayout
    {
        public Transform CameraPosition;
        public Transform DraftTokenRow;
        public PhysicalButton DraftConfirmButton;
    }

    [Serializable]
    public class SharedLayout
    {
        public Transform MyDiceRow;
        public Transform OpponentDiceRow;
        public Transform MyHandRow;
        public GameObject Mat1;
        public GameObject Mat2;
        public float TokenSpacing = 0.4f;
        public float DiceSpacing = 0.35f;
    }
}
