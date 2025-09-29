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
    private CanvasGroup _canvasGroup;

    private GameObject _placeholder;
    private Vector2 _pointerOffset;
    private LayoutElement _layoutElement;
    private bool _addedLayoutElement = false;

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
        _startParent = transform.parent;
        _startSiblingIndex = transform.GetSiblingIndex();

        // Try to get/create a CanvasGroup so we can render on top while dragging
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false; // allow drop detection through this card

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
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Move the dragged card while preserving the grab offset. Compute pointer
        // in hand-local space and position the card there so we avoid reparenting.
        var handRt = _handManager.GetHandRect();
        Vector2 handLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(handRt, eventData.position, eventData.pressEventCamera, out handLocal);

        _rectTransform.anchoredPosition = handLocal - _pointerOffset;

        // compute insertion index using hand-local X (simpler for uniform horizontal layouts)
        int newIndex = _handManager.GetSiblingIndexForLocalX(_rectTransform.anchoredPosition.x);
        _handManager.MoveCardToIndex(_placeholder.transform as RectTransform, newIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        int newIndex = _handManager.GetSiblingIndexForLocalX(_rectTransform.anchoredPosition.x);

        // restore layout control
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

        // update hand ordering/data and re-layout
        _handManager.MoveCardToIndex(_rectTransform, newIndex);

        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
        if (_placeholder != null) { Destroy(_placeholder); _placeholder = null; }
    }
}
