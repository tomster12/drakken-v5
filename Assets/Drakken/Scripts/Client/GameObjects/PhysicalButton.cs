using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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

    [Header("Config")]
    [SerializeField] private float hoverY = 0.4f;
    [SerializeField] private float heldY = -0.15f;
    [SerializeField] private float normLerp = 12f;
    [SerializeField] private float heldLerp = 40f;
    [SerializeField] private Color interactableColor;
    [SerializeField] private Color notInteractableColor;

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
    }

    private void Update()
    {
        if (mouse == null) return;

        Vector3 currentPosition = transform.position;

        outline.enabled = Interactable && isHovered;

        meshRenderer.material.color = Interactable ? interactableColor : notInteractableColor;

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
        if (isHovered && Interactable) Clicked?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData) => isHeld = false;
}
