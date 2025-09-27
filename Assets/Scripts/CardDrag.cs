using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform _rectTransform;
    private Canvas _canvas;
    private Vector3 _startPosition;
    private Transform _startParent;
    private int _startSiblingIndex;
    private HandManager _handManager;
    private CanvasGroup _canvasGroup;

    private GameObject _placeholder;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>(); // needed for scaling with drag
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _startPosition = _rectTransform.anchoredPosition;
        _startParent = transform.parent;
        _startSiblingIndex = transform.GetSiblingIndex();

        // cache hand view if available
        _handManager = GetComponentInParent<HandManager>();

        // Try to get/create a CanvasGroup so we can render on top while dragging
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false; // allow drop detection through this card

        // store original
        _startParent = transform.parent;
        _startSiblingIndex = transform.GetSiblingIndex();

        // create a UI placeholder under the hand parent and size it to match the card
        // we are going to move the real card to a parent overlay so we can drag it freely,
        // but we need to keep a placeholder so the HorizontalLayoutGroup won't reflow other cards
        var placeholderGO = new GameObject("CardPlaceholder", typeof(RectTransform));
        placeholderGO.transform.SetParent(_startParent, false);

        var cardRt = GetComponent<RectTransform>();
        var prt = placeholderGO.GetComponent<RectTransform>();
        // prt.localScale = Vector3.one;
        // prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        // prt.pivot = new Vector2(0.5f, 0.5f);
        // Copy sizeDelta which reflects the intended UI element size
        prt.sizeDelta = cardRt.sizeDelta;

        // make layout respect the placeholder size
        // not needed as rn the HorinzontalLayoutGroup does not control child sizes, but if it did:
        // var le = placeholderGO.AddComponent<LayoutElement>();
        // le.preferredWidth = prt.sizeDelta.x;
        // le.preferredHeight = prt.sizeDelta.y;
        // le.minWidth = le.preferredWidth;
        // le.minHeight = le.preferredHeight;

        placeholderGO.transform.SetSiblingIndex(_startSiblingIndex);
        _placeholder = placeholderGO;

        // move real card to overlay so HLG won't reflow it
        var overlay = _canvas.transform; // or a dedicated overlay container under Canvas
        transform.SetParent(overlay);
        _canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling(); // now only affects overlay, not HLG
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Move the card along with the pointer
        _rectTransform.anchoredPosition += eventData.delta / _canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // restore raycast behavior
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
    }
}
