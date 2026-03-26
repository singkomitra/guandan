using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure logic class — zero Unity dependencies except Debug logging.
/// Single source of truth for all Guandan set and bomb rules.
/// Unit-testable with plain NUnit, no scene required.
/// </summary>
public static class SetValidator
{
    public enum SetType
    {
        Unknown,
        Single, Pair, Triple,
        FullHouse,
        Straight,
        ConsecutiveTriplePairs,   // two consecutive triples, e.g. 444 555
        TripleConsecutivePairs,   // three consecutive pairs,  e.g. 33 44 55
        // Bombs — everything from Bomb4 onward. IsBomb uses >= Bomb4.
        Bomb4, Bomb5, Bomb6, Bomb7, Bomb8,
        StraightFlush,
        JokerBomb
    }

    public enum FailCode
    {
        None,
        NoCardsSelected,
        NotAValidSet,
        WrongSetType,   // must match the required set type
        DoesNotBeat,    // valid set but does not beat the current table set
        NotYourTurn,
    }

    public class ValidationResult
    {
        public bool      IsValid;
        public SetType   Type;
        public FailCode  Code;
        public string    Description;
        public string    FailReason;
        /// <summary>Rank used to compare sets of the same type (e.g. triple rank for Full House, starting rank for Straight).</summary>
        public Card.Rank KeyRank;
    }

    /// <summary>
    /// Injected by TurnManager once turn enforcement is implemented.
    /// Null = no constraints (first player in round).
    /// </summary>
    public class GameContext
    {
        public SetType?         RequiredType;
        public ValidationResult MustBeat;    // null = no constraint
        public Card.Rank        TrumpRank = Card.Rank.Two;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Full validation: set recognition + context constraints.
    /// Called only on commit — never during selection.
    /// </summary>
    public static ValidationResult Validate(
        IReadOnlyList<Card.CardId> cards,
        GameContext context = null)
    {
        if (cards.Count == 0)
        {
            Debug.Log("[SetValidator] Validate: no cards selected.");
            return Fail(FailCode.NoCardsSelected, "No cards selected");
        }

        var trumpRank = context?.TrumpRank ?? Card.Rank.Two;
        Debug.Log($"[SetValidator] Validate: {cards.Count} card(s), trump={trumpRank}");

        var result = TryIdentify(cards, trumpRank);

        if (result == null)
        {
            Debug.Log("[SetValidator] Validate: not a valid Guandan set.");
            return Fail(FailCode.NotAValidSet, "Not a valid Guandan set");
        }

        Debug.Log($"[SetValidator] Identified: {result.Type} — {result.Description}");

        if (context != null)
        {
            // Bombs bypass the required-type lock; regular sets must match.
            if (context.RequiredType.HasValue &&
                result.Type != context.RequiredType.Value &&
                !IsBomb(result.Type))
            {
                Debug.Log($"[SetValidator] Wrong type: need {context.RequiredType.Value}, got {result.Type}.");
                return Fail(FailCode.WrongSetType, $"Must play a {FriendlyTypeName(context.RequiredType.Value)} or a bomb");
            }

            if (context.MustBeat != null && !Beats(result, context.MustBeat, trumpRank))
            {
                Debug.Log($"[SetValidator] Does not beat table set: {context.MustBeat.Description}.");
                return Fail(FailCode.DoesNotBeat, $"Does not beat {context.MustBeat.Description}");
            }
        }

        Debug.Log($"[SetValidator] Valid: {result.Type} — {result.Description}");
        return result;
    }

    /// <summary>
    /// Neutral identification shown in the staging bar during selection.
    /// Never returns a valid/invalid judgment — descriptive only.
    /// </summary>
    public static string Describe(IReadOnlyList<Card.CardId> cards)
    {
        if (cards.Count == 0) return string.Empty;
        // Use trump Two as default since we have no context during selection.
        var result = TryIdentify(cards, Card.Rank.Two);
        return result != null
            ? result.Description
            : $"{cards.Count} card{(cards.Count == 1 ? "" : "s")}";
    }

