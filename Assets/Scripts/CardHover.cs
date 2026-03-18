using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Manages the hover highlight (Y pop + yellow glow) for a hand card.
///
/// State machine:
///   _isHovered  — mouse is physically over this card (owned by pointer events)
///   _isDragging — this card is currently being dragged (owned by CardDrag events)
///
/// Visuals are derived: highlight is ON when either state is true.
/// This means the glow persists through a drag and clears cleanly on drop,
/// with no guard clauses needed in the event handlers.
///
/// Why Canvas.willRenderCanvases for the Y offset:
///   HorizontalLayoutGroup rebuilds anchoredPosition during the canvas rebuild
///   phase, which runs after LateUpdate. Hooking here ensures our Y override
///   always wins without flickering on relayout frames.
/// </summary>
public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float _hoverYOffset = 30f;
    [SerializeField] private float _lerpSpeed = 14f;
    [SerializeField] private Color _glowColor = new Color(1f, 0.85f, 0f, 0.45f);
    [SerializeField] private Vector2 _outlineDistance = new Vector2(10f, 10f);

    private RectTransform _rectTransform;
    private CardDrag _drag;
    private Outline _outline;
    private float _currentY;
    private float _targetY;
    private float _baseY;     // HLG-assigned y, cached when card is at rest
    private bool _isInHand;
    private bool _isHovered;
    private bool _isDragging;

    private bool ShowHighlight => _isHovered || _isDragging;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _drag = GetComponent<CardDrag>();
        CacheIsInHand();

        var img = GetComponent<Image>();
        if (img != null)
        {
            _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor = _glowColor;
            _outline.effectDistance = _outlineDistance;
            _outline.enabled = false;
        }
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases += ApplyYOffset;
        if (_drag != null)
        {
            _drag.OnDragBegin += HandleDragBegin;
            _drag.OnDragEnd   += HandleDragEnd;
        }
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= ApplyYOffset;
        if (_drag != null)
        {
            _drag.OnDragBegin -= HandleDragBegin;
            _drag.OnDragEnd   -= HandleDragEnd;
        }
    }

    private void OnTransformParentChanged() => CacheIsInHand();

    private void CacheIsInHand()
    {
        _isInHand = _rectTransform != null &&
                    _rectTransform.parent != null &&
                    _rectTransform.parent.GetComponent<HorizontalLayoutGroup>() != null;
    }

    // --- Pointer events ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        UpdateVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        UpdateVisuals();
    }

    // --- Drag events ---

    private void HandleDragBegin(CardDrag drag)
    {
        _isDragging = true;
        UpdateVisuals();
    }

    private void HandleDragEnd(CardDrag drag)
    {
        _isDragging = false;
        _isHovered = false; // pointer may have moved; OnPointerEnter will re-set if still over
        UpdateVisuals();
    }

    // --- Visuals ---

    private void UpdateVisuals()
    {
        if (_outline != null) _outline.enabled = ShowHighlight;
        _targetY = ShowHighlight ? _hoverYOffset : 0f;
    }

    private void Update()
    {
        if (!_isInHand)
        {
            _currentY = 0f;
            return;
        }

        _currentY = Mathf.Lerp(_currentY, _targetY, Time.deltaTime * _lerpSpeed);
    }

    private void ApplyYOffset()
    {
        if (!_isInHand) return;

        if (Mathf.Approximately(_currentY, 0f))
        {
            // At rest: let HLG fully own the position and cache it as our base.
            _baseY = _rectTransform.anchoredPosition.y;
            return;
        }

        // Hovering/animating: write an absolute value so we never accumulate.
        _rectTransform.anchoredPosition = new Vector2(
            _rectTransform.anchoredPosition.x,
            _baseY + _currentY);
    }
}
