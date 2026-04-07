using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;
using UnityEngine.UI;

/// <summary>
/// Owns all card positions in the hand. No HorizontalLayoutGroup — positions are
/// computed manually and animated via per-card lerp coroutines.
///
/// Layout data:
///   _order     — logical order of cards; never mutated during drag.
///   _dragging  — cards currently on the root canvas; excluded from layout.
///   _insertionHint — slot index showing where the dragged card would land on drop;
///                    creates a visual gap in the hand while hovering over it.
///
/// Y targets:
///   Selected cards use _selectedYOffset, hovered cards use _hoverYOffset.
///   HandManager subscribes to SelectionManager and CardHover.AnyHoverChanged
///   so it can recompute Y targets without CardHover touching anchoredPosition.
/// </summary>
public class HandManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CardManager  _manager;
    [SerializeField] private RectTransform _handView;
    [SerializeField] private HandDropZone _handDropZone;

    [Header("Hand Settings")]
    [SerializeField, Range(1, 52)] private int _handSize   = 27;
    [SerializeField]               private int _randomSeed = 0;

    [Header("Layout")]
    [Tooltip("Preferred horizontal spacing. Negative = overlap.")]
    [SerializeField] private float _spacing = -120f;
    [Tooltip("Minimum fraction of each card's width that must remain visible.")]
    [SerializeField, Range(0.05f, 0.9f)] private float _minCardVisible = 0.25f;
    [SerializeField] private Vector2 _fallbackCardSize = new(140f, 200f);

    [Header("Y Offsets")]
    [SerializeField] private float _hoverYOffset    = 30f;
    [SerializeField] private float _selectedYOffset = 50f;

    [Header("Animation speeds")]
    [Tooltip("Lerp speed for layout shifts (selection, hover, reflow).")]
    [SerializeField] private float _layoutSpeed = 20f;
    [Tooltip("Lerp speed for returning cards after a failed drop.")]
    [SerializeField] private float _returnSpeed = 14f;

    // ── Static references ────────────────────────────────────────────────────
    public static HandManager Instance { get; private set; }

    // CardDrag reads this to detect whether a card was dragged from the hand
    // without requiring an HLG component on the parent.
    public static RectTransform HandViewRT { get; private set; }

    // ── Public API ──────────────────────────────────────────────────────────
    public RectTransform HandView => _handView;

    public RectTransform GetCardRect(Card.CardId id) =>
        _cardRects.TryGetValue(id, out var rt) ? rt : null;

    // ── State ───────────────────────────────────────────────────────────────
    private readonly List<Card.CardId>                    _order        = new();
    private readonly Dictionary<Card.CardId, RectTransform> _cardRects  = new();
    private readonly HashSet<Card.CardId>                 _dragging     = new();
    private readonly HashSet<Card.CardId>                 _hovered      = new();
    private readonly Dictionary<Card.CardId, Coroutine>   _moveRoutines = new();

    private int    _insertionHint = -1; // slot index of visual gap; -1 = none
    private Canvas _canvas;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance   = this;
        HandViewRT = _handView;
        _canvas    = _handView ? _handView.GetComponentInParent<Canvas>() : null;
    }


    private void Start()
    {
        if (_manager == null) _manager = FindFirstObjectByType<CardManager>();
        if (_manager == null || _handView == null)
        {
            Debug.LogError("[HandManager] CardManager or HandView not assigned.");
            enabled = false;
            return;
        }

        if (_handDropZone != null) _handDropZone.CardReturned += OnCardReturnedToHand;
        SelectionManager.Instance.SelectionChanged   += OnSelectionChanged;
        SelectionManager.Instance.SelectionCommitted += OnSelectionCommitted;
        SelectionManager.Instance.CommitFailed    += OnCommitFailed;
        CardDrag.AnyDragBegin     += OnAnyDragBegin;
        CardDrag.AnyDragEnd       += OnAnyDragEnd;
        CardDrag.AnyDragMoved     += OnAnyDragMoved;
        CardHover.AnyHoverChanged += OnAnyHoverChanged;

        #if UNITY_EDITOR || DEV_BUILD
                if (!NetworkClient.active)
                {
                    DealNewHand();
                    if (TurnManager.IsSoloMode)
                        TurnManager.Instance.DevStartSolo();
                }
        #endif
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_handDropZone != null) _handDropZone.CardReturned -= OnCardReturnedToHand;
        SelectionManager.Instance.SelectionChanged   -= OnSelectionChanged;
        SelectionManager.Instance.SelectionCommitted -= OnSelectionCommitted;
        SelectionManager.Instance.CommitFailed    -= OnCommitFailed;
        CardDrag.AnyDragBegin     -= OnAnyDragBegin;
        CardDrag.AnyDragEnd       -= OnAnyDragEnd;
        CardDrag.AnyDragMoved     -= OnAnyDragMoved;
        CardHover.AnyHoverChanged -= OnAnyHoverChanged;
    }

    // ── Deal ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the hand from a server-dealt set of cards.
    /// Called by DealManager via TargetRpc — only runs on the local client.
    /// </summary>
    public void ReceiveHand(Card.CardId[] cards)
    {
        ClearHand();
        foreach (var id in cards) _order.Add(id);

        foreach (var id in _order)
        {
            var go = _manager.SpawnCard(id, _handView);
            SetupCard(go);
            _cardRects[id] = go.GetComponent<RectTransform>();
        }

        SnapLayout();
    }

    /// <summary>Deal a local hand for offline testing (context menu only).</summary>
    [ContextMenu("Deal New Hand")]
    public void DealNewHand()
    {
        ClearHand();

        var deck = _manager.GetShuffledDeck(_randomSeed);
        int n    = Mathf.Min(_handSize, deck.Count);
        for (int i = 0; i < n; i++) _order.Add(deck[i]);

        foreach (var id in _order)
        {
            var go = _manager.SpawnCard(id, _handView);
            SetupCard(go);
            _cardRects[id] = go.GetComponent<RectTransform>();
        }

        SnapLayout(); // instant on deal — no animation
    }

    [ContextMenu("Clear Hand")]
    public void ClearHand()
    {
        var ids = new List<Card.CardId>(_cardRects.Keys);
        foreach (var id in ids)
        {
            var rt = ReleaseCard(id);
            if (rt != null) Destroy(rt.gameObject);
        }
        _insertionHint = -1;
    }

    private void SetupCard(GameObject go)
    {
        var rt       = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        if (rt.rect.width < 1f || rt.rect.height < 1f) rt.sizeDelta = _fallbackCardSize;

        var img = go.GetComponentInChildren<Image>(true) ?? go.AddComponent<Image>();
        if (img.sprite == null) img.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        img.type           = Image.Type.Simple;
        img.preserveAspect = true;

        if (go.GetComponent<CardHover>()      == null) go.AddComponent<CardHover>();
        if (go.GetComponent<CardSelectable>() == null) go.AddComponent<CardSelectable>();
    }

    // ── Drag ────────────────────────────────────────────────────────────────

    private void OnAnyDragBegin(CardDrag drag)
    {
        var card = drag.GetComponent<Card>();
        if (card == null || !_cardRects.ContainsKey(card.Id)) return;

        // Always mark the primary card as dragging.
        // Also mark staged cards — GroupDragHandler moves them as followers whenever
        // staged.Count > 1, regardless of whether the primary is selected.
        var staged = SelectionManager.Instance.Staged;
        _dragging.Add(card.Id);
        foreach (var id in staged)
            if (_cardRects.ContainsKey(id)) _dragging.Add(id);

        foreach (var id in _dragging)
        {
            // Cards with blocksRaycasts = false won't fire OnPointerExit — clear hover state now.
            _hovered.Remove(id);
            // Stop any running animation so it doesn't fight CardDrag's anchoredPosition writes.
            if (_moveRoutines.TryGetValue(id, out var c) && c != null)
            {
                StopCoroutine(c);
                _moveRoutines.Remove(id);
            }
        }

        _insertionHint = -1;
        RefreshLayout(_layoutSpeed);
    }

    private void OnAnyDragEnd(CardDrag drag)
    {
        if (drag.WasDropHandled) return; // commit or hand-drop handled elsewhere

        var card = drag.GetComponent<Card>();
        if (card != null && _dragging.Contains(card.Id))
            ReturnCard(card.Id);
    }

    private void OnAnyDragMoved(CardDrag drag, Vector2 screenPos)
    {
        var card = drag.GetComponent<Card>();
        if (card == null || !_cardRects.ContainsKey(card.Id)) return;

        Camera cam     = _canvas != null ? _canvas.worldCamera : null;
        bool   overHand = RectTransformUtility.RectangleContainsScreenPoint(_handView, screenPos, cam);

        if (!overHand) { ClearInsertionHint(); return; }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _handView, screenPos, cam, out var local);
        SetInsertionHint(ComputeInsertionIndex(local.x));
    }

    private int ComputeInsertionIndex(float localX)
    {
        // Return the insertion slot index within the VISIBLE list (excludes dragging cards).
        int visibleIndex = 0;
        foreach (var id in _order)
        {
            if (_dragging.Contains(id)) continue;
            if (_cardRects.TryGetValue(id, out var rt) && localX < rt.anchoredPosition.x)
                return visibleIndex;
            visibleIndex++;
        }
        return visibleIndex; // append at end
    }

    private void SetInsertionHint(int index)
    {
        if (_insertionHint == index) return;
        _insertionHint = index;
        RefreshLayout(_layoutSpeed);
    }

    private void ClearInsertionHint()
    {
        if (_insertionHint == -1) return;
        _insertionHint = -1;
        RefreshLayout(_layoutSpeed);
    }

    // ── Return cards ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by HandManager.OnAnyDragEnd for the primary card on failed drop.
    /// Reparents the card to the hand view and animates it to its slot.
    /// </summary>
    public void ReturnCard(Card.CardId id)
    {
        if (!_dragging.Remove(id)) return;
        if (_cardRects.TryGetValue(id, out var rt))
            ReparentToHand(id, rt);
        _insertionHint = -1;
        RefreshLayout(_returnSpeed);
    }

    /// <summary>
    /// Called by GroupDragHandler for follower cards on failed drop or hand-drop.
    /// Reparents each card and animates all back in one RefreshLayout call.
    /// </summary>
    public void ReturnCards(IEnumerable<Card.CardId> ids)
    {
        bool any = false;
        foreach (var id in ids)
        {
            if (!_dragging.Remove(id)) continue;
            any = true;
            if (_cardRects.TryGetValue(id, out var rt))
                ReparentToHand(id, rt);
        }
        if (!any) return;
        _insertionHint = -1;
        RefreshLayout(_returnSpeed);
    }

    // Reparent a card back into the hand at the correct sibling index so render
    // order matches _order — without this, SetParent appends at the end and the
    // card renders on top of all other hand cards.
    private void ReparentToHand(Card.CardId id, RectTransform rt)
    {
        rt.SetParent(_handView, worldPositionStays: true);
        SetSiblingIndexFromOrder(id, rt);
    }

    // Set sibling index within _handView to match this card's position in _order.
    // Cards earlier in _order that are still children of _handView determine the index.
    private void SetSiblingIndexFromOrder(Card.CardId id, RectTransform rt)
    {
        int siblingIndex = 0;
        foreach (var orderId in _order)
        {
            if (orderId.Equals(id)) break;
            if (_cardRects.TryGetValue(orderId, out var other) && other.parent == _handView)
                siblingIndex++;
        }
        rt.SetSiblingIndex(siblingIndex);
    }

    // ── Hover ────────────────────────────────────────────────────────────────

    private void OnAnyHoverChanged(Card.CardId id, bool hovered)
    {
        if (!_cardRects.ContainsKey(id)) return;
        if (hovered) _hovered.Add(id); else _hovered.Remove(id);
        RefreshLayout(_layoutSpeed);
    }

    // ── Selection ────────────────────────────────────────────────────────────

    private void OnSelectionChanged(IReadOnlyList<Card.CardId> _) =>
        RefreshLayout(_layoutSpeed);

    // ── Commit ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when cards are committed. Subscribers receive the RectTransforms and
    /// take full ownership — responsible for positioning and eventual destruction.
    /// Local plays only; subscribe to TrickManager.SetPlayed for remote plays.
    /// </summary>
    public static event Action<IReadOnlyList<(Card.CardId id, RectTransform rt)>> CardsPlayed;

    private void OnSelectionCommitted(IReadOnlyList<Card.CardId> committed, SetValidator.ValidationResult _)
    {
        var played = new List<(Card.CardId, RectTransform)>(committed.Count);
        foreach (var id in committed)
        {
            var rt = ReleaseCard(id);
            if (rt != null) played.Add((id, rt));
        }
        _insertionHint = -1;
        RefreshLayout(_layoutSpeed);

        if (played.Count > 0) CardsPlayed?.Invoke(played);
    }

    private void OnCommitFailed(SetValidator.ValidationResult result)
    {
        var (type, message) = result.Code switch
        {
            SetValidator.FailCode.NotYourTurn  => (AnnouncementType.Error,   "Not Your Turn"),
            SetValidator.FailCode.WrongSetType => (AnnouncementType.Warning, result.FailReason),
            _                                  => (AnnouncementType.Error,   "Not Valid"),
        };
        AnnouncementOverlay.Show(type, message);
    }

    // ── Hand-drop return ─────────────────────────────────────────────────────

    private void OnCardReturnedToHand(CardDrag drag)
    {
        var card = drag.GetComponent<Card>();
        if (card == null || !_cardRects.ContainsKey(card.Id)) return;

        _dragging.Remove(card.Id);

        // Reorder to the current insertion hint, or keep at original position.
        int currentIdx = _order.IndexOf(card.Id);
        if (_insertionHint >= 0 && currentIdx != _insertionHint)
        {
            _order.RemoveAt(currentIdx);
            int target = Mathf.Clamp(_insertionHint, 0, _order.Count);
            _order.Insert(target, card.Id);
        }

        _insertionHint = -1;

        if (_cardRects.TryGetValue(card.Id, out var rt))
            ReparentToHand(card.Id, rt);

        RefreshLayout(_returnSpeed);
    }

    // ── Card release ────────────────────────────────────────────────────────

    /// <summary>
    /// Removes a card from all hand tracking and stops its move routine.
    /// Returns the RectTransform so the caller decides what to do with the GO.
    /// </summary>
    private RectTransform ReleaseCard(Card.CardId id)
    {
        _order.Remove(id);
        _dragging.Remove(id);
        _hovered.Remove(id);
        if (_moveRoutines.TryGetValue(id, out var routine) && routine != null)
        {
            StopCoroutine(routine);
            _moveRoutines.Remove(id);
        }
        _cardRects.Remove(id, out var rt);
        return rt;
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    private void RefreshLayout(float speed)
    {
        // Build visible list (exclude dragging cards).
        var visible = new List<Card.CardId>(_order.Count);
        foreach (var id in _order)
            if (!_dragging.Contains(id)) visible.Add(id);

        int n     = visible.Count;
        int slots = _insertionHint >= 0 ? n + 1 : n; // +1 gap slot when hint active

        int si = 0; // slot index (may skip the gap)
        for (int vi = 0; vi < n; vi++, si++)
        {
            if (_insertionHint >= 0 && si == _insertionHint) si++; // skip gap slot

            var id = visible[vi];
            if (!_cardRects.TryGetValue(id, out var rt)) continue;

            var target = new Vector2(ComputeSlotX(si, slots), GetYTarget(id));
            AnimateCard(id, rt, target, speed);
        }
    }

    private void SnapLayout()
    {
        var visible = new List<Card.CardId>(_order.Count);
        foreach (var id in _order)
            if (!_dragging.Contains(id)) visible.Add(id);

        int n = visible.Count;
        for (int i = 0; i < n; i++)
        {
            var id = visible[i];
            if (_cardRects.TryGetValue(id, out var rt))
            {
                rt.anchoredPosition = new Vector2(ComputeSlotX(i, n), GetYTarget(id));
                rt.localRotation    = Quaternion.identity;
            }
        }
    }

    private float ComputeSlotX(int slotIndex, int totalSlots)
    {
        if (totalSlots <= 0) return 0f;
        float cardWidth      = GetCardWidth();
        float containerWidth = _handView.rect.width;
        if (containerWidth <= 1f) return 0f; // layout not ready

        float spacing    = ComputeSpacing(totalSlots, cardWidth, containerWidth);
        float step       = cardWidth + spacing;
        float totalWidth = cardWidth + step * (totalSlots - 1);
        float startX     = -totalWidth / 2f + cardWidth / 2f;
        return startX + slotIndex * step;
    }

    private float ComputeSpacing(int count, float cardWidth, float containerWidth)
    {
        if (count <= 1) return 0f;
        float fitSpacing = (containerWidth - cardWidth * count) / (count - 1);
        float minSpacing = -(cardWidth * (1f - _minCardVisible));
        return Mathf.Max(Mathf.Min(_spacing, fitSpacing), minSpacing);
    }

    private float GetCardWidth()
    {
        foreach (var rt in _cardRects.Values)
            if (rt != null && rt.rect.width > 1f) return rt.rect.width;
        return _fallbackCardSize.x;
    }

    private float GetYTarget(Card.CardId id)
    {
        if (SelectionManager.Instance.Staged.Contains(id)) return _selectedYOffset;
        if (_hovered.Contains(id))                          return _hoverYOffset;
        return 0f;
    }

    // ── Animation ────────────────────────────────────────────────────────────

    private void AnimateCard(Card.CardId id, RectTransform rt, Vector2 target, float speed)
    {
        if (_moveRoutines.TryGetValue(id, out var existing) && existing != null)
            StopCoroutine(existing);
        _moveRoutines[id] = StartCoroutine(LerpCard(rt, target, speed));
    }

    private static IEnumerator LerpCard(RectTransform rt, Vector2 target, float speed)
    {
        while (rt != null)
        {
            bool posSettled = Vector2.Distance(rt.anchoredPosition, target) < 0.5f;
            bool rotSettled = Quaternion.Angle(rt.localRotation, Quaternion.identity) < 0.5f;
            if (posSettled && rotSettled) break;

            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target,       Time.deltaTime * speed);
            rt.localRotation    = Quaternion.Lerp(rt.localRotation, Quaternion.identity, Time.deltaTime * speed);
            yield return null;
        }
        if (rt != null)
        {
            rt.anchoredPosition = target;
            rt.localRotation    = Quaternion.identity;
        }
    }
}
