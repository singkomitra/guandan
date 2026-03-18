using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Raises a card upward and shows a yellow glow outline when the pointer hovers over it.
///
/// Why Canvas.willRenderCanvases instead of LateUpdate:
///   HorizontalLayoutGroup rebuilds anchoredPosition during the canvas rebuild phase,
///   which runs AFTER LateUpdate. If we set anchoredPosition in LateUpdate, HLG
///   overrides it on any rebuild frame (drag start/end), causing a visible flicker.
///   Canvas.willRenderCanvases fires after all layout rebuilds, so our override
///   always wins cleanly.
/// </summary>
public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float _hoverYOffset    = 30f;
    [SerializeField] private float _selectedYOffset = 50f;
    [SerializeField] private float _lerpSpeed = 14f;
    [SerializeField] private Color _glowColor = new Color(1f, 0.85f, 0f, 0.45f);
    [SerializeField] private Vector2 _outlineDistance = new Vector2(5f, 5f);

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Outline _outline;
    private float _currentY;
    private float _targetY;
    private bool _isInHand;
    private bool _isHovered;
    private bool _isDragging;
    private bool _isSelected;

    private bool ShowHighlight => _isHovered || _isDragging;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
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

    private void OnTransformParentChanged() => CacheIsInHand();

    private void CacheIsInHand()
    {
        _isInHand = _rectTransform != null &&
                    _rectTransform.parent != null &&
                    _rectTransform.parent.GetComponent<HorizontalLayoutGroup>() != null;
    }

    private void OnEnable()  => Canvas.willRenderCanvases += ApplyYOffset;
    private void OnDisable() => Canvas.willRenderCanvases -= ApplyYOffset;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsDragging()) return;
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

    // --- Selection (driven by CardSelectable) ---

    /// <summary>
    /// Called by CardSelectable when this card's selection state changes.
    /// Selected wins over hovered: suppresses yellow glow and uses the larger Y offset.
    /// </summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateVisuals();
    }

    // --- Visuals ---

    private void UpdateVisuals()
    {
        // Yellow glow only shows when hovering/dragging and NOT selected (ring takes over).
        if (_outline != null) _outline.enabled = ShowHighlight && !_isSelected;
        _targetY = _isSelected ? _selectedYOffset : (ShowHighlight ? _hoverYOffset : 0f);
    }

    private void Update()
    {
        if (IsDragging() || !_isInHand)
        {
            ResetHover();
            _currentY = 0f;
            return;
        }

        _currentY = Mathf.Lerp(_currentY, _targetY, Time.deltaTime * _lerpSpeed);
    }

    /// <summary>
    /// Runs after HLG rebuilds — safe to override anchoredPosition.y here.
    /// Always writes so HLG can never win on dirty frames (drag start/end, relayout).
    /// Setting anchoredPosition on a child does NOT trigger a parent LayoutGroup rebuild,
    /// so there is no feedback loop.
    /// </summary>
    private void ApplyYOffset()
    {
        if (!_isInHand) return;

        var pos = _rectTransform.anchoredPosition;
        _rectTransform.anchoredPosition = new Vector2(pos.x, _currentY);
    }

    private void ResetHover()
    {
        _targetY = 0f;
        if (_outline != null) _outline.enabled = false;
    }


    private bool IsDragging()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
        return _canvasGroup != null && !_canvasGroup.blocksRaycasts;
    }
}
