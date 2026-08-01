using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;


namespace Drakken.Client.World
{
    public class PhysicalButton : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        public event Action Clicked;

        public bool Interactable { get; set; } = true;

        [Header("References")]
        [SerializeField] private Outline outline;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private TextMeshPro text;

        [Header("Config")]
        [SerializeField] private float hoverY = 0.4f;
        [SerializeField] private float heldY = -0.15f;
        [SerializeField] private float normLerp = 12f;
        [SerializeField] private float heldLerp = 40f;
        [SerializeField] private Color interactableColor;
        [SerializeField] private Color notInteractableColor;
        [SerializeField] private Color interactableTextColor;
        [SerializeField] private Color notInteractableTextColor;
        [SerializeField] private Color highlightedTextColor;

        private float initialY;
        private bool isHovered;
        private bool isHeld;
        private Mouse mouse;

        private void Awake()
        {
            initialY = transform.position.y;
            isHovered = false;
            isHeld = false;
            mouse = Mouse.current;
            
            outline.Setup();
        }

        private void Update()
        {
            if (mouse == null) return;

            Vector3 currentPosition = transform.position;

            var isActive = (Interactable && isHovered) || isHeld;

            outline.SetEnabled(isActive);

            meshRenderer.material.color =
                Interactable
                ? interactableColor
                : notInteractableColor;

            text.color =
                isActive
                ? highlightedTextColor
                : (Interactable
                    ? interactableTextColor
                    : notInteractableTextColor);

            if (isHeld)
            {
                currentPosition.y = Mathf.Lerp(transform.position.y, initialY + heldY, heldLerp * Time.deltaTime);
            }
            else if (Interactable && isHovered)
            {
                currentPosition.y = Mathf.Lerp(transform.position.y, initialY + hoverY, normLerp * Time.deltaTime);
            }
            else
            {
                currentPosition.y = Mathf.Lerp(transform.position.y, initialY, normLerp * Time.deltaTime);
            }

            transform.position = currentPosition;
        }

        public void OnPointerEnter(PointerEventData eventData) => isHovered = true;

        public void OnPointerExit(PointerEventData eventData) => isHovered = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Interactable) isHeld = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isHovered && isHeld) Clicked?.Invoke();
            isHeld = false;
        }
    }
}
