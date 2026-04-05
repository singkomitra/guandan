using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Animates played cards from their hand positions to the centre of the table.
/// Earlier sets in the same trick remain visible behind the current set (stacked,
/// slightly smaller). All cards are destroyed when the trick ends.
///
/// Local plays: driven by HandManager.CardsPlayed — receives the actual card GOs
/// and flies them from their current world position to the table centre.
/// Remote plays (future): driven by TrickManager.SetPlayed — spawns fresh GOs.
///
/// Scene setup:
///   1. Attach to any persistent GameObject.
///   2. Place "TableCardDisplay" as a direct Canvas child (sibling of TableArea,
///      NOT inside it). Anchor centre, sizeDelta 0,0, scale 1,1,1.
///   3. Wire _cardManager, _trickManager, _cardContainer in the Inspector.
/// </summary>
public class TableDisplay : MonoBehaviour, IPointerClickHandler
{
    [Header("Refs")]
    [SerializeField] private CardManager   _cardManager;
    [SerializeField] private TrickManager  _trickManager;
    [SerializeField] private RectTransform _cardContainer;

    [Header("Layout")]
    [Tooltip("Horizontal distance between card centres.")]
    [SerializeField] private float _cardSpacing = 110f;

    [Header("Animation")]
    [Tooltip("Lerp speed for all card animations.")]
    [SerializeField] private float _animSpeed = 12f;
    [Tooltip("Scale of the most recently played set.")]
    [SerializeField, Range(0.1f, 1f)] private float _cardDisplayScale = 0.6f;
    [Tooltip("Scale multiplier applied to sets behind the current one.")]
    [SerializeField, Range(0.1f, 1f)] private float _backgroundScaleFactor = 0.8f;

    // ── State ────────────────────────────────────────────────────────────────

    private readonly List<GameObject> _currentCards  = new(); // most recent set
    private readonly List<GameObject> _previousCards = new(); // earlier sets this trick
    private readonly List<GameObject> _exitingCards  = new(); // mid-exit animation

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Make _cardContainer hit-testable so IPointerClickHandler fires.
        var img           = _cardContainer.gameObject.AddComponent<Image>();
        img.color         = Color.clear;
        img.raycastTarget = true;
        if (_cardContainer.sizeDelta == Vector2.zero)
            _cardContainer.sizeDelta = new Vector2(600f, 400f);
    }

    private void OnEnable()
    {
        HandManager.CardsPlayed    += OnCardsPlayed;
        _trickManager.TrickStarted += OnTrickStarted;
    }

    private void OnDisable()
    {
        HandManager.CardsPlayed    -= OnCardsPlayed;
        _trickManager.TrickStarted -= OnTrickStarted;

        StopAllCoroutines();
        DestroyAll(_currentCards);
        DestroyAll(_previousCards);
        DestroyAll(_exitingCards);
    }

    // ── Click ────────────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData _)
    {
        if (_trickManager.TrickHistory.Count > 0)
            TrickHistoryOverlay.Show(_trickManager.TrickHistory, _cardManager);
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnCardsPlayed(IReadOnlyList<(Card.CardId id, RectTransform rt)> cards)
    {
        StopAllCoroutines();
        DestroyAll(_exitingCards);

        // Push the current set into the background.
        var backgroundScale = Vector3.one * (_cardDisplayScale * _backgroundScaleFactor);
        foreach (var go in _currentCards)
        {
            if (go == null) continue;
            var brt = go.GetComponent<RectTransform>();
            _previousCards.Add(go);
            StartCoroutine(FlyToTarget(brt, brt.anchoredPosition, backgroundScale));
        }
        _currentCards.Clear();

        // Fly new cards to the table centre.
        float totalWidth = (cards.Count - 1) * _cardSpacing;
        float startX     = -totalWidth * 0.5f;

        for (int i = 0; i < cards.Count; i++)
        {
            var (_, rt) = cards[i];
            if (rt == null) continue;

            DisableInteraction(rt.gameObject);
            rt.SetParent(_cardContainer, worldPositionStays: true);
            rt.SetAsLastSibling();

            _currentCards.Add(rt.gameObject);
            StartCoroutine(FlyToTarget(rt, new Vector2(startX + i * _cardSpacing, 0f),
                                       Vector3.one * _cardDisplayScale));
        }
    }

    private void OnTrickStarted()
    {
        StopAllCoroutines();

        var toExit = new List<GameObject>(_currentCards.Count + _previousCards.Count);
        toExit.AddRange(_currentCards);
        toExit.AddRange(_previousCards);
        _currentCards.Clear();
        _previousCards.Clear();
        _exitingCards.AddRange(toExit);

        foreach (var go in toExit)
        {
            if (go == null) continue;
            var captured = go;
            var rt = captured.GetComponent<RectTransform>();
            StartCoroutine(FlyToTarget(rt, rt.anchoredPosition, Vector3.zero, () =>
            {
                _exitingCards.Remove(captured);
                Destroy(captured);
            }));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DisableInteraction(GameObject go)
    {
        // CardDrag is intentionally NOT disabled here.
        // OnDrop fires before OnEndDrag; disabling CardDrag here prevents OnEndDrag from
        // firing, which means GroupDragHandler.OnAnyDragEnd never runs, _isGroupDragging
        // stays true, and LateUpdate keeps driving followers with mouse movement.
        // raycastTarget = false on the Image is sufficient to block new interactions.
        var hover      = go.GetComponent<CardHover>();
        var selectable = go.GetComponent<CardSelectable>();
        if (hover      != null) hover.enabled      = false;
        if (selectable != null) selectable.enabled = false;

        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null) cg.interactable = false;

        var img = go.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
    }

    private static void DestroyAll(List<GameObject> list)
    {
        foreach (var go in list)
            if (go != null) Destroy(go);
        list.Clear();
    }

    private IEnumerator FlyToTarget(RectTransform rt, Vector2 targetPos, Vector3 targetScale, Action onDone = null)
    {
        while (rt != null)
        {
            bool posSettled   = Vector2.Distance(rt.anchoredPosition, targetPos)           < 0.5f;
            bool scaleSettled = Vector3.Distance(rt.localScale,       targetScale)         < 0.01f;
            bool rotSettled   = Quaternion.Angle(rt.localRotation,    Quaternion.identity) < 0.5f;
            if (posSettled && scaleSettled && rotSettled) break;

            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos,           Time.deltaTime * _animSpeed);
            rt.localScale       = Vector3.Lerp(rt.localScale,       targetScale,         Time.deltaTime * _animSpeed);
            rt.localRotation    = Quaternion.Lerp(rt.localRotation, Quaternion.identity, Time.deltaTime * _animSpeed);
            yield return null;
        }
        if (rt != null)
        {
            rt.anchoredPosition = targetPos;
            rt.localScale       = targetScale;
            rt.localRotation    = Quaternion.identity;
        }
        onDone?.Invoke();
    }
}
