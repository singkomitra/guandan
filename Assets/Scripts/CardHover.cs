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
    [SerializeField] private float _hoverYOffset = 30f;
    [SerializeField] private float _lerpSpeed = 14f;
    [SerializeField] private Color _glowColor = new Color(1f, 0.85f, 0f, 0.45f);
    [SerializeField] private Vector2 _outlineDistance = new Vector2(5f, 5f);

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Outline _outline;
    private float _currentY;
    private float _targetY;
    private bool _isInHand;

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
        _targetY = _hoverYOffset;
        if (_outline != null) _outline.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetHover();
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
