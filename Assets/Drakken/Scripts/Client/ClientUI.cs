using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Drakken.Client
{
    public class ClientUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject StatusPanel;
        [SerializeField] private TextMeshProUGUI StatusTitleText;
        [SerializeField] private TextMeshProUGUI StatusSubTitleText;
        [SerializeField] private Image MyAvatar;
        [SerializeField] private Image OpAvatar;
        [SerializeField] private TextMeshPro MyDiceTotal;
        [SerializeField] private TextMeshPro OpDiceTotal;

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
            UpdateMyDiceTotal(hide: true);
            UpdateOpDiceTotal(hide: true);
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

        public void UpdateMyDiceTotal(bool hide = false)
        {
            if (hide)
            {
                MyDiceTotal.gameObject.SetActive(false);
                return;
            }

            var match = GameEntrypoint.Singleton.Client.Match;
            var total = match.GameState.Clients[match.ClientIndex].GetDiceTotal();
            MyDiceTotal.text = total.ToString();
            MyDiceTotal.gameObject.SetActive(true);
        }

        public void UpdateOpDiceTotal(bool hide = false)
        {
            if (hide)
            {
                OpDiceTotal.gameObject.SetActive(false);
                return;
            }

            var match = GameEntrypoint.Singleton.Client.Match;
            var total = match.GameState.Clients[match.OpClientIndex].GetDiceTotal();
            OpDiceTotal.text = total.ToString();
            OpDiceTotal.gameObject.SetActive(true);
        }

        public enum AvatarState { Hidden, Visible, Ready }
    }
}
