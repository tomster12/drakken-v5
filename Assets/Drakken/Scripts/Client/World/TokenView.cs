using Drakken.Common.Utility;
using Drakken.Domain.Tokens;
using System.Linq;
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

        [Header("Hover / Select Offsets")]
        [SerializeField] private float hoverLiftY = 0.15f;
        [SerializeField] private float selectedLiftY = 0.30f;
        [SerializeField] private Color hoverOutlineColor = Color.white;
        [SerializeField] private Color selectedOutlineColor = Color.yellow;

        [Header("Movement")]
        [SerializeField] private float moveLerp = 10f;
        [SerializeField] private float liftLerp = 20f;

        public TokenInstance TokenInstance { get; private set; }
        public Vector3 TargetLocalPosition { get; set; }
        public bool IsBinded => TokenInstance != null;
        public bool IsSelected { get; private set; } = false;
        public bool IsHovered { get; private set; } = false;
        public bool IsInteractable { get; set; } = false;
        private bool toUpdateOutline = false;

        // ------------------------------ Setup

        public static TokenView Create(AssetDatabase assets, TokenRegistry registry, TokenInstance instance, Transform parent = null)
        {
            var prefab = assets.TokenViewPrefab;
            var tokenGo = Instantiate(prefab, parent);
            var tokenView = tokenGo.GetComponent<TokenView>();

            tokenView.Bind(registry, instance);

            return tokenView;
        }

        private void Awake()
        {
            TargetLocalPosition = transform.localPosition;
        }

        private void Bind(TokenRegistry registry, TokenInstance instance)
        {
            TokenInstance = instance;

            if (!registry.TryGetMeshPrefab(instance.TokenId, out var meshPrefab))
            {
                Log.Error("TokenView", $"No mesh prefab registered for tokenId='{instance.TokenId}'");
                return;
            }

            var meshGo = Instantiate(meshPrefab, transform);
            meshGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            toUpdateOutline = true;
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
            // Calculate offset from target with lift
            float targetOffsetY = IsSelected ? selectedLiftY
                                : IsHovered ? hoverLiftY
                                : 0f;

            var targetLocalPosition = new Vector3(
                TargetLocalPosition.x,
                TargetLocalPosition.y + targetOffsetY,
                TargetLocalPosition.z);

            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                targetLocalPosition,
                moveLerp * Time.deltaTime);

            // Update outline if flagged
            if (toUpdateOutline)
            {
                toUpdateOutline = false;
                UpdateOutline();
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (IsInteractable == interactable) return;

            IsInteractable = interactable;

            if (!interactable && IsHovered)
            {
                IsHovered = false;
                toUpdateOutline = true;
            }
        }

        public void SetSelected(bool selected)
        {
            if (IsInteractable && !selected)
            {
                IsSelected = selected;
                toUpdateOutline = true;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsInteractable && !IsHovered)
            {
                IsHovered = true;
                toUpdateOutline = true;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (IsHovered)
            {
                IsHovered = false;
                toUpdateOutline = true;
            }
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

        private void UpdateOutline()
        {
            if (outline == null) return;

            bool shouldShow = IsSelected || (IsHovered && IsInteractable);
            outline.enabled = shouldShow;

            if (shouldShow)
            {
                outline.OutlineColor = IsSelected ? selectedOutlineColor : hoverOutlineColor;
            }
        }
    }
}
