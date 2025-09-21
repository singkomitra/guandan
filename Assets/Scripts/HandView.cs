using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CardManager _manager;
    [SerializeField] private RectTransform _handParent;

    [Header("Hand Settings")]
    [SerializeField, Range(1, 52)] private int _handSize = 10;
    [SerializeField] private int _randomSeed = 0;    // 0 = time-based
    [SerializeField] private float _spacing = -120f; // negative = overlap (for HLG) or step (manual)
    [SerializeField] private float _fanAngle = 8f;   // per-card Z rotation

    // runtime
    private readonly List<CardManager.CardId> _current = new();
    private HorizontalLayoutGroup _hlg;

    // cached to detect Inspector changes in Play Mode
    private int _lastHandSize, _lastSeed;
    private float _lastSpacing, _lastFanAngle;

    private void Awake()
    {
        _hlg = _handParent ? _handParent.GetComponent<HorizontalLayoutGroup>() : null;
    }

    private void Start()
    {
        if (_manager == null) _manager = FindObjectOfType<CardManager>();
        if (_manager == null) { Debug.LogError("[HandView] No CardManager in scene"); return; }
        if (_manager is { } && _manager.FullDeck.Count == 0)
            Debug.LogWarning("[HandView] CardManager has 0 cards — check Resources paths/filenames.");

        DealNewHand();
        SnapshotSettings();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        // if hand size or seed changed -> re-deal
        if (_handSize != _lastHandSize || _randomSeed != _lastSeed)
        {
            DealNewHand();
            SnapshotSettings();
            return;
        }

        // if layout knobs changed -> re-layout current children
        if (!Mathf.Approximately(_spacing, _lastSpacing) ||
            !Mathf.Approximately(_fanAngle, _lastFanAngle))
        {
            RelayoutCurrentHand();
            SnapshotSettings();
        }
    }

    private void SnapshotSettings()
    {
        _lastHandSize = _handSize;
        _lastSeed = _randomSeed;
        _lastSpacing = _spacing;
        _lastFanAngle = _fanAngle;
    }

    [ContextMenu("Deal New Hand")]
    public void DealNewHand()
    {
        ClearHand();

        var deck = _manager.GetShuffledDeck(_randomSeed);
        int n = Mathf.Min(_handSize, deck.Count);
        for (int i = 0; i < n; i++) _current.Add(deck[i]);

        for (int i = 0; i < _current.Count; i++)
        {
            var go = _manager.SpawnCard(_current[i], _handParent);
            var img = go.GetComponentInChildren<Image>();
            if (img) { img.type = Image.Type.Simple; img.preserveAspect = true; }
        }

        RelayoutCurrentHand();
    }

    [ContextMenu("Clear Hand")]
    public void ClearHand()
    {
        if (_handParent == null) return;
        for (int i = _handParent.childCount - 1; i >= 0; i--)
            Destroy(_handParent.GetChild(i).gameObject);
        _current.Clear();
    }

    public IReadOnlyList<CardManager.CardId> Current => _current;

    // --- layout ---
    private void RelayoutCurrentHand()
    {
        if (_handParent == null) return;
        int count = _handParent.childCount;
        if (count == 0) return;

        if (_hlg != null)
        {
            // Let HorizontalLayoutGroup place X; we control overlap & rotation
            _hlg.spacing = _spacing; // negative to overlap
            // Wait 1 frame then fan (ensures HLG has updated positions)
            StopAllCoroutines();
            StartCoroutine(FanAfterLayout(count));
        }
        else
        {
            // Manual position + rotation
            float startX = -((count - 1) * 0.5f) * _spacing;
            float startAngle = -((count - 1) * 0.5f) * _fanAngle;

            for (int i = 0; i < count; i++)
            {
                var rt = (RectTransform)_handParent.GetChild(i);
                rt.anchoredPosition = new Vector2(startX + i * _spacing, 0f);
                rt.localRotation = Quaternion.Euler(0, 0, startAngle + i * _fanAngle);
            }
        }
    }

    private System.Collections.IEnumerator FanAfterLayout(int count)
    {
        yield return null; // wait for HLG to do a layout pass
        float startAngle = -((count - 1) * 0.5f) * _fanAngle;
        for (int i = 0; i < count; i++)
        {
            var rt = (RectTransform)_handParent.GetChild(i);
            rt.localRotation = Quaternion.Euler(0, 0, startAngle + i * _fanAngle);
        }
    }
}
