using Drakken.Domain.Tokens;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

namespace Drakken.Client.GameObjects
{
    public class TokenView : MonoBehaviour
    {
        public UnityEvent<TokenView> OnClicked;

        [Header("References")]
        [SerializeField] private Outline outline;
        [SerializeField] private new Renderer renderer;
        [SerializeField] private TextMeshPro labelName;
        [SerializeField] private TextMeshPro labelDescription;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Config")]
        [SerializeField] private Color colorNormal = new(0.19f, 0.15f, 0.133f);
        [SerializeField] private Color colorDisabled = new(0.35f, 0.35f, 0.35f);
        [SerializeField] private float hoverLiftY = 0.15f;
        [SerializeField] private float selectedLiftY = 0.30f;
        [SerializeField] private float lerpSpeed = 12f;

        public TokenInstance TokenInstance { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsInteractable { get; private set; } = true;

        private Vector3 basePosition;
        private bool isHovered;

        private void Awake()
        {
            basePosition = transform.localPosition;
            if (outline != null) outline.enabled = false;
        }

        public void Bind(TokenInstance instance, TokenDefinition definition, Sprite mesh = null)
        {
            TokenInstance = instance;
            if (labelName != null) labelName.text = definition?.DisplayName ?? instance.TokenId;
            if (labelDescription != null) labelDescription.text = definition?.Description ?? "";
            if (spriteRenderer != null && mesh != null) spriteRenderer.sprite = mesh;
            SetSelected(false);
        }

        public void SetInteractable(bool interactable)
        {
            IsInteractable = interactable;
            if (renderer != null)
                renderer.material.color = interactable ? colorNormal : colorDisabled;

            // Force-clear hover/select state when disabled
            if (!interactable)
            {
                isHovered = false;
                SetSelected(false);
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (outline != null) outline.enabled = selected;
        }

        private void Update()
        {
            // --- Input (only when interactable) ---
            if (IsInteractable)
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                bool hit = Physics.Raycast(ray, out var hitInfo)
                           && hitInfo.collider.gameObject == gameObject;

                if (hit != isHovered)
                    isHovered = hit;

                if (hit && Input.GetMouseButtonDown(0))
                    OnClicked.Invoke(this);
            }

            // --- Position (always lerp, even after deselect) ---
            float targetY = 0f;
            if (IsSelected) targetY = selectedLiftY;
            else if (isHovered) targetY = hoverLiftY;

            var target = basePosition + Vector3.up * targetY;
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, target, lerpSpeed * Time.deltaTime);
        }
    }
}