using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Drakken.Client
{
    [Serializable]
    public class ClientPlayerUI
    {

        [SerializeField] public TextMeshPro OpDiceTotal;
    }

    public class ClientUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject StatusPanel;
        [SerializeField] private TextMeshProUGUI StatusTitleText;
        [SerializeField] private TextMeshProUGUI StatusSubTitleText;
        [SerializeField] private Image MyAvatar;
        [SerializeField] private Image OpAvatar;
        [SerializeField] public Transform DiceTotalParent;
        [SerializeField] public TextMeshPro MyDiceTotal;
        [SerializeField] public TextMeshPro OpDiceTotal;

        [Header("Config")]
        [SerializeField] private Color AvatarVisibleColor;
        [SerializeField] private Color AvatarReadyColor;

        private void Awake()
        {
            SetInitialState();
        }

        private void SetInitialState()
        {
            HideStatus();
            SetMyAvatar(AvatarState.Hidden);
            SetOpAvatar(AvatarState.Hidden);
            UpdateDiceTotal(0, 0, hide: true);
            UpdateDiceTotal(0, 1, hide: true);
        }

        public void OnDisconnect()
        {
            SetInitialState();
        }

        public void SetStatus(string title, string subTitle)
        {
            StatusPanel.SetActive(true);
            StatusTitleText.text = title;
            StatusSubTitleText.text = subTitle;
        }

        public void HideStatus()
        {
            StatusPanel.SetActive(false);
        }

        public void SetMyAvatar(AvatarState state)
        {
            switch (state)
            {
                case AvatarState.Hidden:
                    MyAvatar.gameObject.SetActive(false);
                    break;
                case AvatarState.Visible:
                    MyAvatar.gameObject.SetActive(true);
                    MyAvatar.color = AvatarVisibleColor;
                    break;
                case AvatarState.Ready:
                    MyAvatar.gameObject.SetActive(true);
                    MyAvatar.color = AvatarReadyColor;
                    break;
            }
        }

        public void SetOpAvatar(AvatarState state)
        {
            switch (state)
            {
                case AvatarState.Hidden:
                    OpAvatar.gameObject.SetActive(false);
                    break;
                case AvatarState.Visible:
                    OpAvatar.gameObject.SetActive(true);
                    OpAvatar.color = AvatarVisibleColor;
                    break;
                case AvatarState.Ready:
                    OpAvatar.gameObject.SetActive(true);
                    OpAvatar.color = AvatarReadyColor;
                    break;
            }
        }

        public void UpdateDiceTotal(int playerClientImdex, int clientIndex, bool unknown = false, bool hide = false)
        {
            // Manipulation so we can use same 2 text meshes irrelevant of which way looking at board
            DiceTotalParent.transform.rotation = Quaternion.Euler(0, playerClientImdex * 180f, 0);
            var diceTotal = playerClientImdex == 0
                ? (clientIndex == 0 ? MyDiceTotal : OpDiceTotal)
                : (clientIndex == 0 ? OpDiceTotal : MyDiceTotal);

            if (hide)
            {
                diceTotal.gameObject.SetActive(false);
                return;
            }

            diceTotal.gameObject.SetActive(true);

            if (unknown)
            {
                diceTotal.text = "?";
            }
            else
            {
                var match = GameEntrypoint.Singleton.Client.Match;
                var total = match.GameState.Clients[clientIndex].GetDiceTotal();
                diceTotal.text = total.ToString();
            }
        }

        public enum AvatarState { Hidden, Visible, Ready }
    }
}
