using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Null or invalid arguments return false rather than throwing.
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
        int wildcardSlots = ExtractWildcards(cards, trumpRank, out var naturalCards);
        if (wildcardSlots > 2) return null;

        string desc;
        Card.Rank keyRank;

        // Single: always valid; lone wildcard plays as its natural trump-rank card.
        if (n == 1)
            return Identified(SetType.Single, SingleDesc(cards[0]), cards[0].rank);

        // --- Bombs ---
        if (TryJokerBomb(naturalCards, wildcardSlots, out desc))
            return Identified(SetType.JokerBomb, desc, Card.Rank.RedJoker);

        if (TryStraightFlush(naturalCards, n, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.StraightFlush, desc, keyRank);

        if (TryNBomb(naturalCards, n, wildcardSlots, out desc))
            return Identified(NBombType(n), desc, naturalCards[0].rank);

        // --- Regular sets ---
        if (TryPair(naturalCards, n, wildcardSlots, trumpRank, out desc, out keyRank))
            return Identified(SetType.Pair, desc, keyRank);

        if (TryTriple(naturalCards, n, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.Triple, desc, keyRank);

        if (TryFullHouse(naturalCards, n, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.FullHouse, desc, keyRank);

        if (TryStraight(naturalCards, n, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.Straight, desc, keyRank);

        if (TryConsecutiveTriplePairs(naturalCards, n, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.ConsecutiveTriplePairs, desc, keyRank);

        if (TryTripleConsecutivePairs(naturalCards, n, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.TripleConsecutivePairs, desc, keyRank);

        return null;
    }

    // -------------------------------------------------------------------------
    // Wildcard extraction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Separates trump-of-hearts wildcards from the rest.
    /// Returns the wildcard count; naturalCards receives all non-wildcard cards.
    /// </summary>
    private static int ExtractWildcards(
        IReadOnlyList<Card.CardId> cards,
        Card.Rank trumpRank,
        out List<Card.CardId> naturalCards)
    {
        naturalCards = new List<Card.CardId>(cards.Count);
        int wildcardSlots = 0;
        foreach (var c in cards)
        {
            if (c.rank == trumpRank && c.suit == Card.Suit.Hearts)
                wildcardSlots++;
            else
                naturalCards.Add(c);
        }
        return wildcardSlots;
    }

    // -------------------------------------------------------------------------
    // Bomb detectors
    // -------------------------------------------------------------------------

    /// <summary>Joker Bomb: exactly 2 Black Jokers + 2 Red Jokers. No wildcards allowed.</summary>
    private static bool TryJokerBomb(IReadOnlyList<Card.CardId> naturalCards, int wildcardSlots, out string desc)
    {
        desc = null;
        if (wildcardSlots > 0) return false;
        if (naturalCards.Count != 4) return false;
        if (naturalCards.Any(c => c.rank != Card.Rank.BlackJoker && c.rank != Card.Rank.RedJoker)) return false;
        int black = naturalCards.Count(c => c.rank == Card.Rank.BlackJoker);
        int red   = naturalCards.Count(c => c.rank == Card.Rank.RedJoker);
        if (black != 2 || red != 2) return false;
        desc = "Joker Bomb";
        return true;
    }

    /// <summary>N-Bomb: N cards of the same non-joker rank. Wildcards fill missing slots.</summary>
    private static bool TryNBomb(IReadOnlyList<Card.CardId> naturalCards, int n, int wildcardSlots, out string desc)
    {
        desc = null;
        if (n < 4 || n > 8) return false;
        if (!AllSameNonJokerRank(naturalCards, out var rank)) return false;
        desc = $"{n}-Bomb of {RankName(rank, plural: true)}{WildcardSuffix(wildcardSlots)}";
        return true;
    }

    /// <summary>
    /// Straight Flush: 5+ consecutive cards all of the same suit.
    /// Wildcards fill missing ranks; the wildcard physically adopts the flush suit.
    /// </summary>
    private static bool TryStraightFlush(
        IReadOnlyList<Card.CardId> naturalCards, int n, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (n < 5) return false;
        if (naturalCards.Any(IsJoker)) return false;

        var suit = naturalCards[0].suit;
        if (naturalCards.Any(c => c.suit != suit)) return false;

        var sorted = SortedByRank(naturalCards);
        if (!TryConsecutiveSequence(sorted, n, wildcardSlots, out keyRank, out var highRank)) return false;

        desc = $"Straight Flush {RankName(keyRank, false)}–{RankName(highRank, false)} ({suit}){WildcardSuffix(wildcardSlots)}";
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

    /// <summary>
    /// Pair: two cards of the same rank.
    /// Joker pairs must be same color and cannot use wildcards.
    /// Two wildcards form a pair of the trump rank.
    /// </summary>
    private static bool TryPair(
        IReadOnlyList<Card.CardId> naturalCards, int n, int wildcardSlots, Card.Rank trumpRank,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (n != 2) return false;

        // Both wildcards: pair of trump rank (both cards are physically trump-of-hearts).
        if (naturalCards.Count == 0)
        {
            desc = $"Pair of {RankName(trumpRank, plural: true)}";
            keyRank = trumpRank;
            return true;
        }

        var first = naturalCards[0];

        // Joker pair: must be complete, no wildcard mixing.
        if (IsJoker(first))
        {
            if (wildcardSlots > 0 || naturalCards.Count != 2 || naturalCards[1].rank != first.rank) return false;
            desc = first.rank == Card.Rank.BlackJoker ? "Pair of Black Jokers" : "Pair of Red Jokers";
            keyRank = first.rank;
            return true;
        }

        // Non-joker: all natural cards must share the same rank.
        if (!AllSameNonJokerRank(naturalCards, out keyRank)) return false;
        desc = $"Pair of {RankName(keyRank, plural: true)}{WildcardSuffix(wildcardSlots)}";
        return true;
    }

    /// <summary>Triple: three cards of the same non-joker rank. Wildcards fill missing slots.</summary>
    private static bool TryTriple(
        IReadOnlyList<Card.CardId> naturalCards, int n, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (n != 3) return false;
        if (!AllSameNonJokerRank(naturalCards, out keyRank)) return false;
        desc = $"Triple {RankName(keyRank, plural: true)}{WildcardSuffix(wildcardSlots)}";
        return true;
    }

    /// <summary>
    /// Full House: one triple + one pair.
    /// The triple must be non-joker. The pair may be jokers if already complete.
    /// Wildcards fill non-joker shortfall only.
    /// </summary>
    private static bool TryFullHouse(
        IReadOnlyList<Card.CardId> naturalCards, int n, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (n != 5) return false;

        var groups = GroupByRank(naturalCards);
        if (groups.Count == 0 || groups.Count > 2) return false;

        var ranks = SortedRankKeys(groups);

        if (groups.Count == 2)
        {
            // Both orderings tried — TryFHAssignment rejects joker triples internally.
            Card.Rank tr, pr;
            if (!TryFHAssignment(groups, ranks[1], ranks[0], wildcardSlots, out tr, out pr) &&
                !TryFHAssignment(groups, ranks[0], ranks[1], wildcardSlots, out tr, out pr))
                return false;
            desc = $"Full House: {RankName(tr, plural: true)} over {RankName(pr, plural: true)}{WildcardSuffix(wildcardSlots)}";
            keyRank = tr;
            return true;
        }
        else // groups.Count == 1: wildcards fill the pair
        {
            if (wildcardSlots != 2) return false;
            if (!TryTriple(naturalCards, 3, 0, out _, out keyRank)) return false;
            desc = $"Full House: {RankName(keyRank, plural: true)} over wildcard pair (wildcard)";
            return true;
        }
    }

    /// <summary>
    /// Attempts to assign tripleRank and pairRank roles.
    /// Triple must be non-joker. Joker pair must already be complete.
    /// Non-joker shortfall must not exceed wildcardSlots.
    /// </summary>
    private static bool TryFHAssignment(
        Dictionary<Card.Rank, int> groups,
        Card.Rank tripleRank, Card.Rank pairRank, int wildcardSlots,
        out Card.Rank outTriple, out Card.Rank outPair)
    {
        outTriple = default; outPair = default;
        if (IsJokerRank(tripleRank)) return false;

        int tripleCount = groups.TryGetValue(tripleRank, out int tc) ? tc : 0;
        int pairCount   = groups.TryGetValue(pairRank,   out int pc) ? pc : 0;

        int tripleShortfall = Math.Max(0, 3 - tripleCount);
        int pairShortfall   = Math.Max(0, 2 - pairCount);

        if (IsJokerRank(pairRank) && pairShortfall > 0) return false;

        int nonJokerShortfall = tripleShortfall + (IsJokerRank(pairRank) ? 0 : pairShortfall);
        if (nonJokerShortfall > wildcardSlots) return false;

        outTriple = tripleRank;
        outPair   = pairRank;
        return true;
    }

    /// <summary>
    /// Straight: 5+ cards with strictly consecutive, distinct ranks — no jokers, no duplicates.
    /// Does not wrap (A–2 is invalid). Wildcards fill gaps or extend at ends.
    /// </summary>
    private static bool TryStraight(
        IReadOnlyList<Card.CardId> naturalCards, int n, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (n < 5) return false;
        if (naturalCards.Any(IsJoker)) return false;

        var sorted = SortedByRank(naturalCards);
        if (!TryConsecutiveSequence(sorted, n, wildcardSlots, out keyRank, out var highRank)) return false;

        desc = $"Straight {RankName(keyRank, false)}–{RankName(highRank, false)}{WildcardSuffix(wildcardSlots)}";
        return true;
    }

    /// <summary>
    /// Consecutive Triple Pairs: exactly two triples of consecutive rank (e.g. 444 555).
    /// No jokers. Trump rank is its natural value. Wildcards fill missing cards.
    /// Requires exactly 2 distinct consecutive ranks in naturalCards.
    /// </summary>
    private static bool TryConsecutiveTriplePairs(
        IReadOnlyList<Card.CardId> naturalCards, int n, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (!TryConsecutiveGroups(naturalCards, n, wildcardSlots, 2, out var ranks)) return false;
        keyRank = ranks[0];
        desc = $"Consecutive Triples {RankName(ranks[0], false)}–{RankName(ranks[1], false)}{WildcardSuffix(wildcardSlots)}";
        return true;
    }

    private static bool TryTripleConsecutivePairs(
        IReadOnlyList<Card.CardId> naturalCards, int n, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (!TryConsecutiveGroups(naturalCards, n, wildcardSlots, 3, out var ranks)) return false;
        keyRank = ranks[0];
        desc = $"Consecutive Pairs {RankName(ranks[0], false)}–{RankName(ranks[2], false)}{WildcardSuffix(wildcardSlots)}";
        return true;
    }

    // -------------------------------------------------------------------------
    // Shared sequence helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Core straight/flush validation using the gap-sum heuristic.
    /// gaps = sum of (rank[i+1] - rank[i] - 1) across adjacent sorted natural cards.
    /// Valid when gaps &lt;= wildcardSlots. Remaining wildcards extend at ends.
    /// Prefers extending above (stronger straight); falls back to extending below.
    /// </summary>
    private static bool TryConsecutiveSequence(
        List<Card.CardId> sorted, int n, int wildcardSlots,
        out Card.Rank keyRank, out Card.Rank highRank)
    {
        keyRank = default; highRank = default;

        // No duplicate ranks.
        for (int i = 1; i < sorted.Count; i++)
            if (sorted[i].rank == sorted[i - 1].rank) return false;

        // Sum interior gaps.
        int gaps = 0;
        for (int i = 1; i < sorted.Count; i++)
            gaps += (int)sorted[i].rank - (int)sorted[i - 1].rank - 1;

        if (gaps > wildcardSlots) return false;

        // Remaining wildcards extend at ends — prefer above for a stronger straight.
        int leftover    = wildcardSlots - gaps;
        int canGoAbove  = (int)Card.Rank.Ace - (int)sorted[sorted.Count - 1].rank;
        int goAbove     = Math.Min(leftover, canGoAbove);
        int goBelow     = leftover - goAbove;

        if ((int)sorted[0].rank - goBelow < (int)Card.Rank.Two) return false;

        keyRank  = (Card.Rank)((int)sorted[0].rank - goBelow);
        highRank = (Card.Rank)((int)sorted[sorted.Count - 1].rank + goAbove);
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

    private static ValidationResult Identified(SetType type, string desc, Card.Rank keyRank) =>
        new ValidationResult { IsValid = true, Type = type, Description = desc, KeyRank = keyRank };

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

    /// <summary>True when every card in <paramref name="naturalCards"/> shares the same non-joker rank.</summary>
    private static bool AllSameNonJokerRank(IReadOnlyList<Card.CardId> naturalCards, out Card.Rank rank)
    {
        rank = default;
        if (naturalCards.Count == 0) return false;
        var first = naturalCards[0];
        if (naturalCards.Any(c => c.rank != first.rank || IsJoker(c))) return false;
        rank = first.rank;
        return true;
    }

    private static string WildcardSuffix(int wildcardSlots) => wildcardSlots > 0 ? " (wildcard)" : "";

    /// <summary>
    /// True when naturalCards form exactly <paramref name="groupCount"/> distinct consecutive non-joker ranks,
    /// each group within its target size (6 / groupCount), with total shortfall ≤ wildcardSlots.
    /// </summary>
    private static bool TryConsecutiveGroups(
        IReadOnlyList<Card.CardId> naturalCards, int n, int wildcardSlots, int groupCount,
        out List<Card.Rank> ranks)
    {
        ranks = null;
        if (n != 6) return false;
        if (naturalCards.Any(IsJoker)) return false;
        var groups = GroupByRank(naturalCards);
        if (groups.Count != groupCount) return false;
        var r = SortedRankKeys(groups);
        for (int i = 1; i < r.Count; i++)
            if ((int)r[i] - (int)r[i - 1] != 1) return false;
        int target = 6 / groupCount;
        int shortfall = 0;
        foreach (var rank in r)
        {
            if (groups[rank] > target) return false;
            shortfall += target - groups[rank];
        }
        if (shortfall > wildcardSlots) return false;
        ranks = r;
        return true;
    }
}
