using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Moves other staged cards as followers when a selected card is dragged.
///
/// Drag phase  : followers are reparented to the root canvas for free world-space
///               movement and lerped toward a fan/stack offset from the primary.
///
/// Return phase: on failed drop (or hand-drop), followers are handed back to
///               HandManager.ReturnCards, which reparents them to the hand view
///               and animates them to their target positions. The primary card is
///               handled separately by HandManager.OnAnyDragEnd.
///
/// Y offsets, insertion hints, and all position logic live in HandManager.
/// </summary>
public class GroupDragHandler : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private HandManager   _handManager;
    [SerializeField] private RectTransform _handViewport;
    [SerializeField] private Canvas        _rootCanvas;

    [Header("Fan (pointer inside hand)")]
    [SerializeField] private Vector2 _fanOffset = new Vector2(-24f, 14f);
    [SerializeField] private float   _fanAngle  = 6f;

    [Header("Stack (pointer outside hand)")]
    [SerializeField] private Vector2 _stackOffset = new Vector2(-20f, -20f);
    [SerializeField] private float   _stackAngle  = 2f;

    [Header("Follow speed")]
    [SerializeField] private float _lerpSpeed = 18f;

    private CardDrag                    _activeDrag;
    private readonly List<RectTransform> _followers = new();
    private bool _isGroupDragging;

    private void OnEnable()
    {
        CardDrag.AnyDragBegin += OnAnyDragBegin;
        CardDrag.AnyDragEnd   += OnAnyDragEnd;
    }

    private void OnDisable()
    {
        CardDrag.AnyDragBegin -= OnAnyDragBegin;
        CardDrag.AnyDragEnd   -= OnAnyDragEnd;
        _isGroupDragging = false;
        _followers.Clear();
    }

    // -------------------------------------------------------------------------
    // LateUpdate — lerp followers toward fan/stack offset from primary
    // -------------------------------------------------------------------------

    private void LateUpdate()
    {
        if (!_isGroupDragging || _activeDrag == null) return;

        bool inHand = RectTransformUtility.RectangleContainsScreenPoint(
            _handViewport, _activeDrag.PointerScreenPos, _rootCanvas.worldCamera);

        Vector3 primaryPos = _activeDrag.CardRect.position;

        for (int i = 0; i < _followers.Count; i++)
        {
            int     slot   = i + 1;
            var     rt     = _followers[i];
            Vector3 target = primaryPos + (Vector3)(inHand ? _fanOffset * slot : _stackOffset * slot);
            float   angle  = inHand ? _fanAngle * slot : _stackAngle;

            rt.position      = Vector3.Lerp(rt.position, target, Time.deltaTime * _lerpSpeed);
            rt.localRotation = Quaternion.Lerp(rt.localRotation, Quaternion.Euler(0f, 0f, angle),
                                               Time.deltaTime * _lerpSpeed);
        }
    }

    // -------------------------------------------------------------------------
    // Drag begin — reparent followers to root canvas (HandManager marks them dragging)
    // -------------------------------------------------------------------------

    private void OnAnyDragBegin(CardDrag drag)
    {
        if (_handManager == null || _handViewport == null || _rootCanvas == null) return;

        var card = drag.GetComponent<Card>();
        if (card == null) return;

        var staged = SelectionManager.Instance.Staged;
        if (staged.Count == 0) return; // nothing staged to follow

        _activeDrag = drag;
        _followers.Clear();

        foreach (var id in staged)
        {
            if (id.Equals(card.Id)) continue;

            var rt = _handManager.GetCardRect(id);
            if (rt == null) continue;

            EnsureCanvasGroup(rt).blocksRaycasts = false;
            rt.SetParent(_rootCanvas.transform, worldPositionStays: true);
            rt.SetAsLastSibling();
            _followers.Add(rt);
        }

        _isGroupDragging = _followers.Count > 0;
    }

    // -------------------------------------------------------------------------
    // Drag end — hand followers back to HandManager for return animation
    // -------------------------------------------------------------------------

    private void OnAnyDragEnd(CardDrag drag)
    {
        if (!_isGroupDragging || drag != _activeDrag) return;
        _isGroupDragging = false;
        _activeDrag      = null;

        // Committed: Staged is empty after SelectionManager.Commit(); HandManager
        // already removed the cards via OnSelectionCommitted. Just clean up.
        if (drag.WasDropHandled && SelectionManager.Instance.Staged.Count == 0)
        {
            _followers.Clear();
            return;
        }

        // Failed drop or returned to hand: restore raycasts and delegate to HandManager.
        // HandManager.ReturnCards reparents followers to HandView and animates them.
        var ids = new List<Card.CardId>(_followers.Count);
        foreach (var rt in _followers)
        {
            EnsureCanvasGroup(rt).blocksRaycasts = true;
            var img = rt.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;

            var c = rt.GetComponent<Card>();
            if (c != null) ids.Add(c.Id);
        }

        if (ids.Count > 0) _handManager.ReturnCards(ids);
        _followers.Clear();
    }

    private static CanvasGroup EnsureCanvasGroup(RectTransform rt)
    {
        var cg = rt.GetComponent<CanvasGroup>();
        return cg != null ? cg : rt.gameObject.AddComponent<CanvasGroup>();
    }
}
