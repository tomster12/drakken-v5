using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain.Animation;
using Drakken.Domain;
using Drakken.Domain.Dice;
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

        private Outline outline;
        private Color hoverOutlineColor = Color.white;
        private Color selectedOutlineColor = Colors.Hex("#a1e0ff");
        private Color currentHighlightOutlineColor = Color.white;
        private bool isHighlighted = false;
        private readonly float FaceLabelFontSize = 2.5f;
        private readonly float FaceLabelSurfaceOffset = 0.01f;
        private Vector2 FaceLabelSize = new(1.0f, 1.0f);
        private Color FaceLabelColor = Colors.Hex("#9f8d81");

        private IGameStateProvider gameStateProvider;
        private IReadOnlyList<DiceMeshFactory.DiceFacePose> faces;
        private readonly List<(TextMeshPro Label, int FaceIndex)> faceLabels = new();
        // fallback for dice not tracked in GameState, e.g. preview dice
        private DiceInstance detachedInstance;
        public int InstanceId { get; private set; }
        public AnimationPlayer Animator { get; private set; } = new();
        public bool IsHovered { get; private set; } = false;
        public bool IsSelected { get; private set; } = false;
        public bool IsPickable { get; private set; } = false;
        private bool isInteractionLocked = false;
        public bool IsInteractable => !Animator.IsAnimating && !isInteractionLocked;
        // Use GameState dice instance whenever possible to prevent splitting
        public DiceInstance DiceInstance => gameStateProvider?.GameState?.GetDiceInstance(InstanceId) ?? detachedInstance;

        // ------------------------------ Binding

        public static DiceView Create(
            AssetDatabase assets, DiceInstance instance, IGameStateProvider gameStateProvider = null, float scale = 1.0f)
        {
            GameObject diceGo = new($"Dice View (D{instance.Sides})");
            var diceView = diceGo.AddComponent<DiceView>();

            diceView.Bind(assets, instance, gameStateProvider, scale);

            return diceView;
        }

        private void Bind(AssetDatabase assets, DiceInstance instance, IGameStateProvider gameStateProvider, float scale = 1.0f)
        {
            this.InstanceId = instance.InstanceId;
            this.gameStateProvider = gameStateProvider;

            // Keep track of this incase we need it
            this.detachedInstance = instance;

            var diceMesh = DiceMeshFactory.Create(instance, assets.DiceMeshMaterial, scale);
            diceMesh.GameObject.transform.SetParent(transform, false);
            faces = diceMesh.Faces;

            CreateFaceLabels(assets, instance, diceMesh.Faces, scale);

            outline = gameObject.AddComponent<Outline>();
            outline.Setup();
        }

        private void CreateFaceLabels(
            AssetDatabase assets,
            DiceInstance dice,
            IReadOnlyList<DiceMeshFactory.DiceFacePose> faces,
            float scale = 1.0f)
        {

            for (int i = 0; i < faces.Count && i < dice.Faces.Count; i++)
            {
                DiceMeshFactory.DiceFacePose pose = faces[i];

                if (pose.LabelSpots != null)
                {
                    foreach (DiceMeshFactory.DiceLabelSpot spot in pose.LabelSpots)
                    {
                        var label = CreateFaceLabel(
                            assets, dice.Faces[spot.ValueFaceIndex].Value,
                            spot.Position + pose.Direction * FaceLabelSurfaceOffset, spot.Rotation,
                            // Use a smaller scale when we have multiple label spots
                            scale * 0.6f);
                        faceLabels.Add((label, spot.ValueFaceIndex));
                    }
                }
                else
                {
                    var label = CreateFaceLabel(
                        assets, dice.Faces[i].Value,
                        pose.Position + pose.Direction * FaceLabelSurfaceOffset, pose.LabelRotation,
                        scale * GetFaceLabelScaleMultiplier(dice.Sides));
                    faceLabels.Add((label, i));
                }
            }
        }

        private TextMeshPro CreateFaceLabel(
            AssetDatabase assets,
            int value,
            Vector3 localPosition,
            Quaternion localRotation,
            float scale)
        {
            // TextMeshPro requires a RectTransform, so add it before touching the transform.
            GameObject labelGo = new($"Face Label {value}");
            var label = labelGo.AddComponent<TextMeshPro>();

            label.rectTransform.SetParent(transform, false);
            label.rectTransform.SetLocalPositionAndRotation(localPosition, localRotation);
            label.rectTransform.sizeDelta = FaceLabelSize * scale;

            label.text = value.ToString();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = FaceLabelFontSize * scale;
            label.color = FaceLabelColor;

            if (assets.DiceFaceLabelFont != null) label.font = assets.DiceFaceLabelFont;
            if (assets.DiceFaceLabelMaterial != null) label.material = assets.DiceFaceLabelMaterial;

            return label;
        }

        private static float GetFaceLabelScaleMultiplier(int sides)
        {
            // For bypyramids we have to shrink the labels to fit on the triangles
            bool isBipyramid =
                sides >= 10 &&
                sides != 12 &&
                sides != 20;

            if (!isBipyramid) return 1f;

            if (sides >= 20) return 0.4f;
            if (sides >= 14) return 0.5f;
            if (sides >= 10) return 0.6f;
            return 0.7f;
        }

        public void RefreshFaceLabels()
        {
            var dice = DiceInstance;
            if (dice == null) return;

            foreach (var (label, faceIndex) in faceLabels)
            {
                if (faceIndex < 0 || faceIndex >= dice.Faces.Count) continue;
                label.text = dice.Faces[faceIndex].Value.ToString();
            }
        }

        // ------------------------------ Animation

        public async Task AnimateGrow(CancellationToken ct, float duration = 0.3f)
        {
            await Animator.Play(AnimationSequenceBuilder.Start()
                .Next(AnimationTracks.LocalScale(
                    duration, transform, transform.localScale, Vector3.one, Easing.EaseOutCubic))
                .Build(), ct);
        }

        public async Task AnimateShake(CancellationToken ct, float durationSeconds = 0.8f, float positionMagnitude = 0.05f)
        {
            Vector3 basePosition = transform.position;
            Quaternion baseRotation = transform.rotation;
            transform.rotation = baseRotation;

            // Spin continuously around 2 random axes
            int firstAxis = Random.Range(0, 3);
            int secondAxis = (firstAxis + Random.Range(1, 3)) % 3;

            Vector3 spinAmount = Vector3.zero;
            spinAmount[firstAxis] = 1 * 360f * (Random.value < 0.5f ? -1f : 1f);
            spinAmount[secondAxis] = 1 * 360f * (Random.value < 0.5f ? -1f : 1f);

            var builder = AnimationSequenceBuilder.Start()
                .Next(AnimationTracks.LocalEulerRotation(
                    durationSeconds, transform, baseRotation, spinAmount, Easing.Linear));

            // Jittery positions in parallel with the spin
            const int jitterSteps = 12;
            float stepDuration = durationSeconds / jitterSteps;
            Vector3 previousPosition = basePosition;

            for (int i = 0; i < jitterSteps; i++)
            {
                bool isLastStep = i == jitterSteps - 1;

                Vector3 targetPosition = isLastStep
                    ? basePosition
                    : basePosition + new Vector3(
                        Random.Range(positionMagnitude * 0.5f, positionMagnitude) * (Random.value < 0.5f ? -1f : 1f), 0f,
                        Random.Range(positionMagnitude * 0.5f, positionMagnitude) * (Random.value < 0.5f ? -1f : 1f));

                builder.At(i * stepDuration, AnimationTracks.Position(
                    stepDuration, transform, previousPosition, targetPosition, Easing.Linear));

                previousPosition = targetPosition;
            }

            await Animator.Play(builder.Build(), ct);
        }

        public async Task AnimateRoll(CancellationToken ct, float durationMultiplier = 1f, Vector3? targetDirection = null)
        {
            // Start and end at targetRotation, with value facing towards targetDirection
            Quaternion targetRotation = DiceMeshFactory.GetRotationForSide(faces, DiceInstance.CurrentSide, targetDirection);
            Quaternion startRotation = targetRotation;
            transform.rotation = startRotation;

            // Spin around 2 random axis in full integer rotations
            int firstAxis = Random.Range(0, 3);
            int secondAxis = (firstAxis + Random.Range(1, 3)) % 3;

            Vector3 spinAmount = Vector3.zero;
            spinAmount[firstAxis] = Random.Range(3, 4) * 360f * (Random.value < 0.5f ? -1f : 1f);
            spinAmount[secondAxis] = Random.Range(1, 2) * 360f * (Random.value < 0.5f ? -1f : 1f);

            float durationSeconds = 1.0f * durationMultiplier;

            await Animator.Play(AnimationSequenceBuilder.Start()
                .Next(AnimationTracks.LocalEulerRotation(
                    durationSeconds, transform, startRotation, spinAmount, Easing.EaseOutCubic))
                .Build(), ct);
        }

        public async Task AnimateShrinkAndDestroy(CancellationToken ct)
        {
            await Animator.Play(AnimationSequenceBuilder.Start()
                .Next(AnimationTracks.LocalScale(
                    0.3f, transform, transform.localScale, Vector3.zero, Easing.EaseInCubic))
                .Build(), ct);

            GameObject.Destroy(gameObject);
        }

        public async Task FlashHighlight(Color color, float durationSeconds, CancellationToken ct)
        {
            currentHighlightOutlineColor = color;
            isHighlighted = true;

            try
            {
                await Task.Delay((int)(durationSeconds * 1000f), ct);
            }
            catch (TaskCanceledException) { }
            finally
            {
                isHighlighted = false;
            }
        }

        // ------------------------------ Interaction

        private void Update()
        {
            // Enable / disable outline, preferring the selected color, then highlight, then hover
            bool showOutline = IsSelected || isHighlighted || (IsHovered && IsInteractable);
            outline.SetEnabled(showOutline);
            if (showOutline)
            {
                outline.OutlineColor = IsSelected
                    ? selectedOutlineColor
                    : isHighlighted ? currentHighlightOutlineColor : hoverOutlineColor;
            }
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
