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
