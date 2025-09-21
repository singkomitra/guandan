using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central loader/factory for UI cards.
/// Expects sprites under:
///   Resources/Cards/Hearts, /Spades, /Diamonds, /Clubs
/// Your Card prefab must have the "Card" MonoBehaviour with SetupCard(Sprite).
/// </summary>
public class CardManager : MonoBehaviour
{
    [Header("Prefabs & Parents")]
    [SerializeField] private GameObject _cardPrefab;     // UI prefab with Card script + Image
    [SerializeField] private Transform _defaultParent;   // e.g., a grid/hand panel under a Canvas

    [Header("Resource Folders (relative to Resources/)")]
    [SerializeField] private string _heartsPath = "Cards/Hearts";
    [SerializeField] private string _spadesPath = "Cards/Spades";
    [SerializeField] private string _diamondsPath = "Cards/Diamonds";
    [SerializeField] private string _clubsPath = "Cards/Clubs";
    [SerializeField] private bool _logOnAwake = true;

    public int LoadedCount => _fullDeck.Count;

    [ContextMenu("Rebuild & Report")]
    private void RebuildAndReport()
    {
        BuildIndex();
        Debug.Log($"CardManager: loaded {_fullDeck.Count} cards");
    }

    // ---------- data ----------
    private readonly Dictionary<Card.CardId, Sprite> _spriteById = new();
    private readonly List<Card.CardId> _fullDeck = new(52);

    // ---------- lifecycle ----------
    private void Awake()
    {
        BuildIndex();
        if (_logOnAwake)
        {
            Debug.Log($"[CardManager] Loaded {LoadedCount} cards");
            // show a few examples
            int shown = 0;
            foreach (var kv in _spriteById)
            {
                Debug.Log($"  + {kv.Key} -> '{kv.Value.name}'");
                if (++shown >= 6) break;
            }
        }
    }

    private void BuildIndex()
    {
        _spriteById.Clear();
        _fullDeck.Clear();

        LoadSuit(Card.Suit.Hearts, _heartsPath);
        LoadSuit(Card.Suit.Spades, _spadesPath);
        LoadSuit(Card.Suit.Diamonds, _diamondsPath);
        LoadSuit(Card.Suit.Clubs, _clubsPath);

        if (_spriteById.Count == 0)
            Debug.LogError("CardManager: No sprites found. Check your Resources paths.");
    }

    private void LoadSuit(Card.Suit suit, string path)
    {
        var sprites = Resources.LoadAll<Sprite>(path);
        if (sprites == null || sprites.Length == 0) return;

        foreach (var s in sprites)
        {
            var name = s.name.Trim().ToLowerInvariant();   // e.g., "7_club", "10_spades", "j_heart", "a"
            string rankToken = name;
            string suitToken = null;

            int us = name.IndexOf('_');
            if (us >= 0)
            {
                rankToken = name.Substring(0, us);
                suitToken = name.Substring(us + 1).Trim();   // part after '_'
                if (suitToken.EndsWith("s"))                 // singularize: clubs -> club
                    suitToken = suitToken[..^1];
            }

            // if filename includes a suit, ensure it matches the folder suit
            if (!string.IsNullOrEmpty(suitToken) && !SuitMatches(suit, suitToken))
                continue;

            if (TryParseRank(rankToken, out var rank))
            {
                var id = new Card.CardId(suit, rank);
                if (!_spriteById.ContainsKey(id))
                {
                    _spriteById.Add(id, s);
                    if (!_fullDeck.Contains(id)) _fullDeck.Add(id);
                }
            }
            // check if loaded
            if (!_spriteById.ContainsKey(new Card.CardId(suit, rank)))
                Debug.LogWarning($"CardManager: Could not parse rank from sprite name '{name}' in {path}");
        }

        // --- local helpers ---
        static bool SuitMatches(Card.Suit target, string token)
        {
            return token switch
            {
                "club" => target == Card.Suit.Clubs,
                "diamond" => target == Card.Suit.Diamonds,
                "heart" => target == Card.Suit.Hearts,
                "spade" => target == Card.Suit.Spades,
                _ => true // unknown token -> don't block
            };
        }

    }
    // accepts: 2..10, 1,11,12,13  and  a/j/q/k (or "ace","jack","queen","king")
    private static bool TryParseRank(string token, out Card.Rank rank)
    {
        token = token.Trim().ToLowerInvariant();

        // numeric filenames (e.g., "11", "1", "7")
        if (int.TryParse(token, out int v))
        {
            switch (v)
            {
                case 1: rank = Card.Rank.Ace; return true; // many packs use 1 for Ace
                case 11: rank = Card.Rank.Jack; return true;
                case 12: rank = Card.Rank.Queen; return true;
                case 13: rank = Card.Rank.King; return true;
                default:
                    if (v >= 2 && v <= 10) { rank = (Card.Rank)v; return true; }
                    break;
            }
        }

        // letter/word aliases
        switch (token)
        {
            case "a":
            case "ace": rank = Card.Rank.Ace; return true;
            case "k":
            case "king": rank = Card.Rank.King; return true;
            case "q":
            case "queen": rank = Card.Rank.Queen; return true;
            case "j":
            case "jack": rank = Card.Rank.Jack; return true;
        }

        rank = Card.Rank.Two; // dummy
        return false;
    }


    // ---------- queries & spawns ----------
    public IReadOnlyList<Card.CardId> FullDeck => _fullDeck;

    public Sprite GetSprite(Card.CardId id)
    {
        _spriteById.TryGetValue(id, out var s);
        return s;
    }

    public GameObject SpawnCard(Card.CardId id, Transform parent = null)
    {
        if (!_spriteById.TryGetValue(id, out var sprite))
        {
            Debug.LogError($"CardManager: No sprite for {id}");
            return null;
        }

        var p = parent == null ? _defaultParent : parent;
        var go = Instantiate(_cardPrefab, p);
        var card = go.GetComponent<Card>();
        if (card == null) Debug.LogError("CardManager: prefab missing Card component.");
        else card.SetupCard(sprite, id);
        return go;
    }

    /// <summary>Returns a fresh shuffled copy of the deck.</summary>
    public List<Card.CardId> GetShuffledDeck(int seed = 0)
    {
        var list = new List<Card.CardId>(_fullDeck);
        var rng = seed == 0 ? new System.Random() : new System.Random(seed);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
