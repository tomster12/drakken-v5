using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client.World.Animation;
using Drakken.Domain;
using Drakken.Utility;
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
        public AnimationPlayer Animator { get; private set; } = new();
        public bool IsHovered { get; private set; } = false;
        private bool IsInteractable => !Animator.IsAnimating;

        // ------------------------------ Binding

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

            // Update labels to match dice instance values
            if (valueLabel != null) valueLabel.text = DiceInstance.Value.ToString();
            if (sidesLabel != null) sidesLabel.text = $"D{DiceInstance.Sides}";
        }

        // ------------------------------ Animation

        public async Task AnimateRoll(CancellationToken ct)
        {
            // Generate 5 random values evenly spaced across duration
            float duration = 2.0f;
            float values = 15;
            List<(float Time, int Value)> timedValues = new();

            for (int i = 0; i < values; i++)
            {
                float time = Easing.EaseInSin((float)i / (values - 1)) * duration;
                int value = Random.Range(1, DiceInstance.Sides + 1);
                timedValues.Add((time, value));
            }
            timedValues.Add((duration, DiceInstance.Value));

            // Build and play animation
            var animationBuilder = AnimationSequenceBuilder
                .Start()
                .Next(AnimationTracks.EulerRotation(duration, transform, transform.rotation, new Vector3(0, 360f * 3, 0), Easing.EaseInOutQuad));

            foreach (var pair in timedValues)
            {
                animationBuilder.At(pair.Time, () => valueLabel.text = pair.Value.ToString());
            }

            var animation = animationBuilder.Build();
            await Animator.Play(animation, ct);
        }

        // ------------------------------ Interaction

        private void Update()
        {
            // Show sides label when hovered
            sidesLabel.gameObject.SetActive(IsHovered && IsInteractable);

            // Enable / disable hover
            outline.SetEnabled(IsHovered && IsInteractable);
            if (IsHovered && IsInteractable) outline.OutlineColor = hoverOutlineColor;

            // Update material based on if has effects
            bool hasEffects = DiceInstance.Effects != null && DiceInstance.Effects.Count > 0;
            if (renderer != null) renderer.material.color = hasEffects ? colorAffected : normalColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
        }
    }
}

