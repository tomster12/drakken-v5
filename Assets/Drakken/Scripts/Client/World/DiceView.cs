using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client.World.Animation;
using Drakken.Domain;
using Drakken.Generation;
using Drakken.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Drakken.Client.World
{
    public class DiceView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler
    {
        public UnityEvent<DiceView> OnClicked = new();

        [Header("References")]
        [SerializeField] private new Renderer renderer;
        [SerializeField] private TextMeshPro valueLabel;
        [SerializeField] private TextMeshPro sidesLabel;
        [SerializeField] private Outline outline;

        [Header("Config")]
        [SerializeField] private Color normalColor = new(0.19f, 0.15f, 0.133f);
        [SerializeField] private Color hoverOutlineColor = Color.white;
        [SerializeField] private Color selectedOutlineColor = new(0.63f, 0.88f, 1f);

        private const float FaceLabelFontSize = 3f;
        private const float FaceLabelSurfaceOffset = 0.01f;
        private static readonly Vector2 FaceLabelSize = new(0.8f, 0.8f);

        public DiceInstance DiceInstance { get; private set; }
        public AnimationPlayer Animator { get; private set; } = new();
        public bool IsHovered { get; private set; } = false;
        public bool IsSelected { get; private set; } = false;
        public bool IsPickable { get; private set; } = false;
        private bool isInteractionLocked = false;
        public bool IsInteractable => !Animator.IsAnimating && !isInteractionLocked;

        private bool isProcedural = false;

        // ------------------------------ Binding

        public static DiceView Create(AssetDatabase assets, DiceInstance instance)
        {
            var prefab = assets.DiceViewPrefab;
            var diceGo = Instantiate(prefab);
            var diceView = diceGo.GetComponent<DiceView>();

            diceView.Bind(instance);

            return diceView;
        }

        public static DiceView CreateProcedural(AssetDatabase assets, DiceInstance instance)
        {
            GameObject diceGo = new($"Dice View (D{instance.Sides})");
            var diceView = diceGo.AddComponent<DiceView>();

            diceView.BindProcedural(assets, instance);

            return diceView;
        }

        void Awake()
        {
            if (sidesLabel != null) sidesLabel.gameObject.SetActive(false);
        }

        public void Bind(DiceInstance dice)
        {
            this.DiceInstance = dice;

            outline.Setup();

            // Update labels to match dice instance values
            if (valueLabel != null) valueLabel.text = DiceInstance.Value.ToString();
            if (sidesLabel != null) sidesLabel.text = $"D{DiceInstance.Sides}";
        }

        public void BindProcedural(AssetDatabase assets, DiceInstance dice)
        {
            isProcedural = true;
            this.DiceInstance = dice;

            var diceMesh = DiceMeshFactory.Create(dice, assets.DiceMeshMaterial);
            diceMesh.GameObject.transform.SetParent(transform, false);
            renderer = diceMesh.GameObject.GetComponent<Renderer>();

            CreateFaceLabels(assets, dice, diceMesh.Faces);
        }

        private void CreateFaceLabels(AssetDatabase assets, DiceInstance dice, IReadOnlyList<DiceMeshFactory.DiceFacePose> faces)
        {
            for (int i = 0; i < faces.Count && i < dice.Faces.Count; i++)
            {
                DiceMeshFactory.DiceFacePose pose = faces[i];

                // TextMeshPro requires a RectTransform, so add it before touching the transform.
                GameObject labelGo = new($"Face Label {dice.Faces[i].Value}");
                var label = labelGo.AddComponent<TextMeshPro>();

                label.rectTransform.SetParent(transform, false);
                label.rectTransform.SetLocalPositionAndRotation(
                    pose.Position + pose.Direction * FaceLabelSurfaceOffset,
                    pose.LabelRotation);
                label.rectTransform.sizeDelta = FaceLabelSize;

                label.text = dice.Faces[i].Value.ToString();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = FaceLabelFontSize;
                if (assets.DiceFaceLabelFont != null) label.font = assets.DiceFaceLabelFont;
            }
        }

        public void Rebind(DiceInstance dice)
        {
            // Repoints this view at the latest DiceInstance for the same dice (e.g. after a new round's GameState
            // replaces the old one) without recreating the view or re-running one-time setup
            this.DiceInstance = dice;
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
            // Procedural test dice have no outline/hover-label setup to drive.
            if (isProcedural) return;

            // Show sides label when hovered
            sidesLabel.gameObject.SetActive(IsHovered && IsInteractable);

            // Enable / disable outline, preferring the selected color over the hover color
            bool showOutline = IsSelected || (IsHovered && IsInteractable);
            outline.SetEnabled(showOutline);
            if (showOutline) outline.OutlineColor = IsSelected ? selectedOutlineColor : hoverOutlineColor;

            renderer.material.color = normalColor;
        }

        public void SetInteractionLocked(bool interactionLocked)
        {
            isInteractionLocked = interactionLocked;
        }

        public void SetPickable(bool pickable)
        {
            IsPickable = pickable;
            if (!pickable) IsSelected = false;
        }

        public bool SetSelected(bool selected)
        {
            if (IsPickable && IsSelected != selected)
            {
                IsSelected = selected;
                return true;
            }

            return false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsPickable && IsInteractable && eventData.button == PointerEventData.InputButton.Left)
            {
                OnClicked.Invoke(this);
            }
        }
    }
}
