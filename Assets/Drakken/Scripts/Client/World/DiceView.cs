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
        private float FaceLabelFontSize = 1.3f;
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

        public static DiceView Create(AssetDatabase assets, DiceInstance instance)
        {
            GameObject diceGo = new($"Dice View (D{instance.Sides})");
            var diceView = diceGo.AddComponent<DiceView>();

            diceView.Bind(assets, instance);

            return diceView;
        }

        private void Bind(AssetDatabase assets, DiceInstance dice)
        {
            this.DiceInstance = dice;

            var diceMesh = DiceMeshFactory.Create(dice, assets.DiceMeshMaterial);
            diceMesh.GameObject.transform.SetParent(transform, false);
            faces = diceMesh.Faces;

            CreateFaceLabels(assets, dice, diceMesh.Faces);

            outline = gameObject.AddComponent<Outline>();
            outline.Setup();
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
                if (assets.DiceFaceLabelMaterial != null) label.material = assets.DiceFaceLabelMaterial;
            }
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
