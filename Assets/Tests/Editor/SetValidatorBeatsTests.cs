using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for SetValidator.Beats() and its integration into Validate() via GameContext.MustBeat.
/// Pure logic — no scene required.
/// </summary>
public class SetValidatorBeatsTests
{
    // Shorthand helpers
    static Card.CardId C(Card.Rank rank, Card.Suit suit = Card.Suit.Spades) =>
        new Card.CardId { rank = rank, suit = suit };

    static SetValidator.ValidationResult V(params Card.CardId[] cards) =>
        SetValidator.Validate(cards, null);

    /// <summary>
    /// Logs the comparison inputs and result, then asserts. Output appears in the test runner.
    /// </summary>
    static void AssertBeats(
        bool expected,
        SetValidator.ValidationResult challenger,
        SetValidator.ValidationResult tableSet,
        Card.Rank trumpRank = Card.Rank.Two)
    {
        bool actual = SetValidator.Beats(challenger, tableSet, trumpRank);
        Debug.Log($"Beats({challenger.Description}, table: {tableSet.Description}, trump={trumpRank}) " +
                  $"= {actual}  (expected {expected})");
        Assert.AreEqual(expected, actual);
    }

    // -------------------------------------------------------------------------
    // Same-type non-bomb comparisons
    // -------------------------------------------------------------------------

