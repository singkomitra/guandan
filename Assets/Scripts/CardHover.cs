using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Manages the hover highlight (yellow glow) and render priority for a hand card.
/// Y-axis lift is now owned entirely by HandManager, which listens to AnyHoverChanged
/// and recalculates card target positions. This class is purely visual.
/// </summary>
public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Color   _glowColor       = new Color(1f, 0.85f, 0f, 0.45f);
    [SerializeField] private Vector2 _outlineDistance  = new Vector2(10f, 10f);
    private Card            _card;
    private Outline         _outline;
    private bool            _isHovered;
    private bool            _isSelected;

    /// <summary>
    /// Fired when the pointer enters or leaves this card.
    /// HandManager subscribes to update Y targets without CardHover touching positions.
    /// </summary>
    public static event Action<Card.CardId, bool> AnyHoverChanged;

    /// <summary>Set by CardDrag — suppresses hover effects while any card is being dragged.</summary>
    public static bool IsDragging { get; private set; }

    private void OnEnable()
    {
        CardDrag.AnyDragBegin += OnAnyDragBegin;
        CardDrag.AnyDragEnd   += OnAnyDragEnd;
    }

    private void OnDisable()
    {
        CardDrag.AnyDragBegin -= OnAnyDragBegin;
        CardDrag.AnyDragEnd   -= OnAnyDragEnd;
    }

    private void OnAnyDragBegin(CardDrag _)
    {
        IsDragging = true;
        // Clear hover state on this card so it doesn't stay lit while another card is dragged.
        if (_isHovered)
        {
            _isHovered = false;
            UpdateVisuals();
            if (_card != null) AnyHoverChanged?.Invoke(_card.Id, false);
        }
    }

    private void OnAnyDragEnd(CardDrag _)
    {
        IsDragging = false;
    }

    private void Awake()
    {
        _card = GetComponent<Card>();

        var img = GetComponent<Image>();
        if (img != null)
        {
            _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor    = _glowColor;
            _outline.effectDistance = _outlineDistance;
            _outline.enabled        = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsDragging) return;
        _isHovered = true;
        UpdateVisuals();
        if (_card != null) AnyHoverChanged?.Invoke(_card.Id, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        UpdateVisuals();
        if (_card != null) AnyHoverChanged?.Invoke(_card.Id, false);
    }

    /// <summary>Called by CardSelectable when this card's selection state changes.</summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        // Yellow glow only when hovering and not already selected (ring takes over).
        if (_outline != null) _outline.enabled = _isHovered && !_isSelected;
    }

}
