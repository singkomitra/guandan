using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Pure logic class — zero Unity dependencies except Debug logging.
/// Single source of truth for all Guandan set and bomb rules.
/// Unit-testable with plain NUnit, no scene required.
/// Utility helpers live in SetValidatorUtil.cs (same partial class).
/// </summary>
public static partial class SetValidator
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

    /// <summary>Maximum number of trump-of-hearts wildcards allowed in a single set.</summary>
    public const int MaxWildcardsPerSet = 2;

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
        if (wildcardSlots > MaxWildcardsPerSet) return null;

        string desc;
        Card.Rank keyRank;

        // Single: always valid; lone wildcard plays as its natural trump-rank card.
        if (n == 1)
            return Identified(SetType.Single, SingleDesc(cards[0]), cards[0].rank);

        // All wildcards: type is fully determined by count.
        // n=5 is always the strongest possible straight flush; otherwise it's a rank-uniform set.
        if (naturalCards.Count == 0)
        {
            return n switch
            {
                2 => Identified(SetType.Pair,          RankSetDesc(SetType.Pair,     trumpRank, wildcardSlots), trumpRank),
                3 => Identified(SetType.Triple,        RankSetDesc(SetType.Triple,   trumpRank, wildcardSlots), trumpRank),
                5 => Identified(SetType.StraightFlush, StraightFlushDesc(Card.Rank.Ten, Card.Rank.Ace, Card.Suit.Hearts, wildcardSlots), Card.Rank.Ten),
                _ when n >= 4 && n <= 8 => Identified(NBombType(n), RankSetDesc(NBombType(n), trumpRank, wildcardSlots), trumpRank),
                _ => null
            };
        }

        // --- Bombs ---
        if (TryJokerBomb(naturalCards, wildcardSlots, out desc))
            return Identified(SetType.JokerBomb, desc, Card.Rank.RedJoker);

        if (TryStraightFlush(naturalCards, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.StraightFlush, desc, keyRank);

        if (TryNBomb(naturalCards, wildcardSlots, out desc))
            return Identified(NBombType(n), desc, naturalCards[0].rank);

        // --- Regular sets ---
        if (TryPair(naturalCards, wildcardSlots, trumpRank, out desc, out keyRank))
            return Identified(SetType.Pair, desc, keyRank);

        if (TryTriple(naturalCards, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.Triple, desc, keyRank);

        if (TryFullHouse(naturalCards, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.FullHouse, desc, keyRank);

        if (TryStraight(naturalCards, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.Straight, desc, keyRank);

        if (TryConsecutiveTriplePairs(naturalCards, wildcardSlots, out desc, out keyRank))
            return Identified(SetType.ConsecutiveTriplePairs, desc, keyRank);

        if (TryTripleConsecutivePairs(naturalCards, wildcardSlots, out desc, out keyRank))
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
    private static bool TryNBomb(IReadOnlyList<Card.CardId> naturalCards, int wildcardSlots, out string desc)
    {
        desc = null;
        int n = naturalCards.Count + wildcardSlots;
        if (n < 4 || n > 8) return false;
        if (!AllSameNonJokerRank(naturalCards, out var rank)) return false;
        desc = RankSetDesc(NBombType(n), rank, wildcardSlots);
        return true;
    }

    /// <summary>
    /// Straight Flush: exactly 5 consecutive cards all of the same suit.
    /// Wildcards fill missing ranks; the wildcard physically adopts the flush suit.
    /// </summary>
    private static bool TryStraightFlush(
        IReadOnlyList<Card.CardId> naturalCards, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        int n = naturalCards.Count + wildcardSlots;
        if (n != 5) return false;
        if (naturalCards.Any(IsJoker)) return false;

        var suit = naturalCards[0].suit;
        if (naturalCards.Any(c => c.suit != suit)) return false;

        var sorted = SortedByRank(naturalCards);
        if (!TryConsecutiveSequence(sorted, wildcardSlots, out keyRank, out var highRank)) return false;

        desc = StraightFlushDesc(keyRank, highRank, suit, wildcardSlots);
        return true;
    }

    // -------------------------------------------------------------------------
    // Regular set detectors
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pair: two cards of the same rank.
    /// Joker pairs must be same color and cannot use wildcards.
    /// </summary>
    private static bool TryPair(
        IReadOnlyList<Card.CardId> naturalCards, int wildcardSlots, Card.Rank trumpRank,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (naturalCards.Count + wildcardSlots != 2) return false;

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
        desc = RankSetDesc(SetType.Pair, keyRank, wildcardSlots);
        return true;
    }

    /// <summary>Triple: three cards of the same non-joker rank. Wildcards fill missing slots.</summary>
    private static bool TryTriple(
        IReadOnlyList<Card.CardId> naturalCards, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (naturalCards.Count + wildcardSlots != 3) return false;
        if (!AllSameNonJokerRank(naturalCards, out keyRank)) return false;
        desc = RankSetDesc(SetType.Triple, keyRank, wildcardSlots);
        return true;
    }

    /// <summary>
    /// Full House: one triple + one pair.
    /// The triple must be non-joker. The pair may be jokers if already complete.
    /// Wildcards fill non-joker shortfall only.
    /// </summary>
    private static bool TryFullHouse(
        IReadOnlyList<Card.CardId> naturalCards, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (naturalCards.Count + wildcardSlots != 5) return false;

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
            desc = FullHouseDesc(tr, pr, wildcardSlots);
            keyRank = tr;
            return true;
        }
        else // groups.Count == 1: all naturals share one rank; wildcards fill the pair
        {
            if (wildcardSlots != MaxWildcardsPerSet) return false;
            if (!TryTriple(naturalCards, 0, out _, out keyRank)) return false;
            desc = FullHouseWildcardPairDesc(keyRank, wildcardSlots);
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
    /// Straight: exactly 5 consecutive, distinct ranks — no jokers, no duplicates.
    /// Does not wrap (A–2 is invalid). Wildcards fill gaps or extend at ends.
    /// </summary>
    private static bool TryStraight(
        IReadOnlyList<Card.CardId> naturalCards, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        int n = naturalCards.Count + wildcardSlots;
        if (n != 5) return false;
        if (naturalCards.Any(IsJoker)) return false;

        var sorted = SortedByRank(naturalCards);
        if (!TryConsecutiveSequence(sorted, wildcardSlots, out keyRank, out var highRank)) return false;

        desc = SequenceSetDesc(SetType.Straight, keyRank, highRank, wildcardSlots);
        return true;
    }

    /// <summary>
    /// Consecutive Triple Pairs: exactly two triples of consecutive rank (e.g. 444 555).
    /// No jokers. Trump rank is its natural value. Wildcards fill missing cards.
    /// Requires exactly 2 distinct consecutive ranks in naturalCards.
    /// </summary>
    private static bool TryConsecutiveTriplePairs(
        IReadOnlyList<Card.CardId> naturalCards, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (!TryConsecutiveGroups(naturalCards, wildcardSlots, 2, 3, out var ranks)) return false;
        keyRank = ranks[0];
        desc = SequenceSetDesc(SetType.ConsecutiveTriplePairs, ranks[0], ranks[1], wildcardSlots);
        return true;
    }

    private static bool TryTripleConsecutivePairs(
        IReadOnlyList<Card.CardId> naturalCards, int wildcardSlots,
        out string desc, out Card.Rank keyRank)
    {
        desc = null; keyRank = default;
        if (!TryConsecutiveGroups(naturalCards, wildcardSlots, 3, 2, out var ranks)) return false;
        keyRank = ranks[0];
        desc = SequenceSetDesc(SetType.TripleConsecutivePairs, ranks[0], ranks[2], wildcardSlots);
        return true;
    }

    // -------------------------------------------------------------------------
    // Shared sequence helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Core straight/flush validation using the gap-sum heuristic.
    /// gaps = sum of (rank[i+1] - rank[i] - 1) across adjacent sorted natural cards.
    /// Valid when gaps &lt;= wildcardSlots. Remaining wildcards extend at ends.
    /// Prefers extending above (stronger straight); falls back to extending below.
    /// </summary>
    private static bool TryConsecutiveSequence(
        List<Card.CardId> sorted, int wildcardSlots,
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

    /// <summary>
    /// True when naturalCards form exactly <paramref name="groupCount"/> distinct consecutive non-joker ranks,
    /// each group of exactly <paramref name="groupSize"/> cards, with total shortfall ≤ wildcardSlots.
    /// </summary>
    private static bool TryConsecutiveGroups(
        IReadOnlyList<Card.CardId> naturalCards, int wildcardSlots, int groupCount, int groupSize,
        out List<Card.Rank> ranks)
    {
        ranks = null;
        if (naturalCards.Count + wildcardSlots != groupCount * groupSize) return false;
        if (naturalCards.Any(IsJoker)) return false;
        var groups = GroupByRank(naturalCards);
        if (groups.Count != groupCount) return false;
        var r = SortedRankKeys(groups);
        for (int i = 1; i < r.Count; i++)
            if ((int)r[i] - (int)r[i - 1] != 1) return false;
        int shortfall = 0;
        foreach (var rank in r)
        {
            if (groups[rank] > groupSize) return false;
            shortfall += groupSize - groups[rank];
        }
        if (shortfall > wildcardSlots) return false;
        ranks = r;
        return true;
    }
}
