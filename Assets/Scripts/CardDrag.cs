using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // rectransform of the card being dragged
    private RectTransform _rectTransform;
    private Transform _startParent;
    private int _startSiblingIndex;
    private HandManager _handManager;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;

    private GameObject _placeholder;
    private Vector2 _pointerOffset;
    private LayoutElement _layoutElement;
    private bool _addedLayoutElement = false;
    private bool _dragFromTable;
    private bool _droppedOnZone;

    public bool IsFromTable => _dragFromTable;
    public int PlaceholderSiblingIndex => _placeholder != null ? _placeholder.transform.GetSiblingIndex() : _startSiblingIndex;

    public void ReturnToHand()
    {
        _droppedOnZone = true;
        if (_layoutElement != null) _layoutElement.ignoreLayout = false;
        _handManager.MoveCardToIndex(_rectTransform, PlaceholderSiblingIndex);
    }

    public void PlayToTable()
    {
        _droppedOnZone = true;
        var rootCanvas = _handManager.Canvas.rootCanvas;
        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();
        _handManager.PlayCard(_rectTransform);
    }

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    // Called by the hand when it spawns a card so we don't need to rely on scene lookups.
    public void SetHandManager(HandManager hm)
    {
        _handManager = hm;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false; // allow drop detection through this card

        _dragFromTable = _handManager == null || transform.parent != (Transform)_handManager.GetHandRect();

        if (_dragFromTable)
        {
            // Card is on the canvas (played to table) — use screen-space tracking
            transform.SetAsLastSibling();
            _pointerOffset = (Vector2)_rectTransform.position - eventData.position;
            return;
        }

        _startParent = transform.parent;
        _startSiblingIndex = transform.GetSiblingIndex();

        // create a UI placeholder under the hand parent and size it to match the card
        // we are going to move the real card to a parent overlay so we can drag it freely,
        // but we need to keep a placeholder so the HorizontalLayoutGroup won't reflow other cards
        var placeholderGO = new GameObject("CardPlaceholder", typeof(RectTransform));
        placeholderGO.transform.SetParent(_startParent, false);

        var cardRt = GetComponent<RectTransform>();
        var prt = placeholderGO.GetComponent<RectTransform>();
        prt.sizeDelta = cardRt.sizeDelta;

        placeholderGO.transform.SetSiblingIndex(_startSiblingIndex);
        _placeholder = placeholderGO;

        // ensure the card isn't controlled by the layout while dragging
        _layoutElement = GetComponent<LayoutElement>();
        if (_layoutElement == null)
        {
            _layoutElement = gameObject.AddComponent<LayoutElement>();
            _addedLayoutElement = true;
        }
        _layoutElement.ignoreLayout = true;

        // Keep the real card under the hand parent (avoid reparenting) but make
        // the layout ignore it so we can move it freely while the placeholder
        // reserves the slot.
        var handRt = _handManager?.GetHandRect();

        // compute pointer offset in hand-local space so the grab point is preserved
        if (handRt != null)
        {
            Vector2 handLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(handRt, eventData.position, eventData.pressEventCamera, out handLocal);
            _pointerOffset = handLocal - _rectTransform.anchoredPosition;

            // bring this card visually on top of siblings
            transform.SetAsLastSibling();
        }
        else
        {
            // fallback: no hand rect available
            _pointerOffset = Vector2.zero;
            transform.SetAsLastSibling();
        }
        // bring this card visually on top of everything
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragFromTable)
        {
            Vector2 target = eventData.position + _pointerOffset;
            _rectTransform.position = new Vector3(target.x, target.y, _rectTransform.position.z);
            return;
        }

        // Move the dragged card in canvas space, preserving the grab offset
        var handRt = _handManager.GetHandRect();
        Vector2 handLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(handRt, eventData.position, eventData.pressEventCamera, out handLocal);

        _rectTransform.anchoredPosition = handLocal - _pointerOffset;

        bool overHand = RectTransformUtility.RectangleContainsScreenPoint(handRt, eventData.position, eventData.pressEventCamera);
        _placeholder.SetActive(overHand);

        if (overHand)
        {
            int newIndex = _handManager.GetSiblingIndexForLocalX(_rectTransform.anchoredPosition.x);
            _handManager.MoveCardToIndex(_placeholder.transform as RectTransform, newIndex);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragFromTable)
        {
            _dragFromTable = false;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
            return;
        }

        // If no drop zone handled this drag, fall back to returning the card to its hand slot
        if (!_droppedOnZone)
            ReturnToHand();

        _droppedOnZone = false;

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
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
        if (_placeholder != null) { Destroy(_placeholder); _placeholder = null; }
    }
}
