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
            Title.SetInitialState();
            Drafting.SetInitialState();
            Shared.SetInitialState();
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

        public void SetInitialState()
        {
            Title.SetActive(false);
            JoinButton.gameObject.SetActive(false);
            ReadyButton.gameObject.SetActive(false);
            OptionsButton.gameObject.SetActive(false);
            ExitButton.gameObject.SetActive(false);
            Clutter.SetActive(false);
        }
    }

    [Serializable]
    public class DraftingLayout
    {
        public Transform CameraPosition;
        public Transform DraftTokenRow;
        public PhysicalButton DraftConfirmButton;

        public void SetInitialState()
        {
            DraftConfirmButton.gameObject.SetActive(false);
        }
    }

    [Serializable]
    public class SharedLayout
    {
        public Transform MyDiceRow;
        public Transform OpDiceRow;
        public Transform MyTokenRow;
        public Transform OpTokenRow;
        public GameObject Mat1;
        public GameObject Mat2;
        public float TokenSpacing = 0.4f;
        public float DiceSpacing = 0.35f;

        public void SetInitialState()
        {
            Mat1.SetActive(false);
            Mat2.SetActive(false);
        }

        public void OnDisconnect()
        {
            SetInitialState();
        }
    }
}
