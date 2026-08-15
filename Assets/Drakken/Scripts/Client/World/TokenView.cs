using Drakken.Domain.Animation;
using Drakken.Common.Utility;
using Drakken.Domain.Tokens;
using Drakken.Utility;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Drakken.Client.World
{
    public class TokenView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        public UnityEvent<TokenView> OnClicked = new();
        public UnityEvent<TokenView> OnDragStarted = new();
        public UnityEvent<TokenView> OnDragMoved = new();
        public UnityEvent<TokenView> OnDragEnded = new();

        [Header("References")]
        [SerializeField] private Outline outline;
        [SerializeField] private TextMeshPro titleText;
        [SerializeField] private Canvas descriptionCanvas;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private VerticalLayoutGroup descriptionVlg;

        [Header("Config")]
        [SerializeField] private float hoverLiftY = 0.15f;
        [SerializeField] private float selectedLiftY = 0.30f;
        [SerializeField] private Color discardHoverOutlineColor = Colors.Hex("#e8aba8");
        [SerializeField] private Color discardSelectedOutlineColor = Colors.Hex("#9e302b");
        [SerializeField] private Color discardHoverSelectedOutlineColor = Colors.Hex("#e4453d");
        [SerializeField] private Color playingHoverOutlineColor = Colors.Hex("#d4cdcb");
        [SerializeField] private Color playingSelectedOutlineColor = Colors.Hex("#ece9e8");
        [SerializeField] private Color playingHoverSelectedOutlineColor = Colors.Hex("#fcf9f8");
        [SerializeField] private Color playingPrimedOutlineColor = Colors.Hex("#a1f57c");

        [Header("Movement")]
        [SerializeField] private float moveLerp = 10f;
        [SerializeField] private float liftLerp = 20f;

        public TokenDefinition TokenDefinition { get; private set; }
        public TokenInstance TokenInstance { get; private set; }
        public InteractionModeType InteractionMode { get; private set; } = InteractionModeType.None;
        public AnimationPlayer Animator { get; private set; } = new();
        private bool isInteractionLocked = false;
        public bool IsInteractable => InteractionMode != InteractionModeType.None && !Animator.IsAnimating && !isInteractionLocked;
        public bool IsBinded => TokenInstance != null;
        public bool IsSelected { get; private set; } = false;
        public bool IsHovered { get; private set; } = false;
        public bool IsDragging { get; private set; } = false;
        public bool IsHidden { get; private set; } = false;
        private GameObject tokenMesh;
        private GameObject blankMesh;
        private Vector3 currentPosition;
        private float currentLiftOffsetY;
        private Camera dragRaycastCamera;
        private Plane dragPlane;
        private bool isPrimedToPlay;

        private Color HoverOutlineColor => InteractionMode switch
        {
            InteractionModeType.Discard => discardHoverOutlineColor,
            InteractionModeType.Play => playingHoverOutlineColor,
            _ => Color.black
        };

        private Color SelectedOutlineColor => InteractionMode switch
        {
            InteractionModeType.Discard => discardSelectedOutlineColor,
            InteractionModeType.Play => playingSelectedOutlineColor,
            _ => Color.black
        };

        private Color HoverSelectedOutlineColor => InteractionMode switch
        {
            InteractionModeType.Discard => discardHoverSelectedOutlineColor,
            InteractionModeType.Play => playingHoverSelectedOutlineColor,
            _ => Color.black
        };

        private Color PrimedOutlineColor => InteractionMode switch
        {
            InteractionModeType.Play => playingPrimedOutlineColor,
            _ => Color.black
        };

        // ------------------------------ Lifetime

        public static TokenView Create(AssetDatabase assets, TokenRegistry registry, TokenInstance instance, bool hidden = false)
        {
            var prefab = assets.TokenViewPrefab;
            var tokenGo = Instantiate(prefab);
            tokenGo.gameObject.SetActive(true);
            var tokenView = tokenGo.GetComponent<TokenView>();

            tokenView.Bind(assets, registry, instance, hidden);

            return tokenView;
        }

        private void Bind(AssetDatabase assets, TokenRegistry registry, TokenInstance instance, bool hidden = false)
        {
            TokenInstance = instance;
            IsHidden = hidden;

            var entry = registry.GetEntryOrThrow(instance.TokenId);

            TokenDefinition = entry.Definition;

            tokenMesh = Instantiate(entry.Visuals.MeshPrefab, transform);
            tokenMesh.SetActive(true);
            tokenMesh.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (hidden)
            {
                blankMesh = Instantiate(assets.EmptyTokenMeshPrefab, transform);
                blankMesh.SetActive(true);
                blankMesh.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            titleText.text = TokenDefinition.DisplayName;
            titleText.gameObject.SetActive(!hidden);

            descriptionCanvas.gameObject.SetActive(false);
            descriptionText.text = TokenDefinition.Description;

            outline.Setup();
        }

        [ContextMenu("Bind Random")]
        private void DebugBindRandom()
        {
            GameEntrypoint.Singleton.TokenRegistry ??=
                TokenRegistryBuilder.BuildClientRegistry(GameEntrypoint.Singleton.Client.Assets.GetTokenMeshPrefabById);

            var tokenId = GameEntrypoint.Singleton.TokenRegistry.AllDefinitions
                .ToList()
                .ShuffleInplace()
                .First()
                .TokenId;

            var tokenInstance = TokenInstance.Create(tokenId);

            Bind(
                GameEntrypoint.Singleton.Client.Assets,
                GameEntrypoint.Singleton.TokenRegistry,
                tokenInstance);
        }

        public void Reveal()
        {
            Assert.True(IsHidden);

            titleText.gameObject.SetActive(true);

            tokenMesh.SetActive(true);
            blankMesh.SetActive(false);
        }

        private void Awake()
        {
            currentPosition = transform.localPosition;
            dragRaycastCamera = Camera.main;
        }

        private void Update()
        {
            UpdateInteraction();
        }

        // ------------------------------ Animation

        public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            currentPosition = position;
            transform.rotation = rotation;
        }

        public IAnimationTrack CreateCurrentPositionAnimationTrack(
            float durationSeconds, Func<float, Vector3> getPositionFunc, Func<float, float> easingFunc = null)
        {
            easingFunc ??= Easing.Linear;
            return new AnimationTrack<Vector3>(
                durationSeconds,
                normalizedTime => getPositionFunc(easingFunc(normalizedTime)),
                position => currentPosition = position);
        }

        public async Task AnimateShrinkAfter(float delaySeconds, CancellationToken ct)
        {
            if (delaySeconds > 0f)
                await Task.Delay((int)(delaySeconds * 1000f));

            await Animator.Play(AnimationSequenceBuilder.Start()
                .Next(AnimationTracks.LocalScale(
                    0.3f, transform, transform.localScale, Vector3.zero, Easing.EaseInCubic))
                .Build(), ct);
        }

        // ------------------------------ Interaction

        private void UpdateInteraction()
        {
            var hoveredAndInteractable = IsHovered && IsInteractable;

            outline.SetEnabled(IsSelected || hoveredAndInteractable || IsDragging);
            if (outline.IsEnabled)
            {
                outline.OutlineColor = IsDragging
                    ? (isPrimedToPlay ? PrimedOutlineColor : HoverOutlineColor)
                    : IsSelected
                        ? (IsHovered ? HoverSelectedOutlineColor : SelectedOutlineColor)
                        : HoverOutlineColor;
            }

            var showDescription = hoveredAndInteractable && !IsDragging;

            if (!descriptionCanvas.gameObject.activeSelf && showDescription)
            {
                descriptionCanvas.gameObject.SetActive(true);

                // Force text to regenerate and the layout to rebuild
                descriptionText.ForceMeshUpdate();
                LayoutRebuilder.ForceRebuildLayoutImmediate(descriptionVlg.transform as RectTransform);
            }
            else if (descriptionCanvas.gameObject.activeSelf && !showDescription)
            {
                descriptionCanvas.gameObject.SetActive(false);
            }


            float targetLiftOffsetY =
                IsDragging ? 0f
                : IsSelected ? selectedLiftY
                : hoveredAndInteractable ? hoverLiftY
                : 0f;

            currentLiftOffsetY = Mathf.Lerp(
                currentLiftOffsetY,
                targetLiftOffsetY,
                liftLerp * Time.deltaTime);

            transform.position = new Vector3(
                currentPosition.x,
                currentPosition.y + currentLiftOffsetY,
                currentPosition.z);
        }

        public void SetInteractionLocked(bool interactionLocked)
        {
            isInteractionLocked = interactionLocked;
        }

        public bool SetSelected(bool selected)
        {
            if (IsInteractable && IsSelected != selected)
            {
                IsSelected = selected;
                return true;
            }

            return false;
        }

        public void SetPrimedToPlay(bool isPrimedToPlay)
        {
            this.isPrimedToPlay = isPrimedToPlay;
        }

        public void SetInteractionMode(InteractionModeType interactionModeType)
        {
            Assert.False(IsDragging);

            InteractionMode = interactionModeType;
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
            if (IsInteractable)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    OnClicked.Invoke(this);
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData) { }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (InteractionMode != InteractionModeType.Play) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            currentLiftOffsetY = 0f;

            IsDragging = true;
            isPrimedToPlay = false;
            dragPlane = new Plane(Vector3.up, currentPosition + Vector3.up * 0.8f);

            var dragRay = dragRaycastCamera.ScreenPointToRay(eventData.position);
            if (dragPlane.Raycast(dragRay, out float rayDistance))
            {
                currentPosition = dragRay.GetPoint(rayDistance);
            }

            OnDragStarted.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            var dragRay = dragRaycastCamera.ScreenPointToRay(eventData.position);
            if (dragPlane.Raycast(dragRay, out float rayDistance))
            {
                currentPosition = dragRay.GetPoint(rayDistance);
            }

            OnDragMoved.Invoke(this);
        }

        void OnDrawGizmos()
        {
            Gizmos.DrawCube(dragPlane.ClosestPointOnPlane(currentPosition), Vector3.one * 0.4f);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            IsDragging = false;

            OnDragEnded.Invoke(this);
        }

        public enum InteractionModeType { None, Discard, Play };
    }
}
