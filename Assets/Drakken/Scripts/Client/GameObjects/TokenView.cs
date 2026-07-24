using Drakken.Common.Utility;
using Drakken.Domain.Tokens;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Drakken.Client.GameObjects
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
        public bool IsSelected { get; private set; }
        public Vector3 TargetPosition { get; set; }
        private TokenRegistry registry;
        private bool isHovered;
        private bool isInteractable = true;
        private float currentOffsetY;

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
            TargetPosition = transform.position;
            UpdateOutline();
        }

        private void Update()
        {
            float targetOffsetY = IsSelected ? selectedLiftY
                                : isHovered ? hoverLiftY
                                : 0f;

            currentOffsetY = Mathf.Lerp(currentOffsetY, targetOffsetY, liftLerp * Time.deltaTime);

            transform.position = new Vector3(TargetPosition.x, TargetPosition.y + currentOffsetY, TargetPosition.z);
        }

        private void Bind(TokenRegistry registry, TokenInstance instance)
        {
            this.registry = registry;
            TokenInstance = instance;

            if (!registry.TryGetMeshPrefab(instance.TokenId, out var meshPrefab))
            {
                Log.Warning("TokenView", $"No mesh prefab registered for tokenId='{instance.TokenId}'");
            }
            else
            {
                var meshGo = Instantiate(meshPrefab, transform);
                meshGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                outline = meshGo.GetComponent<Outline>() ?? meshGo.AddComponent<Outline>();
                AddPointerColliders(meshGo);
            }

            SetSelected(false);
        }

        // ------------------------------ Interaction

        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;

            if (!interactable)
            {
                isHovered = false;
                UpdateOutline();
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            UpdateOutline();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isInteractable) return;
            isHovered = true;
            UpdateOutline();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UpdateOutline();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isInteractable) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            OnClicked.Invoke(this);
        }

        public void OnPointerUp(PointerEventData eventData) { }

        private void UpdateOutline()
        {
            if (outline == null) return;

            bool shouldShow = IsSelected || (isHovered && isInteractable);
            outline.OutlineColor = IsSelected ? selectedOutlineColor : hoverOutlineColor;
            outline.enabled = shouldShow;
        }

        private static void AddPointerColliders(GameObject meshRoot)
        {
            foreach (var meshFilter in meshRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null) continue;

                var meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }
        }
    }
}