    /// <summary>
    /// Returns true when <paramref name="challenger"/> beats <paramref name="tableSet"/>.
    /// Handles bomb-vs-non-bomb promotion and the full bomb hierarchy.
    /// Mismatched non-bomb types always return false.
    /// </summary>
    public static bool Beats(
        ValidationResult challenger,
        ValidationResult tableSet,
        Card.Rank trumpRank = Card.Rank.Two)
    {
        if (challenger == null || tableSet == null)   return false;
        if (!challenger.IsValid || !tableSet.IsValid) return false;

        bool chalBomb  = IsBomb(challenger.Type);
        bool tableBomb = IsBomb(tableSet.Type);

        if (chalBomb && !tableBomb) return true;   // bomb always beats any non-bomb
        if (!chalBomb && tableBomb) return false;  // non-bomb can never beat a bomb

        if (chalBomb) // both bombs: compare strength tier then rank
        {
            int cs = BombStrength(challenger.Type);
            int ts = BombStrength(tableSet.Type);
            if (cs != ts) return cs > ts;
            return KeyRankValue(challenger.Type, challenger.KeyRank, trumpRank)
                 > KeyRankValue(tableSet.Type,   tableSet.KeyRank,   trumpRank);
        }

        // Both non-bombs: must match type.
        // When called from Validate(), type-mismatch is already caught by the WrongSetType
        // check above, so this branch is dead in the normal flow. It is kept here so that
        // direct callers of the standalone Beats() API are handled correctly.
        if (challenger.Type != tableSet.Type) return false;

        return KeyRankValue(challenger.Type, challenger.KeyRank, trumpRank)
             > KeyRankValue(tableSet.Type,   tableSet.KeyRank,   trumpRank);
    }

    // -------------------------------------------------------------------------
    // Core recognizer
    // -------------------------------------------------------------------------

    private static ValidationResult TryIdentify(IReadOnlyList<Card.CardId> cards, Card.Rank trumpRank)
    {
        int n = cards.Count;

        // --- Bombs (can always be played) ---
        if (TryJokerBomb(cards, out string desc))                        return Identified(SetType.JokerBomb, desc, cards);
        if (n >= 5 && TryStraightFlush(cards, trumpRank, out desc))      return Identified(SetType.StraightFlush, desc, cards);
        if (n >= 4 && n <= 8 && TryNBomb(cards, n, out desc))            return Identified(NBombType(n), desc, cards);

        // --- Regular sets ---
        if (n == 1) return Identified(SetType.Single, SingleDesc(cards[0]), cards);
        if (n == 2 && TryPair(cards, out desc))                          return Identified(SetType.Pair, desc, cards);
        if (n == 3 && TryTriple(cards, out desc))                        return Identified(SetType.Triple, desc, cards);
        if (n == 5 && TryFullHouse(cards, out desc))                     return Identified(SetType.FullHouse, desc, cards);
        if (n >= 5 && TryStraight(cards, out desc))                      return Identified(SetType.Straight, desc, cards);
        if (n == 6 && TryConsecutiveTriplePairs(cards, out desc))        return Identified(SetType.ConsecutiveTriplePairs, desc, cards);
        if (n == 6 && TryTripleConsecutivePairs(cards, out desc))        return Identified(SetType.TripleConsecutivePairs, desc, cards);

        return null;
    }

    // -------------------------------------------------------------------------
    // Bomb detectors
    // -------------------------------------------------------------------------

    /// <summary>Joker Bomb: exactly 2 Black Jokers + 2 Red Jokers.</summary>
    private static bool TryJokerBomb(IReadOnlyList<Card.CardId> cards, out string desc)
    {
        desc = null;
        if (cards.Count != 4) return false;
        int black = 0, red = 0;
        foreach (var c in cards)
        {
            if      (c.rank == Card.Rank.BlackJoker) black++;
            else if (c.rank == Card.Rank.RedJoker)   red++;
            else return false;
        }
        if (black != 2 || red != 2) return false;
        desc = "Joker Bomb";
        return true;
    }

