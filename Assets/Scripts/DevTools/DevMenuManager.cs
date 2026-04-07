#if UNITY_EDITOR || DEV_BUILD
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// In-game developer menu. Press backtick (`) to toggle visibility.
///
/// Capabilities:
///   - Pick exact cards from a 4×13 grid and apply them as your hand.
///   - Deal N random cards.
///   - Clear staged selection (SelectionManager).
///   - Reset the current trick on the table (TrickManager).
///
/// Only compiled in UNITY_EDITOR and DEV_BUILD. Wire all SerializeField refs
/// in the Inspector after creating the panel hierarchy in GameScene.
/// </summary>
public class DevMenuManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject _panel;   // DevMenuPanel — NOT the same GO as this component

    [Header("Card Picker")]
    [SerializeField] private Transform        _cardGrid;
    [SerializeField] private CardManager      _cardManager;

    [Header("Hand Controls")]
    [SerializeField] private TMP_InputField _randomCountField;
    [SerializeField] private Button         _applyHandBtn;
    [SerializeField] private Button         _dealRandomBtn;

    [Header("Trick Controls")]
    [SerializeField] private Button _clearSelectionBtn;
    [SerializeField] private Button _resetTrickBtn;

    // ── Colours ──────────────────────────────────────────────────────────────

    private static readonly Color ColSelected   = new(0.25f, 0.70f, 0.35f, 1f); // green bg when picked
    private static readonly Color ColUnselected = new(1.00f, 1.00f, 1.00f, 1f); // white bg default
    private static readonly Color ColSuitRed    = new(0.80f, 0.10f, 0.10f, 1f); // red text
    private static readonly Color ColSuitBlack  = new(0.10f, 0.10f, 0.10f, 1f); // near-black text

    // ── State ────────────────────────────────────────────────────────────────

    private readonly HashSet<Card.CardId>             _picked     = new();
    private readonly Dictionary<Card.CardId, Graphic> _btnGraphic = new();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_cardManager == null)
            _cardManager = FindFirstObjectByType<CardManager>();

        BuildGrid();

        _applyHandBtn    .onClick.AddListener(OnApplyHand);
        _dealRandomBtn   .onClick.AddListener(OnDealRandom);
        _clearSelectionBtn.onClick.AddListener(OnClearSelection);
        _resetTrickBtn   .onClick.AddListener(OnResetTrick);

        _panel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
            TogglePanel();
    }

    public void TogglePanel() => _panel.SetActive(!_panel.activeSelf);

    // ── Grid ─────────────────────────────────────────────────────────────────

    private void BuildGrid()
    {
        if (_cardGrid == null || _cardManager == null) return;

        // Suits in row order: Hearts, Diamonds, Clubs, Spades
        var suits = new[] { Card.Suit.Hearts, Card.Suit.Diamonds, Card.Suit.Clubs, Card.Suit.Spades };
        // Ranks in column order: 2 through Ace
        var ranks = new[]
        {
            Card.Rank.Two, Card.Rank.Three, Card.Rank.Four, Card.Rank.Five,
            Card.Rank.Six, Card.Rank.Seven, Card.Rank.Eight, Card.Rank.Nine,
            Card.Rank.Ten, Card.Rank.Jack, Card.Rank.Queen, Card.Rank.King, Card.Rank.Ace
        };

        var deckSet = new HashSet<Card.CardId>(_cardManager.FullDeck);

        foreach (var suit in suits)
        {
            bool isRed = suit == Card.Suit.Hearts || suit == Card.Suit.Diamonds;
            Color labelCol = isRed ? ColSuitRed : ColSuitBlack;

            foreach (var rank in ranks)
            {
                var id = new Card.CardId(suit, rank, 0);
                if (!deckSet.Contains(id)) continue;

                var btn = CreateCardButton(id, labelCol);
                _btnGraphic[id] = btn.targetGraphic;
            }
        }
    }

    private Button CreateCardButton(Card.CardId id, Color labelColor)
    {
        var go  = new GameObject($"Btn_{id}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_cardGrid, false);

        var img = go.GetComponent<Image>();
        img.color = ColUnselected;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var captured = id;
        btn.onClick.AddListener(() => TogglePick(captured));

        // Label
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);

        var rt = labelGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text      = $"{RankAbbr(id.rank)}{SuitGlyph(id.suit)}";
        tmp.color     = labelColor;
        tmp.fontSize  = 14;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    private void TogglePick(Card.CardId id)
    {
        if (_picked.Contains(id))
        {
            _picked.Remove(id);
            if (_btnGraphic.TryGetValue(id, out var g)) g.color = ColUnselected;
        }
        else
        {
            _picked.Add(id);
            if (_btnGraphic.TryGetValue(id, out var g)) g.color = ColSelected;
        }
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void OnApplyHand()
    {
        if (_picked.Count == 0) return;
        HandManager.Instance.ReceiveHand(_picked.ToArray());
    }

    private void OnDealRandom()
    {
        int n = 10;
        if (_randomCountField != null && int.TryParse(_randomCountField.text, out int parsed))
            n = Mathf.Clamp(parsed, 1, 52);

        var deck = _cardManager.GetShuffledDeck();
        var hand = deck.Take(n).ToArray();
        HandManager.Instance.ReceiveHand(hand);
    }

    private void OnClearSelection() => SelectionManager.Instance.Clear();

    private void OnResetTrick()
    {
        if (TrickManager.Instance != null)
            TrickManager.Instance.StartTrick();
        else
            Debug.LogWarning("[DevMenu] TrickManager.Instance is null — scene may not be fully loaded.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string SuitGlyph(Card.Suit suit) => suit switch
    {
        Card.Suit.Hearts   => "♥",
        Card.Suit.Diamonds => "♦",
        Card.Suit.Clubs    => "♣",
        Card.Suit.Spades   => "♠",
        _                  => "?"
    };

    private static string RankAbbr(Card.Rank rank) => rank switch
    {
        Card.Rank.Ace   => "A",
        Card.Rank.King  => "K",
        Card.Rank.Queen => "Q",
        Card.Rank.Jack  => "J",
        Card.Rank.Ten   => "10",
        _               => ((int)rank).ToString()
    };
}
#endif
