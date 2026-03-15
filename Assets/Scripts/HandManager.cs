using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// Displays a "hand" of card prefabs under a parent RectTransform,
/// dealing a shuffled subset from CardManager and laying them out
/// either with a HorizontalLayoutGroup (overlap via negative spacing)
/// or with a simple manual fan (position + Z-rotation).
/// </summary>
public class HandManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CardManager _manager;
    [SerializeField] private RectTransform _handView;

    [Header("Hand Settings")]
    [Tooltip("How many cards to show in the hand.")]
    [SerializeField, Range(1, 52)] private int _handSize = 27;

    [Tooltip("0 = time-based seed (let CardManager decide). Non-zero = deterministic.")]
    [SerializeField] private int _randomSeed = 0;

    [Tooltip("Horizontal spacing between cards. Negative to overlap (with HLG) or to step (manual).")]
    [SerializeField] private float _spacing = -120f;

    [Tooltip("Per-card Z rotation (degrees) for the fanned look.")]
    [SerializeField] private float _fanAngle = 8f;

    [Header("Fit / Overflow")]
    [Tooltip("Minimum fraction of each card's width that must remain visible when auto-compressing. 0.25 = at least 25% of each card shows.")]
    [SerializeField, Range(0.05f, 0.9f)] private float _minCardVisible = 0.25f;

    [Header("Debug/UX")]
    [Tooltip("Enable extra Debug.Log messages.")]
    [SerializeField] private bool _verboseLogs = false;

    [Header("Card Defaults (if prefab provides no size)")]
    [SerializeField] private Vector2 _fallbackCardSize = new(140, 200);

    // runtime
    private readonly List<Card.CardId> _current = new();
    private HorizontalLayoutGroup _hlg;
    private Canvas _canvas;

    // cached to detect Inspector changes while playing
    private int _lastHandSize, _lastSeed;
    private float _lastSpacing, _lastFanAngle;

    // cached waiter to avoid new allocations per relayout
    private readonly WaitForEndOfFrame _waitEndOfFrame = new();

    #region Unity Lifecycle

    private void Awake()
    {
        // cache component lookup once here
        _hlg = _handView ? _handView.GetComponent<HorizontalLayoutGroup>() : null;
        _canvas = _handView ? _handView.GetComponentInParent<Canvas>() : null;
    }

    private void Start()
    {
        // try to find CardManager if not wired in the inspector
        if (_manager == null) _manager = FindFirstObjectByType<CardManager>();

        if (_manager == null)
        {
            Debug.LogError("[HandManager] No CardManager in scene.");
            enabled = false;
            return;
        }

        if (_handView == null)
        {
            Debug.LogError("[HandManager] Hand parent is not assigned.");
            enabled = false;
            return;
        }

        if (_manager.FullDeck.Count == 0)
            Debug.LogWarning("[HandManager] CardManager has 0 cards — check Resources paths/filenames.");

        ValidateSetup();
        DealNewHand();
        SnapshotSettings();
    }

    private void Update()
    {
        // only respond to runtime inspector tweaks while playing
        if (!Application.isPlaying) return;

        // if hand size or seed changed → re-deal a new hand
        if (_handSize != _lastHandSize || _randomSeed != _lastSeed)
        {
            DealNewHand();
            SnapshotSettings();
            return;
        }

        // if layout knobs changed → keep same cards, just re-layout
        if (!Mathf.Approximately(_spacing, _lastSpacing) ||
            !Mathf.Approximately(_fanAngle, _lastFanAngle))
        {
            RelayoutCurrentHand();
            SnapshotSettings();
        }
    }

    // call this once in Start() after you validate refs
    private void ValidateSetup()
    {
        var canvas = _handView.GetComponentInParent<Canvas>();
        if (canvas == null)
            Debug.LogError("[HandManager] Hand parent is not under a Canvas (UI won’t render).");

        Debug.Log($"[HandManager] Canvas render mode: {canvas.renderMode}, pixel rect: {canvas.pixelRect}");

        var rt = _handView as RectTransform;
        if (rt != null && (rt.rect.width <= 1f || rt.rect.height <= 1f))
            Debug.LogWarning($"[HandManager] Hand parent rect is very small ({rt.rect.size}). Give it width/height.");

        if (_hlg != null)
        {
            // sensible defaults for card stacks
            _hlg.childControlWidth = false;
            _hlg.childControlHeight = false;
            _hlg.childForceExpandWidth = false;
            _hlg.childForceExpandHeight = false;
            _hlg.childAlignment = TextAnchor.MiddleCenter;
        }
    }

#if UNITY_EDITOR
    // this lets you preview spacing/angle changes in the editor (when not playing)
    private void OnValidate()
    {
        // very defensive: these accesses can happen before Start()
        if (_handView == null) return;

        // refresh cached ref if the parent changed in inspector
        _hlg ??= _handView.GetComponent<HorizontalLayoutGroup>();

        // quick in-editor relayout for visual feedback
        if (!Application.isPlaying)
        {
            if (_hlg != null)
            {
                _hlg.spacing = _spacing;
                // apply fan after Unity runs the layout pass in editor
                FanImmediately();
            }
            else
            {
                ManualFanLayout(_spacing);
            }
        }
    }