    /// <summary>N-Bomb: N cards of the same rank (no jokers). N in [4, 8].</summary>
    private static bool TryNBomb(IReadOnlyList<Card.CardId> cards, int n, out string desc)
    {
        desc = null;
        var first = cards[0];
        if (IsJoker(first)) return false;
        foreach (var c in cards)
            if (c.rank != first.rank || IsJoker(c)) return false;
        desc = $"{n}-Bomb of {RankName(first.rank, plural: true)}";
        return true;
    }

    /// <summary>
    /// Straight Flush: 5+ consecutive cards all of the same suit.
    /// Trump-of-Hearts wildcard is not yet implemented (TODO).
    /// </summary>
    private static bool TryStraightFlush(
        IReadOnlyList<Card.CardId> cards, Card.Rank trumpRank, out string desc)
    {
        desc = null;
        foreach (var c in cards) if (IsJoker(c)) return false;

        var suit = cards[0].suit;
        foreach (var c in cards) if (c.suit != suit) return false;

        var sorted = SortedByRank(cards);
        if (!AreStrictlyConsecutive(sorted)) return false;

        desc = $"Straight Flush {RankName(sorted[0].rank, false)}–{RankName(sorted[sorted.Count - 1].rank, false)} ({suit})";
        return true;
    }

    // -------------------------------------------------------------------------
    // Regular set detectors
    // -------------------------------------------------------------------------

    private static string SingleDesc(Card.CardId c)
    {
        if (c.rank == Card.Rank.BlackJoker) return "Black Joker";
        if (c.rank == Card.Rank.RedJoker)   return "Red Joker";
        return $"{RankName(c.rank, false)} of {c.suit}";
    }

    /// <summary>Pair: two cards of the same rank. Joker pairs must be same color.</summary>
    private static bool TryPair(IReadOnlyList<Card.CardId> cards, out string desc)
    {
        desc = null;
        var a = cards[0];
        var b = cards[1];

        // Joker pair: both must be the same joker rank
        if (IsJoker(a) || IsJoker(b))
        {
            if (a.rank != b.rank) return false;
            desc = a.rank == Card.Rank.BlackJoker ? "Pair of Black Jokers" : "Pair of Red Jokers";
            return true;
        }

        if (a.rank != b.rank) return false;
        desc = $"Pair of {RankName(a.rank, plural: true)}";
        return true;
    }

    /// <summary>Triple: three cards of the same non-joker rank.</summary>
    private static bool TryTriple(IReadOnlyList<Card.CardId> cards, out string desc)
    {
        desc = null;
        var r = cards[0].rank;
        if (IsJokerRank(r)) return false;
        foreach (var c in cards) if (c.rank != r) return false;
        desc = $"Triple {RankName(r, plural: true)}";
        return true;
    }

    /// <summary>Full House: one triple + one pair, no jokers.</summary>
    private static bool TryFullHouse(IReadOnlyList<Card.CardId> cards, out string desc)
    {
        desc = null;
        foreach (var c in cards) if (IsJoker(c)) return false;

        var groups = GroupByRank(cards);
        if (groups.Count != 2) return false;

        Card.Rank tripleRank = default, pairRank = default;
        bool foundTriple = false, foundPair = false;
        foreach (var kv in groups)
        {
            if      (kv.Value == 3) { tripleRank = kv.Key; foundTriple = true; }
            else if (kv.Value == 2) { pairRank   = kv.Key; foundPair   = true; }
            else return false;
        }
        if (!foundTriple || !foundPair) return false;

        desc = $"Full House: {RankName(tripleRank, plural: true)} over {RankName(pairRank, plural: true)}";
        return true;
    }

    /// <summary>
    /// Straight: 5+ cards with strictly consecutive, distinct ranks — no jokers, no duplicates.
    /// Does not wrap (A–2 is invalid). Trump-of-Hearts wildcard is not yet implemented (TODO).
    /// </summary>
    private static bool TryStraight(IReadOnlyList<Card.CardId> cards, out string desc)
    {
        desc = null;
        foreach (var c in cards) if (IsJoker(c)) return false;

        var sorted = SortedByRank(cards);
        if (!AreStrictlyConsecutive(sorted)) return false;

        desc = $"Straight {RankName(sorted[0].rank, false)}–{RankName(sorted[sorted.Count - 1].rank, false)}";
        return true;
    }

