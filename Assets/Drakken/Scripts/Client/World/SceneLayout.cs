using System;
using UnityEngine;

namespace Drakken.Client.World
{
    public class SceneLayout : MonoBehaviour
    {
        public TitleLayout Title;
        public DraftingLayout Drafting;
        public GameLayout Game;
        public float TokenSpacing = 0.4f;
        public float DiceSpacing = 0.35f;

        private void Awake()
        {
            Title.SetInitialState();
            Drafting.SetInitialState();
            Game.SetInitialState();
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
        public PhysicalButton DraftConfirmButton;
        public DraftingPlayerLayout P1;
        public DraftingPlayerLayout P2;

        public DraftingPlayerLayout Player(int clientIndex) => clientIndex == 0 ? P1 : P2;

        public void SetInitialState()
        {
            DraftConfirmButton.gameObject.SetActive(false);
        }
    }

    [Serializable]
    public class DraftingPlayerLayout
    {
        public Transform CameraPosition;
        public Transform DraftConfirmButtonPosition;
        public Transform DraftTokenRow;
    }

    [Serializable]
    public class GameLayout
    {
        public GamePlayerLayout P1;
        public GamePlayerLayout P2;
        public Transform CentrePos;
        public float PlayDropRadius = 1.0f;

        public GamePlayerLayout Player(int clientIndex) => clientIndex == 0 ? P1 : P2;

        public void SetInitialState()
        {
            P1.Mat.SetActive(false);
            P2.Mat.SetActive(false);
        }

        public void OnDisconnect()
        {
            SetInitialState();
        }
    }

    [Serializable]
    public class GamePlayerLayout
    {
        public Transform PlayingCameraPosition;
        public Transform DiceRow;
        public Transform TokenRow;
        public GameObject Mat;
        public GameObject Bag;
    }
}

