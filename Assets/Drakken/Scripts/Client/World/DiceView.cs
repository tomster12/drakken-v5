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
        private Color selectedOutlineColor = new(0.63f, 0.88f, 1f);
        private float FaceLabelFontSize = 1.4f;
        private float FaceLabelSurfaceOffset = 0.01f;
        private Vector2 FaceLabelSize = new(0.8f, 0.8f);

        private IReadOnlyList<DiceMeshFactory.DiceFacePose> faces;
        public DiceInstance DiceInstance { get; private set; }
        public AnimationPlayer Animator { get; private set; } = new();
        public bool IsHovered { get; private set; } = false;
        public bool IsSelected { get; private set; } = false;
        public bool IsPickable { get; private set; } = false;
        private bool isInteractionLocked = false;
        public bool IsInteractable => !Animator.IsAnimating && !isInteractionLocked;

        // ------------------------------ Binding

        public static DiceView Create(AssetDatabase assets, DiceInstance instance, float scale = 1.0f)
        {
            GameObject diceGo = new($"Dice View (D{instance.Sides})");
            var diceView = diceGo.AddComponent<DiceView>();

            diceView.Bind(assets, instance, scale);

            return diceView;
        }

        private void Bind(AssetDatabase assets, DiceInstance dice, float scale = 1.0f)
        {
            this.DiceInstance = dice;

            var diceMesh = DiceMeshFactory.Create(dice, assets.DiceMeshMaterial, scale);
            diceMesh.GameObject.transform.SetParent(transform, false);
            faces = diceMesh.Faces;

            CreateFaceLabels(assets, dice, diceMesh.Faces, scale);

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
                        CreateFaceLabel(
                            assets, dice.Faces[spot.ValueFaceIndex].Value,
                            spot.Position + pose.Direction * FaceLabelSurfaceOffset, spot.Rotation, scale);
                    }
                }
                else
                {
                    CreateFaceLabel(
                        assets, dice.Faces[i].Value,
                        pose.Position + pose.Direction * FaceLabelSurfaceOffset, pose.LabelRotation, scale);
                }
            }
        }

        private void CreateFaceLabel(AssetDatabase assets, int value, Vector3 localPosition, Quaternion localRotation, float scale)
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

            if (assets.DiceFaceLabelFont != null) label.font = assets.DiceFaceLabelFont;
            if (assets.DiceFaceLabelMaterial != null) label.material = assets.DiceFaceLabelMaterial;
        }

        public void Rebind(DiceInstance dice)
        {
            // Repoints this view at the latest DiceInstance for the same dice (e.g. after a new round's GameState
            // replaces the old one) without recreating the view or re-running one-time setup
            this.DiceInstance = dice;
        }

        // ------------------------------ Animation

        public async Task AnimateGrow(CancellationToken ct)
        {
            await Animator.Play(AnimationSequenceBuilder
                .Start()
                .Next(AnimationTracks.LocalScale(
                    0.3f, transform, transform.localScale, Vector3.one, Easing.EaseOutCubic))
                .Build(), ct);
        }

        // targetDirection is the world direction the die's value-reading vertex/face should end up
        // pointing along (default matches GetUpFaceValue's world-up convention). Pass Vector3.down (or
        // whatever direction faces the camera) for a die that isn't resting on a surface being read from
        // above - e.g. a floating preview die where the labels need to face the viewer instead.
        public async Task AnimateRoll(CancellationToken ct, float durationMultiplier = 1f, Vector3? targetDirection = null)
        {
            // Land exactly on the rotation that reads as this die's current value, instead of an
            // arbitrary/uncontrolled resting orientation.
            Quaternion targetRotation = DiceMeshFactory.GetRotationForValue(faces, DiceInstance.Value, targetDirection);
            Quaternion startRotation = targetRotation;
            transform.rotation = startRotation;

            // Tumble around two of the local axes (always distinct, so never opposite/aligned) - one doing
            // roughly four full spins and the other roughly three, which reads as a natural dice roll.
            // These must be exact multiples of 360 degrees so the spin cancels out and the die actually
            // lands back on targetRotation instead of drifting to an unrelated angle.
            int firstAxis = Random.Range(0, 3);
            int secondAxis = (firstAxis + Random.Range(1, 3)) % 3;

            Vector3 spinAmount = Vector3.zero;
            spinAmount[firstAxis] = Random.Range(4, 6) * 360f * (Random.value < 0.5f ? -1f : 1f);
            spinAmount[secondAxis] = Random.Range(3, 5) * 360f * (Random.value < 0.5f ? -1f : 1f);

            float durationSeconds = 0.9f * durationMultiplier;

            await Animator.Play(AnimationSequenceBuilder
                .Start()
                .Next(AnimationTracks.LocalEulerRotation(
                    durationSeconds, transform, startRotation, spinAmount, Easing.EaseOutCubic))
                .Build(), ct);
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
            // Enable / disable outline, preferring the selected color over the hover color
            bool showOutline = IsSelected || (IsHovered && IsInteractable);
            outline.SetEnabled(showOutline);
            if (showOutline) outline.OutlineColor = IsSelected ? selectedOutlineColor : hoverOutlineColor;
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