    /// <summary>Pair of Consecutive Triples: exactly two triples of consecutive rank (e.g. 444 555).</summary>
    private static bool TryConsecutiveTriplePairs(IReadOnlyList<Card.CardId> cards, out string desc)
    {
        desc = null;
        foreach (var c in cards) if (IsJoker(c)) return false;

        var groups = GroupByRank(cards);
        if (groups.Count != 2) return false;
        foreach (var kv in groups) if (kv.Value != 3) return false;

        var ranks = SortedRankKeys(groups);
        if ((int)ranks[1] - (int)ranks[0] != 1) return false;

        desc = $"Consecutive Triples {RankName(ranks[0], false)}–{RankName(ranks[1], false)}";
        return true;
    }

    /// <summary>Triple Consecutive Pairs: exactly three pairs of consecutive rank (e.g. 33 44 55).</summary>
    private static bool TryTripleConsecutivePairs(IReadOnlyList<Card.CardId> cards, out string desc)
    {
        desc = null;
        foreach (var c in cards) if (IsJoker(c)) return false;

        var groups = GroupByRank(cards);
        if (groups.Count != 3) return false;
        foreach (var kv in groups) if (kv.Value != 2) return false;

        var ranks = SortedRankKeys(groups);
        if ((int)ranks[1] - (int)ranks[0] != 1 ||
            (int)ranks[2] - (int)ranks[1] != 1) return false;

        desc = $"Consecutive Pairs {RankName(ranks[0], false)}–{RankName(ranks[2], false)}";
        return true;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool IsJoker(Card.CardId c) => IsJokerRank(c.rank);
    private static bool IsJokerRank(Card.Rank r) =>
        r == Card.Rank.BlackJoker || r == Card.Rank.RedJoker;

    private static bool IsBomb(SetType t) => t >= SetType.Bomb4;

    private static SetType NBombType(int n) => n switch
    {
        4 => SetType.Bomb4,
        5 => SetType.Bomb5,
        6 => SetType.Bomb6,
        7 => SetType.Bomb7,
        8 => SetType.Bomb8,
        _ => throw new ArgumentOutOfRangeException(nameof(n))
    };

    /// <summary>
    /// Returns cards sorted by natural rank value (Two=2 … Ace=14, jokers 15–16).
    /// </summary>
    private static List<Card.CardId> SortedByRank(IReadOnlyList<Card.CardId> cards)
    {
        var list = new List<Card.CardId>(cards);
        list.Sort((a, b) => (int)a.rank - (int)b.rank);
        return list;
    }

    /// <summary>
    /// True when the sorted list has no duplicate ranks and each step is exactly +1.
    /// </summary>
    private static bool AreStrictlyConsecutive(List<Card.CardId> sorted)
    {
        for (int i = 1; i < sorted.Count; i++)
            if ((int)sorted[i].rank - (int)sorted[i - 1].rank != 1)
                return false;
        return true;
    }

    private static Dictionary<Card.Rank, int> GroupByRank(IReadOnlyList<Card.CardId> cards)
    {
        var d = new Dictionary<Card.Rank, int>();
        foreach (var c in cards)
        {
            d.TryGetValue(c.rank, out int cnt);
            d[c.rank] = cnt + 1;
        }
        return d;
    }

    private static List<Card.Rank> SortedRankKeys(Dictionary<Card.Rank, int> groups)
    {
        var ranks = new List<Card.Rank>(groups.Keys);
        ranks.Sort((a, b) => (int)a - (int)b);
        return ranks;
    }

    private static string RankName(Card.Rank rank, bool plural)
    {
        string name = rank switch
        {
            Card.Rank.Two      => "2",
            Card.Rank.Three    => "3",
            Card.Rank.Four     => "4",
            Card.Rank.Five     => "5",
            Card.Rank.Six      => "6",
            Card.Rank.Seven    => "7",
            Card.Rank.Eight    => "8",
            Card.Rank.Nine     => "9",
            Card.Rank.Ten      => "10",
            Card.Rank.Jack     => "Jack",
            Card.Rank.Queen    => "Queen",
            Card.Rank.King     => "King",
            Card.Rank.Ace      => "Ace",
            Card.Rank.BlackJoker => "Black Joker",
            Card.Rank.RedJoker   => "Red Joker",
            _                  => rank.ToString()
        };
        return plural ? name + "s" : name;
    }

    private static string FriendlyTypeName(SetType type) => type switch
    {
        SetType.Single                 => "Single",
        SetType.Pair                   => "Pair",
        SetType.Triple                 => "Triple",
        SetType.FullHouse              => "Full House",
        SetType.Straight               => "Straight",
        SetType.ConsecutiveTriplePairs => "Consecutive Triples",
        SetType.TripleConsecutivePairs => "Consecutive Pairs",
        SetType.Bomb4                  => "4-Bomb",
        SetType.Bomb5                  => "5-Bomb",
        SetType.Bomb6                  => "6-Bomb",
        SetType.Bomb7                  => "7-Bomb",
        SetType.Bomb8                  => "8-Bomb",
        SetType.StraightFlush          => "Straight Flush",
        SetType.JokerBomb              => "Joker Bomb",
        _                              => type.ToString()
    };

    private static ValidationResult Identified(SetType type, string desc, IReadOnlyList<Card.CardId> cards) =>
        new ValidationResult { IsValid = true, Type = type, Description = desc, KeyRank = GetKeyRank(type, cards) };

    /// <summary>Returns the rank used to compare two sets of the same type.</summary>
    private static Card.Rank GetKeyRank(SetType type, IReadOnlyList<Card.CardId> cards)
    {
        switch (type)
        {
            case SetType.FullHouse:
                foreach (var kv in GroupByRank(cards))
                    if (kv.Value == 3) return kv.Key;
                return default;

            case SetType.Straight:
            case SetType.StraightFlush:
            case SetType.ConsecutiveTriplePairs:
            case SetType.TripleConsecutivePairs:
                return SortedByRank(cards)[0].rank;

            case SetType.JokerBomb:
                return Card.Rank.RedJoker; // irrelevant — JokerBomb always wins

            default:
                // Single, Pair, Triple, NBombs: all cards share one rank
                return cards[0].rank;
        }
    }

    /// <summary>
    /// Ordering of bomb types, weakest (1) to strongest (7).
    /// StraightFlush sits between Bomb5 and Bomb6 per game rules.
    /// </summary>
    private static int BombStrength(SetType type) => type switch
    {
        SetType.Bomb4         => 1,
        SetType.Bomb5         => 2,
        SetType.StraightFlush => 3,
        SetType.Bomb6         => 4,
        SetType.Bomb7         => 5,
        SetType.Bomb8         => 6,
        SetType.JokerBomb     => 7,
        _                     => 0
    };

    /// <summary>
    /// Integer rank for comparison. Uses 2× scale so trump (= Ace + 1 step) fits between
    /// Ace (28) and Black Joker (30) without floating-point.
    /// Scale: Two=4 … Ace=28, Trump=29, BlackJoker=30, RedJoker=32.
    /// </summary>
    /// <summary>
    /// Rank value used for set comparison. Straights and StraightFlushes compare by natural
    /// starting rank — trump has no elevating effect. All other sets use EffectiveRank.
    /// </summary>
    private static int KeyRankValue(SetType type, Card.Rank rank, Card.Rank trumpRank)
    {
        if (type == SetType.Straight || type == SetType.StraightFlush)
            return (int)rank;
        return EffectiveRank(rank, trumpRank);
    }

    private static int EffectiveRank(Card.Rank rank, Card.Rank trumpRank)
    {
        if (rank == trumpRank) return (int)Card.Rank.Ace * 2 + 1; // 29
        return (int)rank * 2;
    }

    private static ValidationResult Fail(FailCode code, string reason = null) =>
        new ValidationResult { IsValid = false, Code = code, FailReason = reason };
}