    [Test]
    public void Pair_HigherRankBeatsLower()
    {
        Debug.Log("Pair of Kings should beat Pair of Queens — higher rank wins within same type");
        var kings  = V(C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts));
        var queens = V(C(Card.Rank.Queen), C(Card.Rank.Queen, Card.Suit.Hearts));
        AssertBeats(true, kings, queens);
    }

    [Test]
    public void Pair_LowerRankDoesNotBeatHigher()
    {
        Debug.Log("Pair of Queens should NOT beat Pair of Kings");
        var queens = V(C(Card.Rank.Queen), C(Card.Rank.Queen, Card.Suit.Hearts));
        var kings  = V(C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts));
        AssertBeats(false, queens, kings);
    }

    [Test]
    public void Pair_EqualRankDoesNotBeat()
    {
        Debug.Log("Pair of Kings should NOT beat another Pair of Kings — equal rank is not sufficient");
        var kingsA = V(C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts));
        var kingsB = V(C(Card.Rank.King, Card.Suit.Diamonds), C(Card.Rank.King, Card.Suit.Clubs));
        AssertBeats(false, kingsA, kingsB);
    }

    [Test]
    public void Single_TrumpRankBeatsAce()
    {
        Debug.Log("Single Queen (trump=Queen) should beat single Ace — trump rank sits above Ace");
        var trumpQueen = V(C(Card.Rank.Queen));
        var ace        = V(C(Card.Rank.Ace));
        AssertBeats(true, trumpQueen, ace, Card.Rank.Queen);
    }

    [Test]
    public void Single_AceDoesNotBeatTrumpLowerCard()
    {
        Debug.Log("Single Ace should NOT beat single Two when trump=Two — trump overrides natural rank");
        var ace      = V(C(Card.Rank.Ace));
        var trumpTwo = V(C(Card.Rank.Two));
        AssertBeats(false, ace, trumpTwo, Card.Rank.Two);
    }

    [Test]
    public void Straight_HigherStartBeatsLower()
    {
        Debug.Log("Straight 4-8 should beat Straight 3-7 — higher starting rank wins");
        var high = V(C(Card.Rank.Four), C(Card.Rank.Five, Card.Suit.Hearts), C(Card.Rank.Six, Card.Suit.Diamonds), C(Card.Rank.Seven, Card.Suit.Clubs), C(Card.Rank.Eight));
        var low  = V(C(Card.Rank.Three), C(Card.Rank.Four, Card.Suit.Hearts), C(Card.Rank.Five, Card.Suit.Diamonds), C(Card.Rank.Six, Card.Suit.Clubs), C(Card.Rank.Seven));
        AssertBeats(true, high, low);
    }

    [Test]
    public void Straight_TrumpStartDoesNotBeatHigherStart()
    {
        Debug.Log("Straight 3-7 (trump=Three) should NOT beat Straight 4-8 — trump does not boost starting rank in straights");
        var trumpStart  = V(C(Card.Rank.Three), C(Card.Rank.Four, Card.Suit.Hearts), C(Card.Rank.Five, Card.Suit.Diamonds), C(Card.Rank.Six, Card.Suit.Clubs), C(Card.Rank.Seven));
        var higherStart = V(C(Card.Rank.Four), C(Card.Rank.Five, Card.Suit.Hearts), C(Card.Rank.Six, Card.Suit.Diamonds), C(Card.Rank.Seven, Card.Suit.Clubs), C(Card.Rank.Eight));
        AssertBeats(false, trumpStart, higherStart, Card.Rank.Three);
    }

    [Test]
    public void StraightFlush_TrumpStartDoesNotBeatHigherStart()
    {
        Debug.Log("Straight Flush 3-7 (trump=Three) should NOT beat Straight Flush 4-8 — trump does not boost starting rank in straight flushes");
        var trumpStart  = V(C(Card.Rank.Three, Card.Suit.Hearts), C(Card.Rank.Four, Card.Suit.Hearts), C(Card.Rank.Five, Card.Suit.Hearts), C(Card.Rank.Six, Card.Suit.Hearts), C(Card.Rank.Seven, Card.Suit.Hearts));
        var higherStart = V(C(Card.Rank.Four, Card.Suit.Spades), C(Card.Rank.Five, Card.Suit.Spades), C(Card.Rank.Six, Card.Suit.Spades), C(Card.Rank.Seven, Card.Suit.Spades), C(Card.Rank.Eight, Card.Suit.Spades));
        AssertBeats(false, trumpStart, higherStart, Card.Rank.Three);
    }

    [Test]
    public void FullHouse_TripleRankDecides()
    {
        Debug.Log("Full House 444+KK should beat Full House 333+AA — triple rank decides, pair rank is irrelevant");
        var fours = V(
            C(Card.Rank.Four), C(Card.Rank.Four, Card.Suit.Hearts), C(Card.Rank.Four, Card.Suit.Diamonds),
            C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts));
        var threes = V(
            C(Card.Rank.Three), C(Card.Rank.Three, Card.Suit.Hearts), C(Card.Rank.Three, Card.Suit.Diamonds),
            C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts));
        AssertBeats(true, fours, threes);
    }

    [Test]
    public void ConsecutiveTriplePairs_StartingRankDecides()
    {
        Debug.Log("Consecutive Triples 555+666 should beat 444+555 — starting triple rank decides");
        var high = V(
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts), C(Card.Rank.Five, Card.Suit.Diamonds),
            C(Card.Rank.Six),  C(Card.Rank.Six,  Card.Suit.Hearts), C(Card.Rank.Six,  Card.Suit.Diamonds));
        var low = V(
            C(Card.Rank.Four), C(Card.Rank.Four, Card.Suit.Hearts), C(Card.Rank.Four, Card.Suit.Diamonds),
            C(Card.Rank.Five, Card.Suit.Clubs), C(Card.Rank.Five, Card.Suit.Spades), C(Card.Rank.Five, Card.Suit.Hearts));
        AssertBeats(true, high, low);
    }

    [Test]
    public void TripleConsecutivePairs_StartingRankDecides()
    {
        Debug.Log("Consecutive Pairs 44+55+66 should beat 33+44+55 — starting pair rank decides");
        var high = V(
            C(Card.Rank.Four), C(Card.Rank.Four, Card.Suit.Hearts),
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts),
            C(Card.Rank.Six),  C(Card.Rank.Six,  Card.Suit.Hearts));
        var low = V(
            C(Card.Rank.Three), C(Card.Rank.Three, Card.Suit.Hearts),
            C(Card.Rank.Four,   Card.Suit.Diamonds), C(Card.Rank.Four, Card.Suit.Clubs),
            C(Card.Rank.Five,   Card.Suit.Diamonds), C(Card.Rank.Five, Card.Suit.Clubs));
        AssertBeats(true, high, low);
    }

    // -------------------------------------------------------------------------
    // Cross-type non-bombs never beat each other
    // -------------------------------------------------------------------------

    [Test]
    public void DifferentTypes_PairDoesNotBeatTriple()
    {
        Debug.Log("Pair of Aces should NOT beat Triple of 2s — once a set type is established, only the same type (or a bomb) is valid");
        var pair   = V(C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts));
        var triple = V(C(Card.Rank.Two), C(Card.Rank.Two, Card.Suit.Hearts), C(Card.Rank.Two, Card.Suit.Diamonds));
        AssertBeats(false, pair, triple);
    }

    // -------------------------------------------------------------------------
    // Bombs vs non-bombs
    // -------------------------------------------------------------------------

    [Test]
    public void Bomb_BeatsAnyNonBomb()
    {
        Debug.Log("4-Bomb of 3s should beat Pair of Aces — bombs bypass set type and always beat non-bombs");
        var bomb = V(
            C(Card.Rank.Three), C(Card.Rank.Three, Card.Suit.Hearts),
            C(Card.Rank.Three, Card.Suit.Diamonds), C(Card.Rank.Three, Card.Suit.Clubs));
        var pairOfAces = V(C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts));
        AssertBeats(true, bomb, pairOfAces);
    }

    [Test]
    public void NonBomb_DoesNotBeatBomb()
    {
        Debug.Log("Pair of Aces should NOT beat 4-Bomb of 2s — non-bombs can never beat a bomb");
        var pairOfAces = V(C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts));
        var bomb = V(
            C(Card.Rank.Two), C(Card.Rank.Two, Card.Suit.Hearts),
            C(Card.Rank.Two, Card.Suit.Diamonds), C(Card.Rank.Two, Card.Suit.Clubs));
        AssertBeats(false, pairOfAces, bomb);
    }

    // -------------------------------------------------------------------------
    // Bomb hierarchy
    // -------------------------------------------------------------------------

    [Test]
    public void Bomb_HigherTierWins()
    {
        Debug.Log("5-Bomb of 2s should beat 4-Bomb of Aces — more cards outranks higher rank");
        var fiveBomb = V(
            C(Card.Rank.Two), C(Card.Rank.Two, Card.Suit.Hearts), C(Card.Rank.Two, Card.Suit.Diamonds),
            C(Card.Rank.Two, Card.Suit.Clubs), C(Card.Rank.Two, Card.Suit.Spades));
        var fourBomb = V(
            C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts),
            C(Card.Rank.Ace, Card.Suit.Diamonds), C(Card.Rank.Ace, Card.Suit.Clubs));
        AssertBeats(true, fiveBomb, fourBomb);
    }

    [Test]
    public void StraightFlush_BeatsFiveBomb()
    {
        Debug.Log("Straight Flush 3-7 should beat 5-Bomb of Aces — Straight Flush ranks above 5-bomb");
        var sf = V(
            C(Card.Rank.Three, Card.Suit.Hearts), C(Card.Rank.Four, Card.Suit.Hearts),
            C(Card.Rank.Five,  Card.Suit.Hearts), C(Card.Rank.Six,  Card.Suit.Hearts),
            C(Card.Rank.Seven, Card.Suit.Hearts));
        var fiveBomb = V(
            C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts), C(Card.Rank.Ace, Card.Suit.Diamonds),
            C(Card.Rank.Ace, Card.Suit.Clubs), C(Card.Rank.Ace, Card.Suit.Spades));
        AssertBeats(true, sf, fiveBomb);
    }

    [Test]
    public void SixBomb_BeatsStraightFlush()
    {
        Debug.Log("6-Bomb of 2s should beat Straight Flush 9-K — 6-bomb ranks above Straight Flush");
        var sixBomb = V(
            C(Card.Rank.Two), C(Card.Rank.Two, Card.Suit.Hearts), C(Card.Rank.Two, Card.Suit.Diamonds),
            C(Card.Rank.Two, Card.Suit.Clubs), C(Card.Rank.Two, Card.Suit.Spades),
            C(Card.Rank.Two, Card.Suit.Hearts));
        var sf = V(
            C(Card.Rank.Nine,  Card.Suit.Spades), C(Card.Rank.Ten,   Card.Suit.Spades),
            C(Card.Rank.Jack,  Card.Suit.Spades), C(Card.Rank.Queen, Card.Suit.Spades),
            C(Card.Rank.King,  Card.Suit.Spades));
        AssertBeats(true, sixBomb, sf);
    }

    [Test]
    public void JokerBomb_BeatsEverything()
    {
        Debug.Log("Joker Bomb should beat 8-Bomb of Aces — Joker Bomb is the absolute highest");
        var jokerBomb = V(
            C(Card.Rank.BlackJoker), C(Card.Rank.BlackJoker, Card.Suit.Hearts),
            C(Card.Rank.RedJoker),   C(Card.Rank.RedJoker,   Card.Suit.Hearts));
        var eightBomb = V(
            C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts), C(Card.Rank.Ace, Card.Suit.Diamonds),
            C(Card.Rank.Ace, Card.Suit.Clubs),
            C(Card.Rank.Ace, Card.Suit.Spades), C(Card.Rank.Ace, Card.Suit.Hearts),
            C(Card.Rank.Ace, Card.Suit.Diamonds), C(Card.Rank.Ace, Card.Suit.Clubs));
        AssertBeats(true, jokerBomb, eightBomb);
    }

    [Test]
    public void SameBombType_HigherRankWins()
    {
        Debug.Log("5-Bomb of Aces should beat 5-Bomb of 2s (trump=Three) — same bomb size, higher rank wins");
        var fiveBombAces = V(
            C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts), C(Card.Rank.Ace, Card.Suit.Diamonds),
            C(Card.Rank.Ace, Card.Suit.Clubs), C(Card.Rank.Ace, Card.Suit.Spades));
        var fiveBombTwos = V(
            C(Card.Rank.Two), C(Card.Rank.Two, Card.Suit.Hearts), C(Card.Rank.Two, Card.Suit.Diamonds),
            C(Card.Rank.Two, Card.Suit.Clubs), C(Card.Rank.Two, Card.Suit.Spades));
        AssertBeats(true, fiveBombAces, fiveBombTwos, Card.Rank.Three);
    }

    // -------------------------------------------------------------------------
    // Joker hierarchy (singles and pairs)
    // -------------------------------------------------------------------------

    [Test]
    public void Single_RedJokerBeatsBlackJoker()
    {
        Debug.Log("Single Red Joker should beat single Black Joker — Red Joker is the highest card");
        var red   = V(C(Card.Rank.RedJoker));
        var black = V(C(Card.Rank.BlackJoker));
        AssertBeats(true, red, black);
    }

    [Test]
    public void Single_BlackJokerBeatsTrump()
    {
        Debug.Log("Single Black Joker should beat single trump Queen — jokers outrank trump");
        var black      = V(C(Card.Rank.BlackJoker));
        var trumpQueen = V(C(Card.Rank.Queen));
        AssertBeats(true, black, trumpQueen, Card.Rank.Queen);
    }

    [Test]
    public void Single_RedJokerBeatsTrump()
    {
        Debug.Log("Single Red Joker should beat single trump Queen — jokers outrank trump");
        var red        = V(C(Card.Rank.RedJoker));
        var trumpQueen = V(C(Card.Rank.Queen));
        AssertBeats(true, red, trumpQueen, Card.Rank.Queen);
    }

    [Test]
    public void Pair_RedJokersBeatsBlackJokers()
    {
        Debug.Log("Pair of Red Jokers should beat Pair of Black Jokers");
        var redPair   = V(C(Card.Rank.RedJoker), C(Card.Rank.RedJoker, Card.Suit.Hearts));
        var blackPair = V(C(Card.Rank.BlackJoker), C(Card.Rank.BlackJoker, Card.Suit.Hearts));
        AssertBeats(true, redPair, blackPair);
    }

    [Test]
    public void Pair_BlackJokersBeatsAces()
    {
        Debug.Log("Pair of Black Jokers should beat Pair of Aces — jokers outrank all regular cards");
        var blackPair = V(C(Card.Rank.BlackJoker), C(Card.Rank.BlackJoker, Card.Suit.Hearts));
        var aces      = V(C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts));
        AssertBeats(true, blackPair, aces);
    }

    [Test]
    public void Pair_BlackJokersBeatsTrumpPair()
    {
        Debug.Log("Pair of Black Jokers should beat Pair of trump Queens — jokers outrank trump");
        var blackPair  = V(C(Card.Rank.BlackJoker), C(Card.Rank.BlackJoker, Card.Suit.Hearts));
        var trumpPair  = V(C(Card.Rank.Queen), C(Card.Rank.Queen, Card.Suit.Hearts));
        AssertBeats(true, blackPair, trumpPair, Card.Rank.Queen);
    }

    [Test]
    public void Pair_MixedJokersIsInvalid()
    {
        Debug.Log("One Red Joker + one Black Joker should NOT form a valid pair — joker pairs must be same color");
        var mixed = SetValidator.Validate(
            new[] { C(Card.Rank.RedJoker), C(Card.Rank.BlackJoker) }, null);
        Debug.Log($"Validate result: IsValid={mixed.IsValid}, Reason={mixed.FailReason}");
        Assert.IsFalse(mixed.IsValid);
    }

    // -------------------------------------------------------------------------
    // Integration: Validate() with MustBeat in GameContext
    // -------------------------------------------------------------------------

    [Test]
    public void Validate_RejectsSetThatDoesNotBeat()
    {
        Debug.Log("Validate() with MustBeat=Pair of Kings should reject Pair of Queens");
        var tableKings = V(C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts));
        var context = new SetValidator.GameContext
        {
            MustBeat     = tableKings,
            RequiredType = SetValidator.SetType.Pair,
        };
        var result = SetValidator.Validate(
            new[] { C(Card.Rank.Queen), C(Card.Rank.Queen, Card.Suit.Hearts) },
            context);
        Debug.Log($"Validate result: IsValid={result.IsValid}, Code={result.Code}, Reason={result.FailReason}");
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(SetValidator.FailCode.DoesNotBeat, result.Code);
    }

    [Test]
    public void Validate_AcceptsSetThatBeats()
    {
        Debug.Log("Validate() with MustBeat=Pair of Queens should accept Pair of Kings");
        var tableQueens = V(C(Card.Rank.Queen), C(Card.Rank.Queen, Card.Suit.Hearts));
        var context = new SetValidator.GameContext
        {
            MustBeat     = tableQueens,
            RequiredType = SetValidator.SetType.Pair,
        };
        var result = SetValidator.Validate(
            new[] { C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts) },
            context);
        Debug.Log($"Validate result: IsValid={result.IsValid}");
        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_BombBeatsNonBombEvenWithRequiredType()
    {
        Debug.Log("Validate() with MustBeat=Pair of Queens and RequiredType=Pair should still accept a 4-Bomb");
        var tableQueens = V(C(Card.Rank.Queen), C(Card.Rank.Queen, Card.Suit.Hearts));
        var context = new SetValidator.GameContext
        {
            MustBeat     = tableQueens,
            RequiredType = SetValidator.SetType.Pair,
        };
        var result = SetValidator.Validate(
            new[]
            {
                C(Card.Rank.Two), C(Card.Rank.Two, Card.Suit.Hearts),
                C(Card.Rank.Two, Card.Suit.Diamonds), C(Card.Rank.Two, Card.Suit.Clubs)
            },
            context);
        Debug.Log($"Validate result: IsValid={result.IsValid}, Type={result.Type}");
        Assert.IsTrue(result.IsValid);
    }
}
