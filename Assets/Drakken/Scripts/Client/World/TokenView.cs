using Drakken.Common.Utility;
using Drakken.Domain.Tokens;
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
        [SerializeField] private Color hoverOutlineColor = Color.white;
        [SerializeField] private Color selectedOutlineColor = Color.yellow;
        [SerializeField] private Color hoverSelectedOutlineColor = Color.yellow;

        [Header("Movement")]
        [SerializeField] private float moveLerp = 10f;
        [SerializeField] private float liftLerp = 20f;

        public TokenDefinition TokenDefinition { get; private set; }
        public TokenInstance TokenInstance { get; private set; }
        public Vector3 TargetLocalPosition { get; set; }
        public bool IsBinded => TokenInstance != null;
        public bool IsSelected { get; private set; } = false;
        public bool IsHovered { get; private set; } = false;
        public bool IsInteractable { get; set; } = false;

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

            if (!registry.TryGetMeshPrefab(instance.TokenId, out var meshPrefab))
            {
                Log.Error("TokenView", $"No mesh prefab registered for tokenId='{instance.TokenId}'");
                return;
            }

            if (!registry.TryGetDefinition(instance.TokenId, out var tokenDefinition))
            {
                Log.Error("TokenView", $"No token definition found for tokenId='{instance.TokenId}'");
                return;
            }

            TokenDefinition = tokenDefinition;

            var meshGo = Instantiate(meshPrefab, transform);
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
            outline.SetEnabled(IsSelected || (IsHovered && IsInteractable));

            if (outline.IsEnabled)
            {
                outline.OutlineColor = IsSelected
                    ? (IsHovered ? hoverSelectedOutlineColor : selectedOutlineColor)
                    : hoverOutlineColor;
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

        public void SetInteractable(bool interactable)
        {
            if (IsInteractable == interactable) return;

            IsInteractable = interactable;

            if (!interactable && IsHovered)
            {
                IsHovered = false;
            }
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsInteractable && !IsHovered)
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
            if (IsInteractable)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    OnClicked.Invoke(this);
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData) { }
    }
}
