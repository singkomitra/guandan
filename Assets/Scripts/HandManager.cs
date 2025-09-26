using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a "hand" of card prefabs under a parent RectTransform,
/// dealing a shuffled subset from CardManager and laying them out
/// either with a HorizontalLayoutGroup (overlap via negative spacing)
/// or with a simple manual fan (position + Z-rotation).
/// </summary>
public class HandView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CardManager _manager;
    [SerializeField] private RectTransform _handView;

    [Header("Hand Settings")]
    [Tooltip("How many cards to show in the hand.")]
    [SerializeField, Range(1, 52)] private int _handSize = 10;

    [Tooltip("0 = time-based seed (let CardManager decide). Non-zero = deterministic.")]
    [SerializeField] private int _randomSeed = 0;

    [Tooltip("Horizontal spacing between cards. Negative to overlap (with HLG) or to step (manual).")]
    [SerializeField] private float _spacing = -120f;

    [Tooltip("Per-card Z rotation (degrees) for the fanned look.")]
    [SerializeField] private float _fanAngle = 8f;

    [Header("Debug/UX")]
    [Tooltip("Enable extra Debug.Log messages.")]
    [SerializeField] private bool _verboseLogs = false;

    [Header("Card Defaults (if prefab provides no size)")]
    [SerializeField] private Vector2 _fallbackCardSize = new(140, 200);

    // runtime
    private readonly List<Card.CardId> _current = new();
    private HorizontalLayoutGroup _hlg;

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
    }

    private void Start()
    {
        // try to find CardManager if not wired in the inspector
        if (_manager == null) _manager = FindObjectOfType<CardManager>();

        if (_manager == null)
        {
            Debug.LogError("[HandView] No CardManager in scene.");
            enabled = false;
            return;
        }

        if (_handView == null)
        {
            Debug.LogError("[HandView] Hand parent is not assigned.");
            enabled = false;
            return;
        }

        if (_manager.FullDeck.Count == 0)
            Debug.LogWarning("[HandView] CardManager has 0 cards — check Resources paths/filenames.");

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
            Debug.LogError("[HandView] Hand parent is not under a Canvas (UI won’t render).");

        var rt = _handView as RectTransform;
        if (rt != null && (rt.rect.width <= 1f || rt.rect.height <= 1f))
            Debug.LogWarning($"[HandView] Hand parent rect is very small ({rt.rect.size}). Give it width/height.");

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
                ManualFanLayout();
            }
        }
    }
#endif

    #endregion

    #region Public API

    /// <summary>Current dealt card IDs (read-only).</summary>
    public IReadOnlyList<Card.CardId> Current => _current;

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
            Debug.LogError("[HandView] Spawned card is not a RectTransform (must be a UI element).");

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

        if (_hlg != null)
        {
            if (_verboseLogs) Debug.Log("[HandView] Using HorizontalLayoutGroup for hand layout.");
            _hlg.spacing = _spacing; // negative to overlap

            // Ensure HLG has updated positions before we fan
            StopAllCoroutines();
            StartCoroutine(FanAfterLayout(count));
        }
        else
        {
            if (_verboseLogs) Debug.LogWarning("[HandView] No HorizontalLayoutGroup; using manual layout.");
            ManualFanLayout();
        }
    }

    /// <summary>
    /// Apply a simple manual layout (no HLG): centered X positions and Z-rotation fan.
    /// </summary>
    private void ManualFanLayout()
    {
        int count = _handView.childCount;
        if (count == 0) return;

        float startX = -((count - 1) * 0.5f) * _spacing;
        float startAngle = -((count - 1) * 0.5f) * _fanAngle;

        for (int i = 0; i < count; i++)
        {
            var rt = (RectTransform)_handView.GetChild(i);
            rt.anchoredPosition = new Vector2(startX + i * _spacing, 0f);
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
}

/*
 * Unity lifecycle cheat-sheet for this component:
 *
 * Awake() — runs first, before Start(). Use it to cache component refs.
 * Start() — runs on the first enabled frame. Use it to initialize content (deal).
 * Update() — runs every frame. Here we watch for runtime inspector tweaks (size/seed/layout).
 * OnValidate() — (editor only) runs when values change in the inspector; lets you preview layout without Play.
 */
