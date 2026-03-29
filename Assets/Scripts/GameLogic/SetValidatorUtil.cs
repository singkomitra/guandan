using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure utility helpers for SetValidator. No game-logic decisions live here —
/// only rank/type queries, collection utilities, and description builders.
/// </summary>
public static partial class SetValidator
{
    // -------------------------------------------------------------------------
    // Type / rank queries
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

    // -------------------------------------------------------------------------
    // Collection helpers
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Result factories
    // -------------------------------------------------------------------------

    private static ValidationResult Identified(SetType type, string desc, Card.Rank keyRank) =>
        new ValidationResult { IsValid = true, Type = type, Description = desc, KeyRank = keyRank };

    private static ValidationResult Fail(FailCode code, string reason = null) =>
        new ValidationResult { IsValid = false, Code = code, FailReason = reason };

    // -------------------------------------------------------------------------
    // Description builders
    // -------------------------------------------------------------------------

    private static string SingleDesc(Card.CardId c)
    {
        if (c.rank == Card.Rank.BlackJoker) return "Black Joker";
        if (c.rank == Card.Rank.RedJoker)   return "Red Joker";
        return $"{RankName(c.rank, false)} of {c.suit}";
    }

    // Rank-uniform sets: Pair, Triple, N-Bombs
    private static string RankSetDesc(SetType type, Card.Rank rank, int wildcardSlots) => type switch
    {
        SetType.Triple => $"Triple {RankName(rank, plural: true)}{WildcardSuffix(wildcardSlots)}",
        _              => $"{FriendlyTypeName(type)} of {RankName(rank, plural: true)}{WildcardSuffix(wildcardSlots)}"
    };

    // Straight, ConsecutiveTriplePairs, TripleConsecutivePairs
    private static string SequenceSetDesc(SetType type, Card.Rank low, Card.Rank high, int wildcardSlots)
        => $"{FriendlyTypeName(type)} {RankName(low, false)}–{RankName(high, false)}{WildcardSuffix(wildcardSlots)}";

    private static string StraightFlushDesc(Card.Rank low, Card.Rank high, Card.Suit suit, int wildcardSlots)
        => $"Straight Flush {RankName(low, false)}–{RankName(high, false)} ({suit}){WildcardSuffix(wildcardSlots)}";

    private static string FullHouseDesc(Card.Rank triple, Card.Rank pair, int wildcardSlots)
        => $"Full House: {RankName(triple, plural: true)} over {RankName(pair, plural: true)}{WildcardSuffix(wildcardSlots)}";

    private static string WildcardSuffix(int wildcardSlots) => wildcardSlots > 0 ? " (wildcard)" : "";

    private static string RankName(Card.Rank rank, bool plural)
    {
        string name = rank switch
        {
            Card.Rank.Two        => "2",
            Card.Rank.Three      => "3",
            Card.Rank.Four       => "4",
            Card.Rank.Five       => "5",
            Card.Rank.Six        => "6",
            Card.Rank.Seven      => "7",
            Card.Rank.Eight      => "8",
            Card.Rank.Nine       => "9",
            Card.Rank.Ten        => "10",
            Card.Rank.Jack       => "Jack",
            Card.Rank.Queen      => "Queen",
            Card.Rank.King       => "King",
            Card.Rank.Ace        => "Ace",
            Card.Rank.BlackJoker => "Black Joker",
            Card.Rank.RedJoker   => "Red Joker",
            _                    => rank.ToString()
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
}
