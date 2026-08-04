using Drakken.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Drakken.Client.World
{
    public class DiceView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [Header("References")]
        [SerializeField] private new Renderer renderer;
        [SerializeField] private TextMeshPro valueLabel;
        [SerializeField] private TextMeshPro sidesLabel;
        [SerializeField] private Outline outline;

        [Header("Config")]
        [SerializeField] private Color normalColor = new(0.19f, 0.15f, 0.133f);
        [SerializeField] private Color colorAffected = new(1f, 0.6f, 0.2f);
        [SerializeField] private Color hoverOutlineColor = Color.white;

        public DiceInstance DiceInstance { get; private set; }
        public bool IsHovered { get; private set; } = false;
        public bool IsInteractable { get; set; } = false;
        private bool toUpdateHovered = false;

        // ------------------------------ Setup

        public static DiceView Create(AssetDatabase assets, DiceInstance instance)
        {
            var prefab = assets.DiceViewPrefab;
            var diceGo = Instantiate(prefab);
            var diceView = diceGo.GetComponent<DiceView>();

            diceView.Bind(instance);

            return diceView;
        }

        void Awake()
        {
            sidesLabel.gameObject.SetActive(false);
        }

        public void Bind(DiceInstance dice)
        {
            this.DiceInstance = dice;
            
            outline.Setup();

            Refresh();
        }

        // ------------------------------ Interaction

        private void Update()
        {
            if (toUpdateHovered)
            {
                toUpdateHovered = false;
                UpdateHovered();
            }
        }

        private void UpdateHovered()
        {
            // Show sides label when hovered
            sidesLabel.gameObject.SetActive(IsHovered);

            // Enable / disable hover
            bool shouldShow = IsHovered && IsInteractable;
            outline.SetEnabled(shouldShow);

            if (shouldShow)
            {
                outline.OutlineColor = hoverOutlineColor;
            }
        }

        public void Refresh()
        {
            // Update labels to match dice instance values
            if (valueLabel != null) valueLabel.text = DiceInstance.Value.ToString();
            if (sidesLabel != null) sidesLabel.text = $"D{DiceInstance.Sides}";

            // Update material based on if has effects
            bool hasEffects = DiceInstance.Effects != null && DiceInstance.Effects.Count > 0;
            if (renderer != null)
                renderer.material.color = hasEffects ? colorAffected : normalColor;
        }

        public void SetInteractable(bool interactable)
        {
            if (IsInteractable == interactable) return;

            IsInteractable = interactable;

            if (!interactable && IsHovered)
            {
                IsHovered = false;
                toUpdateHovered = true;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsInteractable && !IsHovered)
            {
                IsHovered = true;
                toUpdateHovered = true;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (IsHovered)
            {
                IsHovered = false;
                toUpdateHovered = true;
            }
        }
    }
}