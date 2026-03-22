using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Manages the hover highlight (yellow glow) and render priority for a hand card.
/// Y-axis lift is now owned entirely by HandManager, which listens to AnyHoverChanged
/// and recalculates card target positions. This class is purely visual.
/// </summary>
public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ICanvasRaycastFilter
{
    [SerializeField] private Color   _glowColor       = new Color(1f, 0.85f, 0f, 0.45f);
    [SerializeField] private Vector2 _outlineDistance  = new Vector2(10f, 10f);
    [Tooltip("Must match HandManager._selectedYOffset. Non-elevated cards reject raycasts in this top band.")]
    [SerializeField] private float   _rejectionBand   = 50f;

    private RectTransform _rectTransform;
    private Card          _card;
    private Outline       _outline;
    private Canvas        _overrideCanvas;
    private bool          _isHovered;
    private bool          _isSelected;

    /// <summary>
    /// Fired when the pointer enters or leaves this card.
    /// HandManager subscribes to update Y targets without CardHover touching positions.
    /// </summary>
    public static event Action<Card.CardId, bool> AnyHoverChanged;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
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
        bool elevated = _isHovered || _isSelected;

        // Yellow glow only when hovering and not already selected (ring takes over).
        if (_outline != null) _outline.enabled = _isHovered && !_isSelected;

        // Override sorting gives elevated cards higher render priority so they
        // win depth-based raycasts against overlapping neighbours.
        if (_overrideCanvas == null)
            _overrideCanvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        if (_overrideCanvas == null) return;
        _overrideCanvas.overrideSorting = elevated;
        _overrideCanvas.sortingOrder    = elevated ? 1 : 0;
    }

    // Non-elevated cards reject the top band so elevated neighbours win those hits.
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (_isSelected || _isHovered) return true;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, screenPoint, eventCamera, out Vector2 local);

        return local.y <= _rectTransform.rect.yMax - _rejectionBand;
    }
}
