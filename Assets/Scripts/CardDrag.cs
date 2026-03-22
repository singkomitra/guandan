using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform _rectTransform;
    private Canvas _rootCanvas;
    private CanvasGroup _canvasGroup;
    private Transform _startParent;
    private int _startSiblingIndex;
    private bool _droppedOnZone;
    private bool _wasInHand;

    public event Action<CardDrag> OnDragBegin;
    public event Action<CardDrag> OnDragEnd;

    public static event Action<CardDrag>         AnyDragBegin;
    public static event Action<CardDrag>         AnyDragEnd;
    public static event Action<CardDrag, Vector2> AnyDragMoved; // screen pos each drag frame

    public RectTransform CardRect         => _rectTransform;
    public Transform     StartParent      => _startParent;
    public bool          WasDropHandled   => _droppedOnZone;
    public bool          IsFromHand       => _wasInHand;
    public Vector2       PointerScreenPos { get; private set; }
    public int           StartSiblingIndex => _startSiblingIndex;

    public void NotifyDropHandled() => _droppedOnZone = true;

    private void Awake() => _rectTransform = GetComponent<RectTransform>();

    public void OnBeginDrag(PointerEventData eventData)
    {
        _startParent = transform.parent;
        _startSiblingIndex = transform.GetSiblingIndex();
        _droppedOnZone = false;
        PointerScreenPos = eventData.position;

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;

        // HandManager.HandViewRT replaces HLG detection — no HLG dependency.
        _wasInHand = HandManager.HandViewRT != null && _startParent == HandManager.HandViewRT;

        _rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        transform.SetParent(_rootCanvas.transform, true);
        transform.SetAsLastSibling();

        // Fire after all drag state is initialized so subscribers read consistent values.
        OnDragBegin?.Invoke(this);
        AnyDragBegin?.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rootCanvas == null) return;
        PointerScreenPos = eventData.position;
        _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
        AnyDragMoved?.Invoke(this, eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.blocksRaycasts = true;
        OnDragEnd?.Invoke(this);
        AnyDragEnd?.Invoke(this);
        _droppedOnZone = false;
    }
}
