using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Full-screen modal showing every set played in the current trick, oldest first.
/// Click anywhere to dismiss.
///
/// Scene setup:
///   1. Create a Canvas: Sort Order 100, Screen Space Overlay.
///   2. Add a CanvasGroup to the Canvas root.
///   3. Add a full-screen child Image (black, alpha 0.75) — this is the backdrop.
///      The backdrop handles click-to-dismiss via its own IPointerClickHandler.
///   4. Add a centred child RectTransform for _panel (entries are spawned here).
///   5. Attach this script to the Canvas root; wire _canvasGroup, _panel,
///      _trickManager in the Inspector.
/// </summary>
public class TrickHistoryOverlay : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup   _canvasGroup;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private TrickManager  _trickManager;

    [Header("Layout")]
    [SerializeField] private readonly float _rowHeight  = 120f;
    [SerializeField] private readonly float _cardWidth  = 70f;
    [SerializeField] private readonly float _cardHeight = 100f;
    [SerializeField] private readonly float _cardGap    = 8f;
    [SerializeField] private readonly float _labelWidth = 220f;
    [SerializeField] private readonly float _panelWidth = 900f;
    [SerializeField] private readonly float _padding    = 30f;

    [Header("Animation")]
    [SerializeField] private readonly float _fadeDuration = 0.15f;

    // ── Singleton ─────────────────────────────────────────────────────────────

    public static TrickHistoryOverlay Instance { get; private set; }

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly List<GameObject> _entries = new();
    private Coroutine  _fade;
    private CardManager _cardManager;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _canvasGroup.alpha          = 0f;
        _canvasGroup.blocksRaycasts = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()  => _trickManager.TrickStarted += OnTrickStarted;
    private void OnDisable() => _trickManager.TrickStarted -= OnTrickStarted;

    // ── Public API ────────────────────────────────────────────────────────────

    public static void Show(IReadOnlyList<TrickManager.PlayRecord> history, CardManager cardManager)
    {
        if (Instance == null || history.Count == 0) return;
        Instance._cardManager = cardManager;
        Instance.DoShow(history);
    }

    public static void Hide()
    {
        if (Instance != null) Instance.DoHide();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void OnTrickStarted() => HideImmediate();

    private void DoShow(IReadOnlyList<TrickManager.PlayRecord> history)
    {
        if (_fade != null) StopCoroutine(_fade);
        ClearEntries();
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha          = 0f;
        BuildEntries(history);
        _fade = StartCoroutine(FadeAlpha(0f, 1f));
    }

    private void DoHide()
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(FadeAlpha(_canvasGroup.alpha, 0f, () =>
        {
            ClearEntries();
            _canvasGroup.blocksRaycasts = false;
        }));
    }

    private void HideImmediate()
    {
        if (_fade != null) { StopCoroutine(_fade); _fade = null; }
        ClearEntries();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.blocksRaycasts = false;
    }

    // ── Entry building ───────────────────────────────────────────────────────

    private void BuildEntries(IReadOnlyList<TrickManager.PlayRecord> history)
    {
        int   count       = history.Count;
        float totalHeight = count * _rowHeight + _padding * 2f;
        _panel.sizeDelta  = new Vector2(_panelWidth, totalHeight);

        float topY = totalHeight * 0.5f - _padding - _rowHeight * 0.5f;

        for (int i = 0; i < count; i++)
            _entries.Add(BuildRow(history[i], i, topY - i * _rowHeight));
    }

    private GameObject BuildRow(TrickManager.PlayRecord record, int index, float rowY)
    {
        var rowGO = new GameObject($"HistoryRow_{index}");
        rowGO.transform.SetParent(_panel, worldPositionStays: false);
        var rowRt              = rowGO.AddComponent<RectTransform>();
        rowRt.anchorMin        = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot            = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta        = new Vector2(_panelWidth - _padding * 2f, _rowHeight);
        rowRt.anchoredPosition = new Vector2(0f, rowY);

        float halfWidth = (_panelWidth - _padding * 2f) * 0.5f;

        AddLabel(rowGO.transform, PlayerLabel(record.PlayerId),
                 x: -halfWidth + _labelWidth * 0.5f,
                 alignment: TextAlignmentOptions.Left);

        SpawnCards(record.Cards, rowGO.transform);

        AddLabel(rowGO.transform, record.Result?.Description ?? string.Empty,
                 x: halfWidth - _labelWidth * 0.5f,
                 alignment: TextAlignmentOptions.Right);

        return rowGO;
    }

    private void SpawnCards(IReadOnlyList<Card.CardId> cards, Transform parent)
    {
        if (_cardManager == null) return;
        float totalWidth = cards.Count * _cardWidth + (cards.Count - 1) * _cardGap;
        float startX     = -totalWidth * 0.5f + _cardWidth * 0.5f;

        for (int j = 0; j < cards.Count; j++)
        {
            var go = _cardManager.SpawnCard(cards[j], parent);
            if (go == null) continue;

            DisableInteraction(go);

            var rt              = (RectTransform)go.transform;
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(_cardWidth, _cardHeight);
            rt.anchoredPosition = new Vector2(startX + j * (_cardWidth + _cardGap), 0f);
            rt.localScale       = Vector3.one;
            rt.localRotation    = Quaternion.identity;
        }
    }

    private void AddLabel(Transform parent, string text, float x, TextAlignmentOptions alignment)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, worldPositionStays: false);
        var rt              = go.AddComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta        = new Vector2(_labelWidth, 60f);
        var tmp                  = go.AddComponent<TextMeshProUGUI>();
        tmp.text                 = text;
        tmp.alignment            = alignment;
        tmp.enableAutoSizing     = true;
        tmp.fontSizeMin          = 16f;
        tmp.fontSizeMax          = 28f;
        tmp.enableWordWrapping   = false;
        tmp.color                = Color.white;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DisableInteraction(GameObject go)
    {
        var hover      = go.GetComponent<CardHover>();
        var selectable = go.GetComponent<CardSelectable>();
        if (hover      != null) hover.enabled      = false;
        if (selectable != null) selectable.enabled = false;
        var img = go.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
    }

    private void ClearEntries()
    {
        foreach (var go in _entries.Where(go => go != null))
            Destroy(go);
        _entries.Clear();
    }

    private IEnumerator FadeAlpha(float from, float to, Action onDone = null)
    {
        float elapsed      = 0f;
        _canvasGroup.alpha = from;
        while (elapsed < _fadeDuration)
        {
            elapsed           += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / _fadeDuration));
            yield return null;
        }
        _canvasGroup.alpha = to;
        _fade              = null;
        onDone?.Invoke();
    }

    private static string PlayerLabel(int id) => id < 0 ? "You" : $"Player {id + 1}";
}