#endif

    #endregion

    #region Public API

    /// <summary>Canvas reference for UI rendering.</summary>
    public Canvas Canvas => _canvas;

    /// <summary>Deal a new hand using the manager's shuffled deck.</summary>
    [ContextMenu("Deal New Hand")]
    public void DealNewHand()
    {
        if (_manager == null || _handView == null) return;

        ClearHand();

        var deck = _manager.GetShuffledDeck(_randomSeed);
        int n = Mathf.Min(_handSize, deck.Count);

        _current.Capacity = Mathf.Max(_current.Capacity, n);
        for (int i = 0; i < n; i++)
            _current.Add(deck[i]);

        // spawn card visuals
        for (int i = 0; i < _current.Count; i++)
        {
            var go = _manager.SpawnCard(_current[i], _handView);
            // wire the spawned card back to this HandManager so CardDrag can call back without scene lookups
            var drag = go.GetComponent<CardDrag>();
            if (drag != null) drag.SetHandManager(this);
            EnsureCardVisual(go);

            // keep card images crisp and undistorted
            var img = go.GetComponentInChildren<Image>();
            if (img != null)
            {
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
        }

        RelayoutCurrentHand();
    }

    private void EnsureCardVisual(GameObject go)
    {
        var rt = go.transform as RectTransform;
        if (rt == null)
            Debug.LogError("[HandManager] Spawned card is not a RectTransform (must be a UI element).");

        // anchors centered to play nice with HLG rotations
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        // if the prefab has no size, give it a fallback size
        if (rt.rect.width < 1f || rt.rect.height < 1f)
            rt.sizeDelta = _fallbackCardSize;

        // make sure there is something to render
        var img = go.GetComponentInChildren<Image>(true);
        if (img == null)
            img = go.AddComponent<Image>(); // temp visual so you can see it

        if (img.sprite == null)
        {
            // no sprite? use a debug color so you *see* the rect
            img.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            img.raycastTarget = false;
        }
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
    }


    /// <summary>Remove all card instances from the hand and clear IDs.</summary>
    [ContextMenu("Clear Hand")]
    public void ClearHand()
    {
        if (_handView == null) return;

        for (int i = _handView.childCount - 1; i >= 0; i--)
            Destroy(_handView.GetChild(i).gameObject);

        _current.Clear();
    }

    /// <summary>
    /// Remove a card from the hand and leave it on the Canvas where it was dropped.
    /// CardDrag has already reparented the card to the Canvas and positioned it at the
    /// drop location, so this method only needs to update hand data and re-layout.
    /// </summary>
    public void PlayCard(RectTransform cardTransform)
    {
        if (cardTransform == null) return;

        var card = cardTransform.GetComponent<Card>();
        if (card == null) return;

        _current.Remove(card.Id);
        cardTransform.localRotation = Quaternion.identity;
        RelayoutCurrentHand();
    }

    #endregion

    #region Layout

    /// <summary>
    /// Re-layout the current hand. Uses HorizontalLayoutGroup if present (with overlap),
    /// otherwise applies a simple manual fan (position + rotation).
    /// </summary>
    private void RelayoutCurrentHand()
    {
        if (_handView == null) return;

        int count = _handView.childCount;
        if (count == 0) return;

        float applied = ComputeAppliedSpacing();

        if (_hlg != null)
        {
            if (_verboseLogs) Debug.Log($"[HandManager] HLG spacing={applied:F1} (preferred={_spacing:F1})");
            _hlg.spacing = applied;
            StopAllCoroutines();
        }
        else
        {
            if (_verboseLogs) Debug.LogWarning("[HandManager] No HorizontalLayoutGroup; using manual layout.");
            ManualFanLayout(applied);
        }
    }

    /// <summary>
    /// Compute the spacing to use this frame.
    /// Uses the inspector _spacing unless cards would overflow — in that case compresses
    /// just enough to fit, down to a hard floor of _minCardVisible fraction per card.
    /// </summary>
    private float ComputeAppliedSpacing()
    {
        int count = _handView.childCount;
        if (count <= 1) return _spacing;

        // Measure card width from the first child; fall back to the inspector default.
        float cardWidth = _fallbackCardSize.x;
        var firstCard = _handView.GetChild(0) as RectTransform;
        if (firstCard != null && firstCard.rect.width > 1f)
            cardWidth = firstCard.rect.width;

        float containerWidth = _handView.rect.width;
        if (containerWidth <= 1f) return _spacing; // layout not ready yet

        // Spacing that makes all cards exactly fill the container edge-to-edge.
        float fitSpacing = (containerWidth - cardWidth * count) / (count - 1);

        // Hardest allowed overlap: each card must show at least _minCardVisible of its width.
        float minSpacing = -(cardWidth * (1f - _minCardVisible));

        // Prefer _spacing; only compress when overflow would occur; never past the hard floor.
        float applied = Mathf.Max(Mathf.Min(_spacing, fitSpacing), minSpacing);

        if (applied <= minSpacing + 0.5f && fitSpacing < minSpacing)
            Debug.LogWarning($"[HandManager] {count} cards cannot fit in {containerWidth:F0}px without dropping below " +
                             $"{_minCardVisible * 100:F0}% visibility per card. Reduce hand size or increase HandViewPort width.");

        return applied;
    }

    /// <summary>
    /// Apply a simple manual layout (no HLG): centered X positions and Z-rotation fan.
    /// </summary>
    private void ManualFanLayout(float spacing)
    {
        int count = _handView.childCount;
        if (count == 0) return;

        float startX = -((count - 1) * 0.5f) * spacing;
        float startAngle = -((count - 1) * 0.5f) * _fanAngle;

        for (int i = 0; i < count; i++)
        {
            var rt = (RectTransform)_handView.GetChild(i);
            rt.anchoredPosition = new Vector2(startX + i * spacing, 0f);
            rt.localRotation = Quaternion.Euler(0, 0, startAngle + i * _fanAngle);
        }
    }

    /// <summary>
    /// After HLG places children on this frame, apply the Z-rotation fan next frame.
    /// This avoids fighting the layout system.
    /// </summary>
    private IEnumerator FanAfterLayout(int count)
    {
        // wait for end of frame to be extra safe w/ layout timing
        yield return _waitEndOfFrame;
        FanImmediately();
    }

    /// <summary>
    /// Apply fan rotation to current children immediately (positions assumed valid).
    /// </summary>
    private void FanImmediately()
    {
        int count = _handView.childCount;
        if (count == 0) return;

        float startAngle = -((count - 1) * 0.5f) * _fanAngle;
        for (int i = 0; i < count; i++)
        {
            var rt = (RectTransform)_handView.GetChild(i);
            rt.localRotation = Quaternion.Euler(0, 0, startAngle + i * _fanAngle);
        }
    }

    #endregion

    #region Internals

    /// <summary>Cache current inspector values to detect changes next frame.</summary>
    private void SnapshotSettings()
    {
        _lastHandSize = _handSize;
        _lastSeed = _randomSeed;
        _lastSpacing = _spacing;
        _lastFanAngle = _fanAngle;
    }

    #endregion

    // Exposed helpers for drag-and-drop support
    /// <summary>Return the RectTransform of the hand parent for hit-testing.</summary>
    public RectTransform GetHandRect() => _handView;

    /// <summary>
    /// Convert a local X position (in hand local space) to a sibling index where a dropped card should be inserted.
    /// </summary>
    public int GetSiblingIndexForLocalX(float localX)
    {
        int count = _handView.childCount - 1; // exclude dragging card
        if (count <= 0) return 0;

        // Build an array of child X positions
        var positions = new float[count];
        int idx = 0;
        for (int i = 0; i < count; i++)
        {
            var child = _handView.GetChild(i);
            positions[idx++] = ((RectTransform)child).anchoredPosition.x;
        }

        // if spacing is uniform, we can pick the slot by midpoints
        // find nearest slot by comparing to child X positions
        int insertAt = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < idx; i++)
        {
            float px = positions[i];
            float dist = Mathf.Abs(localX - px);
            if (dist < bestDist)
            {
                bestDist = dist;
                insertAt = i;
            }
        }

        // choose to insert after the nearest if pointer is to the right of it
        if (localX > positions[insertAt]) insertAt++;

        // clamp to valid sibling range
        return Mathf.Clamp(insertAt, 0, count);
    }

    /// <summary>
    /// Move the card transform (which MUST already be a direct child of the hand) to the requested sibling index
    /// and update the backed _current list accordingly. This method will NOT parent a non-child
    /// </summary>
    public void MoveCardToIndex(RectTransform cardTransform, int newIndex)
    {
        if (cardTransform == null || _handView == null) return;

        // Require the caller to pass a card that is already parented to the hand.
        if (cardTransform.parent != _handView)
        {
            Debug.LogWarning("[HandManager] MoveCardToIndex expects the card to be a direct child of the hand. ");
            return;
        }

        int currentIndex = cardTransform.GetSiblingIndex();

        // Clamp target against current child count, excluding the moving card.
        int max = _handView.childCount - 1;
        newIndex = Mathf.Clamp(newIndex, 0, max);

        // Account for removal shifting indices when moving forward.
        if (newIndex > currentIndex) newIndex--;
        cardTransform.SetSiblingIndex(newIndex);

        // Rebuild backing list to reflect child order
        _current.Clear();
        for (int i = 0; i < _handView.childCount; i++)
        {
            var child = _handView.GetChild(i);
            var card = child.GetComponent<Card>();
            if (card != null)
                _current.Add(card.Id);
        }

        RelayoutCurrentHand();
    }
}

/*
 * Unity lifecycle cheat-sheet for this component:
 *
 * Awake() — runs first, before Start(). Use it to cache component refs.
 * Start() — runs on the first enabled frame. Use it to initialize content (deal).
 * Update() — runs every frame. Here we watch for runtime inspector tweaks (size/seed/layout).
 * OnValidate() — (editor only) runs when values change in the inspector; lets you preview layout without Play.
 */
