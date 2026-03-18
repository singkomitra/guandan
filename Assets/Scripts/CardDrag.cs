using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform _rectTransform;
    private Canvas _rootCanvas;
    private CanvasGroup _canvasGroup;
    private Transform _startParent;
    private int _startSiblingIndex;
    private GameObject _placeholder;
    private LayoutElement _layoutElement;
    private bool _addedLayoutElement;
    private bool _droppedOnZone;
    private bool _wasInHand;

    public event Action<CardDrag> OnDragEnd;

    public RectTransform CardRect       => _rectTransform;
    public Transform     StartParent    => _startParent;
    public bool          WasDropHandled => _droppedOnZone;
    public bool          IsFromHand     => _wasInHand;
    public int PlaceholderSiblingIndex  => _placeholder != null
        ? _placeholder.transform.GetSiblingIndex()
        : _startSiblingIndex;

    public void NotifyDropHandled()
    {
        _droppedOnZone = true;
        if (_layoutElement != null) _layoutElement.ignoreLayout = false;
    }

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _startParent = transform.parent;
        _startSiblingIndex = transform.GetSiblingIndex();
        _droppedOnZone = false;

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;

        _wasInHand = _startParent.GetComponent<HorizontalLayoutGroup>() != null;
        if (_wasInHand)
        {
            SetSiblingRaycast(_startParent, false);

            var ph = new GameObject("CardPlaceholder", typeof(RectTransform));
            ph.transform.SetParent(_startParent, false);
            ph.GetComponent<RectTransform>().sizeDelta = _rectTransform.sizeDelta;
            ph.transform.SetSiblingIndex(_startSiblingIndex);
            _placeholder = ph;

            _layoutElement = GetComponent<LayoutElement>();
            if (_layoutElement == null)
            {
                _layoutElement = gameObject.AddComponent<LayoutElement>();
                _addedLayoutElement = true;
            }
            _layoutElement.ignoreLayout = true;
        }

        _rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        transform.SetParent(_rootCanvas.transform, true);
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rootCanvas == null) return;

        _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;

        if (_placeholder != null)
        {
            var parentRt = _startParent as RectTransform;
            bool overHand = RectTransformUtility.RectangleContainsScreenPoint(
                parentRt, eventData.position, eventData.pressEventCamera);
            _placeholder.SetActive(overHand);

            if (overHand)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRt, eventData.position, eventData.pressEventCamera, out var localPoint);
                _placeholder.transform.SetSiblingIndex(GetSlotIndex(parentRt, localPoint.x));
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_layoutElement != null)
        {
            _layoutElement.ignoreLayout = false;
            if (_addedLayoutElement)
            {
                Destroy(_layoutElement);
                _layoutElement = null;
                _addedLayoutElement = false;
            }
        }

        _canvasGroup.blocksRaycasts = true;
        if (_wasInHand) SetSiblingRaycast(_startParent, true);
        OnDragEnd?.Invoke(this);

        if (_placeholder != null) { Destroy(_placeholder); _placeholder = null; }
        _droppedOnZone = false;
    }

    private static void SetSiblingRaycast(Transform parent, bool enabled)
    {
        foreach (Transform child in parent)
        {
            var img = child.GetComponent<Image>();
            if (img != null) img.raycastTarget = enabled;
        }
    }

    private int GetSlotIndex(RectTransform parent, float localX)
    {
        int count = parent.childCount;
        if (count <= 1) return 0;

        // Use midpoints between adjacent slot positions as thresholds.
        // HLG places N children at fixed positions regardless of which child occupies
        // which slot, so midpoints are stable and don't shift as the placeholder moves.
        for (int i = 0; i < count - 1; i++)
        {
            float a = ((RectTransform)parent.GetChild(i)).localPosition.x;
            float b = ((RectTransform)parent.GetChild(i + 1)).localPosition.x;
            if (localX < (a + b) * 0.5f) return i;
        }
        return count - 1;
    }
}
