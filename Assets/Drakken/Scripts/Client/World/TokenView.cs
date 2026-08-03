using Drakken.Common.Utility;
using Drakken.Domain.Tokens;
using System;
using System.Linq;
using TMPro;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Drakken.Client.World
{
    public class TokenView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        public UnityEvent<TokenView> OnClicked = new();

        [Header("References")]
        [SerializeField] private Outline outline;
        [SerializeField] private TextMeshPro titleText;
        [SerializeField] private Canvas descriptionCanvas;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Config")]
        [SerializeField] private float hoverLiftY = 0.15f;
        [SerializeField] private float selectedLiftY = 0.30f;
        [SerializeField] private Color discardHoverOutlineColor = new(232, 171, 168); // #e8aba8
        [SerializeField] private Color discardSelectedOutlineColor = new(158, 48, 43); // #9e302b
        [SerializeField] private Color discardHoverSelectedOutlineColor = new(228, 69, 61); // #e4453d
        [SerializeField] private Color playingHoverOutlineColor = new(212, 205, 203); // #d4cdcb
        [SerializeField] private Color playingSelectedOutlineColor = new(236, 233, 232); // #ece9e8
        [SerializeField] private Color playingHoverSelectedOutlineColor = new(252, 249, 248); // #fcf9f8

        [Header("Movement")]
        [SerializeField] private float moveLerp = 10f;
        [SerializeField] private float liftLerp = 20f;

        public TokenDefinition TokenDefinition { get; private set; }
        public TokenInstance TokenInstance { get; private set; }
        public Vector3 TargetLocalPosition { get; set; }
        public bool IsBinded => TokenInstance != null;
        public bool IsSelected { get; private set; } = false;
        public bool IsHovered { get; private set; } = false;
        public InteractionModeType InteractionMode { get; set; } = InteractionModeType.None;

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

        // ------------------------------ Setup

        public static TokenView Create(AssetDatabase assets, TokenRegistry registry, TokenInstance instance, Transform parent = null)
        {
            var prefab = assets.TokenViewPrefab;
            var tokenGo = Instantiate(prefab, parent);
            tokenGo.gameObject.SetActive(true);
            var tokenView = tokenGo.GetComponent<TokenView>();

            tokenView.Bind(registry, instance);

            return tokenView;
        }

        public static TokenView CreateEmpty(AssetDatabase assets, Transform parent = null)
        {
            var prefab = assets.TokenViewPrefab;
            var tokenGo = Instantiate(prefab, parent);
            tokenGo.gameObject.SetActive(true);
            var tokenView = tokenGo.GetComponent<TokenView>();

            tokenView.BindEmpty(assets.EmptyTokenMeshPrefab);

            return tokenView;
        }

        private void Awake()
        {
            TargetLocalPosition = transform.localPosition;
        }

        private void Bind(TokenRegistry registry, TokenInstance instance)
        {
            TokenInstance = instance;

            var entry = registry.GetEntryOrThrow(instance.TokenId);

            TokenDefinition = entry.Definition;

            var meshGo = Instantiate(entry.Visuals.MeshPrefab, transform);
            meshGo.SetActive(true);
            meshGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            titleText.text = TokenDefinition.DisplayName;
            titleText.gameObject.SetActive(true);

            descriptionCanvas.gameObject.SetActive(false);

            descriptionText.text = TokenDefinition.Description;

            outline.Setup();
        }

        private void BindEmpty(GameObject prefab)
        {
            TokenInstance = null;
            TokenDefinition = null;

            var meshGo = Instantiate(prefab, transform);
            meshGo.SetActive(true);
            meshGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            titleText.text = null;
            titleText.gameObject.SetActive(false);

            descriptionCanvas.gameObject.SetActive(false);

            outline.Setup();
        }

        [ContextMenu("Bind Random")]
        private void DebugBindRandom()
        {
            var registry = TokenRegistryBuilder.BuildClientRegistry(GameEntrypoint.Singleton.Client.Assets.GetTokenPrefabById);

            var tokenId = registry.AllDefinitions
                .ToList()
                .ShuffleInplace()
                .First()
                .TokenId;

            var tokenInstance = TokenInstance.Create(tokenId);

            Bind(registry, tokenInstance);
        }

        // ------------------------------ Interaction

        private void Update()
        {
            // Calculate height offset
            float targetOffsetY = IsSelected ? selectedLiftY
                                : IsHovered ? hoverLiftY
                                : 0f;

            // Lerp towards target position
            var targetLocalPosition = new Vector3(
                TargetLocalPosition.x,
                TargetLocalPosition.y + targetOffsetY,
                TargetLocalPosition.z);

            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                targetLocalPosition,
                moveLerp * Time.deltaTime);

            // Update outline
            outline.SetEnabled(IsSelected || (IsHovered && InteractionMode != InteractionModeType.None));

            if (outline.IsEnabled)
            {
                outline.OutlineColor = IsSelected
                    ? (IsHovered ? HoverSelectedOutlineColor : SelectedOutlineColor)
                    : HoverOutlineColor;
            }

            // Update description
            descriptionCanvas.gameObject.SetActive(IsHovered);

            if (IsHovered)
            {
                // Set its position to relative to the title
                descriptionCanvas.transform.SetPositionAndRotation(
                    titleText.transform.position + new Vector3(0, -0.1f, 0.22f),
                    Quaternion.Euler(90.0f, 0, 0));
            }
        }

        public bool SetSelected(bool selected)
        {
            if (InteractionMode != InteractionModeType.None && IsSelected != selected)
            {
                IsSelected = selected;
                return true;
            }

            return false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (InteractionMode != InteractionModeType.None && !IsHovered)
            {
                IsHovered = true;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (IsHovered)
            {
                IsHovered = false;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (InteractionMode != InteractionModeType.None)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    OnClicked.Invoke(this);
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData) { }

        public enum InteractionModeType { None, Discard, Play };
    }
}
