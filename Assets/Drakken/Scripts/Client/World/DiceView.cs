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
        private bool isInteractionLocked = false;
        private bool IsInteractable => !Animator.IsAnimating && !isInteractionLocked;

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

        public async Task AnimateRoll(CancellationToken ct, float durationMultiplier = 1)
        {
            float duration = 1.5f * durationMultiplier;
            int valueCount = (int)(15 * durationMultiplier);

            List<(float Time, int Value)> timedValues = new();

            for (int i = 0; i < valueCount; i++)
            {
                float time = Easing.InverseEaseInOutQuad((float)i / (valueCount - 1)) * duration;
                int value = Random.Range(1, DiceInstance.Sides + 1);
                timedValues.Add((time, value));
            }
            timedValues.Add((duration, DiceInstance.Value));

            // Build and play animation
            var animationBuilder = AnimationSequenceBuilder
                .Start()
                .Next(AnimationTracks.LocalEulerRotation(duration, transform, transform.localRotation, new Vector3(0, 360f * 3, 0), Easing.EaseInOutQuad));

            foreach (var pair in timedValues)
            {
                animationBuilder.At(pair.Time, () => valueLabel.text = pair.Value.ToString());
            }

            var animation = animationBuilder.Build();
            await Animator.Play(animation, ct);
        }

        public async Task AnimateGrowThenRoll(CancellationToken ct, float durationMultiplier)
        {
            await Animator.Play(AnimationSequenceBuilder
                .Start()
                .Next(AnimationTracks.LocalScale(
                    0.3f, transform, Vector3.zero, transform.localScale, Easing.EaseOutCubic))
                .Build(), ct);

            await AnimateRoll(ct, durationMultiplier);
        }

        public async Task AnimateShrinkAndDestroy(CancellationToken ct)
        {
            await Animator.Play(AnimationSequenceBuilder
                .Start()
                .Next(AnimationTracks.LocalScale(
                    0.3f, transform, transform.localScale, Vector3.zero, Easing.EaseInCubic))
                .Build(), ct);

            GameObject.Destroy(gameObject);
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

        public void SetInteractionLocked(bool interactionLocked)
        {
            isInteractionLocked = interactionLocked;
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
