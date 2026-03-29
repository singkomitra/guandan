using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for trump-of-hearts wildcard support in SetValidator.
/// The trump-of-hearts (trumpRank + Hearts) substitutes for any missing non-joker card.
/// Pure logic — no scene required.
/// </summary>
public class SetValidatorWildcardTests
{
    // --- Shorthand helpers ---

    static Card.CardId C(Card.Rank rank, Card.Suit suit = Card.Suit.Spades) =>
        new Card.CardId { rank = rank, suit = suit };

    /// <summary>Returns the trump-of-hearts wildcard for a given trump rank.</summary>
    static Card.CardId W(Card.Rank trump) =>
        new Card.CardId { rank = trump, suit = Card.Suit.Hearts };

    /// <summary>Validates with an explicit trump rank.</summary>
    static SetValidator.ValidationResult VT(Card.Rank trump, params Card.CardId[] cards) =>
        SetValidator.Validate(cards, new SetValidator.GameContext { TrumpRank = trump });

    static void AssertValid(SetValidator.ValidationResult r, SetValidator.SetType expectedType)
    {
        Debug.Log($"Result: IsValid={r.IsValid}, Type={r.Type}, KeyRank={r.KeyRank}, Desc={r.Description}");
        Assert.IsTrue(r.IsValid, $"Expected valid {expectedType} but got: {r.FailReason}");
        Assert.AreEqual(expectedType, r.Type);
    }

    static void AssertInvalid(SetValidator.ValidationResult r)
    {
        Debug.Log($"Result: IsValid={r.IsValid}, Code={r.Code}, Reason={r.FailReason}");
        Assert.IsFalse(r.IsValid);
    }

    // =========================================================================
    // Identity: wildcard plays as its natural rank when no substitution needed
    // =========================================================================

    [Test]
    public void Identity_LoneWildcard_IsValidSingle()
    {
        Debug.Log("Lone trump-of-hearts should be a valid Single of the trump rank");
        var r = VT(Card.Rank.Two, W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.Single);
        Assert.AreEqual(Card.Rank.Two, r.KeyRank);
    }

    [Test]
    public void Identity_TwoWildcards_IsNaturalPairOfTrumpRank()
    {
        Debug.Log("Two trump-of-hearts cards are a natural pair of the trump rank, no substitution");
        var r = VT(Card.Rank.Two, W(Card.Rank.Two), W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.Pair);
        Assert.AreEqual(Card.Rank.Two, r.KeyRank);
    }

    [Test]
    public void Identity_WildcardExtractsAndExtendsAbove()
    {
        Debug.Log("trump=Two: 2♥ is always extracted as wildcard; 3 4 5 6 + wildcard = Straight 3–7");
        // Wildcards are always extracted first — there is no natural-rank pass.
        var r = VT(Card.Rank.Two,
            W(Card.Rank.Two),
            C(Card.Rank.Three, Card.Suit.Hearts),
            C(Card.Rank.Four,  Card.Suit.Diamonds),
            C(Card.Rank.Five),
            C(Card.Rank.Six));
        AssertValid(r, SetValidator.SetType.Straight);
        Assert.AreEqual(Card.Rank.Three, r.KeyRank);
    }

    // =========================================================================
    // Wildcard Pair
    // =========================================================================

    [Test]
    public void WildcardPair_WithKing()
    {
        Debug.Log("Wildcard + K♠ = Pair of Kings");
        var r = VT(Card.Rank.Two, W(Card.Rank.Two), C(Card.Rank.King));
        AssertValid(r, SetValidator.SetType.Pair);
        Assert.AreEqual(Card.Rank.King, r.KeyRank);
    }

    [Test]
    public void WildcardPair_TrumpRankIsQueen()
    {
        Debug.Log("Wildcard (Q♥) + A♠ = Pair of Aces when trump=Queen");
        var r = VT(Card.Rank.Queen, W(Card.Rank.Queen), C(Card.Rank.Ace));
        AssertValid(r, SetValidator.SetType.Pair);
        Assert.AreEqual(Card.Rank.Ace, r.KeyRank);
    }

    [Test]
    public void WildcardPair_CannotPairWithBlackJoker()
    {
        Debug.Log("Wildcard + Black Joker should NOT form a valid pair — joker pairs cannot use wildcards");
        var r = VT(Card.Rank.Two, W(Card.Rank.Two), C(Card.Rank.BlackJoker));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardPair_CannotPairWithRedJoker()
    {
        Debug.Log("Wildcard + Red Joker should NOT form a valid pair");
        var r = VT(Card.Rank.Two, W(Card.Rank.Two), C(Card.Rank.RedJoker));
        AssertInvalid(r);
    }

    // =========================================================================
    // Wildcard Triple
    // =========================================================================

    [Test]
    public void WildcardTriple_PairOfKings()
    {
        Debug.Log("Wildcard + K♠ + K♦ = Triple Kings");
        var r = VT(Card.Rank.Two,
            W(Card.Rank.Two), C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts));
        AssertValid(r, SetValidator.SetType.Triple);
        Assert.AreEqual(Card.Rank.King, r.KeyRank);
    }

    [Test]
    public void WildcardTriple_TwoWildcards_OneNatural()
    {
        Debug.Log("Two wildcards + A♠ = Triple Aces");
        var r = VT(Card.Rank.Two,
            W(Card.Rank.Two), W(Card.Rank.Two), C(Card.Rank.Ace));
        AssertValid(r, SetValidator.SetType.Triple);
        Assert.AreEqual(Card.Rank.Ace, r.KeyRank);
    }

    [Test]
    public void WildcardTriple_MismatchedPair_Invalid()
    {
        Debug.Log("Wildcard + K♠ + Q♠ should NOT be a triple — mismatched ranks");
        var r = VT(Card.Rank.Two,
            W(Card.Rank.Two), C(Card.Rank.King), C(Card.Rank.Queen));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardTriple_JokerInNaturalCards_Invalid()
    {
        Debug.Log("Wildcard + BJ + BJ should NOT be a triple — wildcards cannot fill joker positions");
        var r = VT(Card.Rank.Two,
            W(Card.Rank.Two), C(Card.Rank.BlackJoker), C(Card.Rank.BlackJoker, Card.Suit.Hearts));
        AssertInvalid(r);
    }

    // =========================================================================
    // Wildcard Full House
    // =========================================================================

    [Test]
    public void WildcardFullHouse_ThreePlusOne_WildcardFillsPair()
    {
        Debug.Log("K K K + Q + wildcard = Full House Kings over Queens (wildcard fills pair)");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts), C(Card.Rank.King, Card.Suit.Diamonds),
            C(Card.Rank.Queen),
            W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.FullHouse);
        Assert.AreEqual(Card.Rank.King, r.KeyRank);
    }

    [Test]
    public void WildcardFullHouse_TwoPlusTwo_WildcardFillsHigherTriple()
    {
        Debug.Log("K K + Q Q + wildcard = Full House Kings over Queens (wildcard fills 3rd King — higher rank becomes triple)");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts),
            C(Card.Rank.Queen), C(Card.Rank.Queen, Card.Suit.Hearts),
            W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.FullHouse);
        Assert.AreEqual(Card.Rank.King, r.KeyRank);
    }

    [Test]
    public void WildcardFullHouse_JokerKicker_WildcardFillsTriple()
    {
        Debug.Log("A A + BJ BJ + wildcard = Full House Aces over Black Jokers (wildcard fills 3rd Ace)");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts),
            C(Card.Rank.BlackJoker), C(Card.Rank.BlackJoker, Card.Suit.Hearts),
            W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.FullHouse);
        Assert.AreEqual(Card.Rank.Ace, r.KeyRank);
    }

    [Test]
    public void WildcardFullHouse_IncompleteJokerKicker_Invalid()
    {
        Debug.Log("A A A + BJ + wildcard should be invalid — joker pair is incomplete and wildcard cannot fill it");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts), C(Card.Rank.Ace, Card.Suit.Diamonds),
            C(Card.Rank.BlackJoker),
            W(Card.Rank.Two));
        AssertInvalid(r);
    }

    // =========================================================================
    // Wildcard Straight
    // =========================================================================

    [Test]
    public void WildcardStraight_FillsInteriorGap()
    {
        Debug.Log("3 4 wildcard 6 7 = Straight 3–7 (wildcard fills 5)");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Three, Card.Suit.Hearts),
            C(Card.Rank.Four,  Card.Suit.Diamonds),
            W(Card.Rank.Two),
            C(Card.Rank.Six,   Card.Suit.Clubs),
            C(Card.Rank.Seven, Card.Suit.Spades));
        AssertValid(r, SetValidator.SetType.Straight);
        Assert.AreEqual(Card.Rank.Three, r.KeyRank);
    }

    [Test]
    public void WildcardStraight_ExtendsAbove()
    {
        Debug.Log("3 4 5 6 + wildcard = Straight 3–7 (wildcard extends above)");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Three), C(Card.Rank.Four, Card.Suit.Hearts),
            C(Card.Rank.Five,   Card.Suit.Diamonds), C(Card.Rank.Six),
            W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.Straight);
        Assert.AreEqual(Card.Rank.Three, r.KeyRank);
    }

    [Test]
    public void WildcardStraight_ExtendsBelow_WhenAboveIsAce()
    {
        Debug.Log("J Q K A + wildcard = Straight 10–A (forced to extend below)");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Jack), C(Card.Rank.Queen, Card.Suit.Hearts),
            C(Card.Rank.King,  Card.Suit.Diamonds), C(Card.Rank.Ace),
            W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.Straight);
        Assert.AreEqual(Card.Rank.Ten, r.KeyRank);
    }

    [Test]
    public void WildcardStraight_TwoGaps_Invalid()
    {
        Debug.Log("3 5 7 9 J + wildcard should be invalid — four gaps, only one wildcard");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Three), C(Card.Rank.Five), C(Card.Rank.Seven),
            C(Card.Rank.Nine),  C(Card.Rank.Jack),
            W(Card.Rank.Two));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardStraight_DuplicateRankInNaturals_Invalid()
    {
        Debug.Log("4 4 5 6 + wildcard should be invalid — duplicate rank among natural cards");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Four), C(Card.Rank.Four, Card.Suit.Hearts),
            C(Card.Rank.Five), C(Card.Rank.Six),
            W(Card.Rank.Two));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardStraight_SixCards_Invalid()
    {
        Debug.Log("3 4 wildcard 6 7 8 = 6 cards — straights are exactly 5 cards");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Three), C(Card.Rank.Four, Card.Suit.Hearts),
            W(Card.Rank.Two),
            C(Card.Rank.Six), C(Card.Rank.Seven, Card.Suit.Diamonds), C(Card.Rank.Eight));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardStraight_TwoWildcards_FillTwoGaps()
    {
        Debug.Log("3 5 7 + two wildcards = Straight 3–7 (wildcards fill 4 and 6)");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Three), W(Card.Rank.Two),
            C(Card.Rank.Five,   Card.Suit.Hearts), W(Card.Rank.Two),
            C(Card.Rank.Seven));
        AssertValid(r, SetValidator.SetType.Straight);
        Assert.AreEqual(Card.Rank.Three, r.KeyRank);
    }

    [Test]
    public void Straight_NoWrapAround_QKAA23_Invalid()
    {
        Debug.Log("Q K A 2 3 should be invalid — straights do not wrap around Ace→Two");
        var r = SetValidator.Validate(new[] {
            C(Card.Rank.Queen), C(Card.Rank.King), C(Card.Rank.Ace),
            C(Card.Rank.Two,   Card.Suit.Diamonds),
            C(Card.Rank.Three, Card.Suit.Clubs)
        }, null);
        AssertInvalid(r);
    }

    [Test]
    public void WildcardStraight_NoWrapAround_KAWildcard23_Invalid()
    {
        Debug.Log("K A + wildcard + 2 3 should be invalid — wildcard cannot bridge A→2 wrap");
        var r = VT(Card.Rank.Five,
            C(Card.Rank.King), C(Card.Rank.Ace),
            W(Card.Rank.Five),
            C(Card.Rank.Two, Card.Suit.Diamonds), C(Card.Rank.Three, Card.Suit.Clubs));
        AssertInvalid(r);
    }

    // =========================================================================
    // Wildcard Straight Flush
    // =========================================================================

    [Test]
    public void WildcardStraightFlush_FillsInteriorGap()
    {
        Debug.Log("9♦ 10♦ wildcard Q♦ K♦ = Straight Flush 9–K Diamonds (wildcard fills J♦)");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Nine,  Card.Suit.Diamonds),
            C(Card.Rank.Ten,   Card.Suit.Diamonds),
            W(Card.Rank.Two),
            C(Card.Rank.Queen, Card.Suit.Diamonds),
            C(Card.Rank.King,  Card.Suit.Diamonds));
        AssertValid(r, SetValidator.SetType.StraightFlush);
        Assert.AreEqual(Card.Rank.Nine, r.KeyRank);
    }

    [Test]
    public void WildcardStraightFlush_ExtendsAbove()
    {
        Debug.Log("9♦ 10♦ J♦ Q♦ + wildcard = Straight Flush 9–K Diamonds (wildcard extends above)");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Nine,  Card.Suit.Diamonds),
            C(Card.Rank.Ten,   Card.Suit.Diamonds),
            C(Card.Rank.Jack,  Card.Suit.Diamonds),
            C(Card.Rank.Queen, Card.Suit.Diamonds),
            W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.StraightFlush);
        Assert.AreEqual(Card.Rank.Nine, r.KeyRank);
    }

    [Test]
    public void WildcardStraightFlush_MixedSuits_Invalid()
    {
        Debug.Log("Mixed suits with wildcard should NOT be a StraightFlush");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Nine,  Card.Suit.Diamonds),
            C(Card.Rank.Ten,   Card.Suit.Spades),   // different suit
            W(Card.Rank.Two),
            C(Card.Rank.Queen, Card.Suit.Diamonds),
            C(Card.Rank.King,  Card.Suit.Diamonds));
        // Should be a Straight, not a StraightFlush
        Assert.AreNotEqual(SetValidator.SetType.StraightFlush, r.Type);
    }

    [Test]
    public void WildcardStraightFlush_BeatsFiveBomb()
    {
        Debug.Log("Wildcard Straight Flush should beat a 5-Bomb");
        var sf = VT(Card.Rank.Two,
            C(Card.Rank.Nine,  Card.Suit.Diamonds),
            C(Card.Rank.Ten,   Card.Suit.Diamonds),
            W(Card.Rank.Two),
            C(Card.Rank.Queen, Card.Suit.Diamonds),
            C(Card.Rank.King,  Card.Suit.Diamonds));
        var fiveBomb = SetValidator.Validate(new[] {
            C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts),
            C(Card.Rank.Ace, Card.Suit.Diamonds), C(Card.Rank.Ace, Card.Suit.Clubs),
            C(Card.Rank.Ace, Card.Suit.Spades)
        }, null);
        Assert.IsTrue(SetValidator.Beats(sf, fiveBomb, Card.Rank.Two));
    }

    // =========================================================================
    // Wildcard N-Bomb
    // =========================================================================

    [Test]
    public void WildcardBomb4_ThreeAces()
    {
        Debug.Log("A♠ A♦ A♣ + wildcard = 4-Bomb of Aces");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts),
            C(Card.Rank.Ace, Card.Suit.Diamonds),
            W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.Bomb4);
        Assert.AreEqual(Card.Rank.Ace, r.KeyRank);
    }

    [Test]
    public void WildcardBomb5_FourKings()
    {
        Debug.Log("K K K K + wildcard = 5-Bomb of Kings");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts),
            C(Card.Rank.King, Card.Suit.Diamonds), C(Card.Rank.King, Card.Suit.Clubs),
            W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.Bomb5);
        Assert.AreEqual(Card.Rank.King, r.KeyRank);
    }

    [Test]
    public void WildcardBomb4_MismatchedRanks_Invalid()
    {
        Debug.Log("A A K + wildcard should NOT be a 4-Bomb — mismatched ranks");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts),
            C(Card.Rank.King),
            W(Card.Rank.Two));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardBomb4_JokerInNaturals_Invalid()
    {
        Debug.Log("BJ BJ RJ + wildcard should NOT be a 4-Bomb — jokers cannot form a bomb with wildcards");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.BlackJoker), C(Card.Rank.BlackJoker, Card.Suit.Hearts),
            C(Card.Rank.RedJoker),
            W(Card.Rank.Two));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardBomb4_BeatsNonBomb()
    {
        Debug.Log("Wildcard 4-Bomb should beat a Pair of Aces");
        var bomb = VT(Card.Rank.Two,
            C(Card.Rank.Three), C(Card.Rank.Three, Card.Suit.Hearts),
            C(Card.Rank.Three, Card.Suit.Diamonds),
            W(Card.Rank.Two));
        var pair = SetValidator.Validate(
            new[] { C(Card.Rank.Ace), C(Card.Rank.Ace, Card.Suit.Hearts) }, null);
        Assert.IsTrue(SetValidator.Beats(bomb, pair, Card.Rank.Two));
    }

    // =========================================================================
    // Wildcard Consecutive Triple Pairs
    // =========================================================================

    // =========================================================================
    // Scalability: extension logic with minimal natural cards
    // With MaxWildcardsPerSet=2, a straight needs at least 3 natural cards.
    // When MaxWildcardsPerSet is raised, add 1-natural + 4-wildcard cases here
    // to confirm TryConsecutiveSequence correctly places a 5-card straight from
    // a single anchor (e.g. 6♦ + 4 wildcards = Straight 4–8).
    // =========================================================================

    [Test]
    public void WildcardStraight_BothWildcardsForcedBelow()
    {
        Debug.Log("Q♠ K♥ A♦ + two wildcards: canGoAbove=0, both extend below → 10–A");
        // gaps=0, leftover=2, canGoAbove=Ace-Ace=0, goAbove=0, goBelow=2
        // Queen(12) - 2 = 10 = Ten >= Two → valid; mixed suits so not StraightFlush
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Queen),
            C(Card.Rank.King,  Card.Suit.Hearts),
            C(Card.Rank.Ace,   Card.Suit.Diamonds),
            W(Card.Rank.Two), W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.Straight);
        Assert.AreEqual(Card.Rank.Ten, r.KeyRank);
    }

    [Test]
    public void WildcardConsecutiveTriples_WildcardFillsSecondTriple()
    {
        Debug.Log("5 5 5 + 6 6 + wildcard = Consecutive Triples 5–6");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts), C(Card.Rank.Five, Card.Suit.Diamonds),
            C(Card.Rank.Six),  C(Card.Rank.Six,  Card.Suit.Hearts),
            W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.ConsecutiveTriplePairs);
        Assert.AreEqual(Card.Rank.Five, r.KeyRank);
    }

    [Test]
    public void WildcardConsecutiveTriples_TwoWildcards()
    {
        Debug.Log("5 5 + 6 6 + two wildcards = Consecutive Triples 5–6");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts),
            C(Card.Rank.Six),  C(Card.Rank.Six,  Card.Suit.Hearts),
            W(Card.Rank.Two),  W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.ConsecutiveTriplePairs);
        Assert.AreEqual(Card.Rank.Five, r.KeyRank);
    }

    [Test]
    public void WildcardConsecutiveTriples_NonConsecutiveRanks_Invalid()
    {
        Debug.Log("5 5 5 + 7 7 + wildcard should be invalid — ranks 5 and 7 are not consecutive");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts), C(Card.Rank.Five, Card.Suit.Diamonds),
            C(Card.Rank.Seven), C(Card.Rank.Seven, Card.Suit.Hearts),
            W(Card.Rank.Two));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardConsecutiveTriples_JokerInNaturals_Invalid()
    {
        Debug.Log("Consecutive triples cannot include jokers");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts), C(Card.Rank.Five, Card.Suit.Diamonds),
            C(Card.Rank.BlackJoker), C(Card.Rank.BlackJoker, Card.Suit.Hearts),
            W(Card.Rank.Two));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardConsecutiveTriples_UnevenGroups_Invalid()
    {
        Debug.Log("5 5 5 5 + 6 6 is invalid — rank 5 overflows the triple slot");
        var r = SetValidator.Validate(new[] {
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts),
            C(Card.Rank.Five, Card.Suit.Diamonds), C(Card.Rank.Five, Card.Suit.Clubs),
            C(Card.Rank.Six), C(Card.Rank.Six, Card.Suit.Hearts)
        }, null);
        AssertInvalid(r);
    }

    [Test]
    public void ConsecutiveTriples_SevenCardsWithWildcard_WrongTotalForTwoTriples_Invalid()
    {
        Debug.Log("5 5 5 + 6 6 6 + wildcard = 7 cards — two triples require exactly 6 cards");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts), C(Card.Rank.Five, Card.Suit.Diamonds),
            C(Card.Rank.Six),  C(Card.Rank.Six,  Card.Suit.Hearts), C(Card.Rank.Six,  Card.Suit.Diamonds),
            W(Card.Rank.Two));
        AssertInvalid(r);
    }

    // =========================================================================
    // Wildcard Triple Consecutive Pairs
    // =========================================================================

    [Test]
    public void WildcardTripleConsecPairs_WildcardFillsOneSlot()
    {
        Debug.Log("5 5 + 6 6 + 7 + wildcard = Consecutive Pairs 5–7");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts),
            C(Card.Rank.Six),  C(Card.Rank.Six,  Card.Suit.Hearts),
            C(Card.Rank.Seven),
            W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.TripleConsecutivePairs);
        Assert.AreEqual(Card.Rank.Five, r.KeyRank);
    }

    [Test]
    public void WildcardTripleConsecPairs_TwoWildcards()
    {
        Debug.Log("5 5 + 6 + 7 + two wildcards = Consecutive Pairs 5–7");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts),
            C(Card.Rank.Six),
            C(Card.Rank.Seven),
            W(Card.Rank.Two), W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.TripleConsecutivePairs);
        Assert.AreEqual(Card.Rank.Five, r.KeyRank);
    }

    [Test]
    public void WildcardTripleConsecPairs_NonConsecutiveRanks_Invalid()
    {
        Debug.Log("5 5 + 6 6 + 8 + wildcard should be invalid — ranks not consecutive");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts),
            C(Card.Rank.Six),  C(Card.Rank.Six,  Card.Suit.Hearts),
            C(Card.Rank.Eight),
            W(Card.Rank.Two));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardTripleConsecPairs_JokerInNaturals_Invalid()
    {
        Debug.Log("Triple consecutive pairs cannot include jokers");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Five),  C(Card.Rank.Five,  Card.Suit.Hearts),
            C(Card.Rank.Six),   C(Card.Rank.Six,   Card.Suit.Hearts),
            C(Card.Rank.BlackJoker),
            W(Card.Rank.Two));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardTripleConsecPairs_UnevenGroups_Invalid()
    {
        Debug.Log("5 + 6 6 + 7 7 7 is invalid — rank 7 overflows the pair slot");
        var r = SetValidator.Validate(new[] {
            C(Card.Rank.Five),
            C(Card.Rank.Six),  C(Card.Rank.Six,  Card.Suit.Hearts),
            C(Card.Rank.Seven), C(Card.Rank.Seven, Card.Suit.Hearts), C(Card.Rank.Seven, Card.Suit.Diamonds)
        }, null);
        AssertInvalid(r);
    }

    [Test]
    public void TripleConsecPairs_SevenCardsWithWildcards_NotDivisibleByThreeGroups_Invalid()
    {
        Debug.Log("5 5 + 6 6 + 7 + two wildcards = 7 cards — cannot split into 3 equal groups");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Five), C(Card.Rank.Five, Card.Suit.Hearts),
            C(Card.Rank.Six),  C(Card.Rank.Six,  Card.Suit.Hearts),
            C(Card.Rank.Seven),
            W(Card.Rank.Two), W(Card.Rank.Two));
        AssertInvalid(r);
    }

    // =========================================================================
    // Scalability: wildcards-heavy extension paths
    // Tests below use Assume.That(MaxWildcardsPerSet >= N) so they are skipped
    // today (cap=2) but run automatically once the constant is raised, verifying
    // the gap-sum and extension logic holds for more wildcards without any code
    // changes to SetValidator.
    // =========================================================================

    [Test]
    public void WildcardStraight_SixCards_BothWildcardsForcedBelow_Invalid()
    {
        Debug.Log("J♠ Q♥ K♦ A♣ + two wildcards = 6 cards — straights are exactly 5 cards");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Jack),
            C(Card.Rank.Queen, Card.Suit.Hearts),
            C(Card.Rank.King,  Card.Suit.Diamonds),
            C(Card.Rank.Ace,   Card.Suit.Clubs),
            W(Card.Rank.Two), W(Card.Rank.Two));
        AssertInvalid(r);
    }

    [Test]
    public void WildcardStraight_ThreeWildcards_TwoNaturalsMixedSuits()
    {
        // Skipped until MaxWildcardsPerSet >= 3.
        // 6♦ + 8♠ + 3 wildcards = 5 cards; mixed suits → Straight (not flush)
        // gaps=(8-6-1)=1, leftover=3-1=2, goAbove=2, goBelow=0 → Straight 6–10
        Assume.That(SetValidator.MaxWildcardsPerSet, Is.GreaterThanOrEqualTo(3),
            $"Requires MaxWildcardsPerSet >= 3 (currently {SetValidator.MaxWildcardsPerSet})");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Six,   Card.Suit.Diamonds),
            C(Card.Rank.Eight, Card.Suit.Spades),
            W(Card.Rank.Two), W(Card.Rank.Two), W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.Straight);
        Assert.AreEqual(Card.Rank.Six, r.KeyRank);
    }

    [Test]
    public void WildcardStraightFlush_FourWildcards_OneNatural()
    {
        // Skipped until MaxWildcardsPerSet >= 4.
        // 6♦ + 4 wildcards = 5 cards; single suit → wildcards adopt Diamonds → Straight Flush 6–10
        // gaps=0, leftover=4, canGoAbove=8, goAbove=4, goBelow=0 → keyRank=Six
        Assume.That(SetValidator.MaxWildcardsPerSet, Is.GreaterThanOrEqualTo(4),
            $"Requires MaxWildcardsPerSet >= 4 (currently {SetValidator.MaxWildcardsPerSet})");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.Six, Card.Suit.Diamonds),
            W(Card.Rank.Two), W(Card.Rank.Two), W(Card.Rank.Two), W(Card.Rank.Two));
        AssertValid(r, SetValidator.SetType.StraightFlush);
        Assert.AreEqual(Card.Rank.Six, r.KeyRank);
    }

    // =========================================================================
    // Joker set exclusions
    // =========================================================================

    [Test]
    public void JokerBomb_CannotUseWildcard()
    {
        Debug.Log("BJ BJ RJ RJ + wildcard should NOT be a Joker Bomb");
        var r = VT(Card.Rank.Two,
            C(Card.Rank.BlackJoker), C(Card.Rank.BlackJoker, Card.Suit.Hearts),
            C(Card.Rank.RedJoker),   C(Card.Rank.RedJoker,   Card.Suit.Hearts),
            W(Card.Rank.Two));
        Assert.AreNotEqual(SetValidator.SetType.JokerBomb, r.Type);
    }

    [Test]
    public void JokerPair_CannotUseWildcard()
    {
        Debug.Log("BJ + wildcard should NOT be a valid joker pair");
        var r = VT(Card.Rank.Two, C(Card.Rank.BlackJoker), W(Card.Rank.Two));
        AssertInvalid(r);
    }

    // =========================================================================
    // Beats integration
    // =========================================================================

    [Test]
    public void Beats_WildcardPair_BeatsLowerNaturalPair()
    {
        Debug.Log("Wildcard Pair of Kings should beat natural Pair of Queens");
        var wildKings  = VT(Card.Rank.Two, W(Card.Rank.Two), C(Card.Rank.King));
        var natQueens  = SetValidator.Validate(
            new[] { C(Card.Rank.Queen), C(Card.Rank.Queen, Card.Suit.Hearts) }, null);
        Assert.IsTrue(SetValidator.Beats(wildKings, natQueens, Card.Rank.Two));
    }

    [Test]
    public void Beats_WildcardPair_LosesToHigherNaturalPair()
    {
        Debug.Log("Wildcard Pair of Queens should NOT beat natural Pair of Kings");
        var wildQueens = VT(Card.Rank.Two, W(Card.Rank.Two), C(Card.Rank.Queen));
        var natKings   = SetValidator.Validate(
            new[] { C(Card.Rank.King), C(Card.Rank.King, Card.Suit.Hearts) }, null);
        Assert.IsFalse(SetValidator.Beats(wildQueens, natKings, Card.Rank.Two));
    }

    [Test]
    public void Beats_WildcardStraight_BeatsLowerStart()
    {
        Debug.Log("Wildcard Straight 3–7 (gap filled) should beat natural Straight 2–6");
        var wildStraight = VT(Card.Rank.Two,
            C(Card.Rank.Three), C(Card.Rank.Four, Card.Suit.Hearts),
            W(Card.Rank.Two),
            C(Card.Rank.Six, Card.Suit.Diamonds), C(Card.Rank.Seven));
        var natStraight = SetValidator.Validate(new[] {
            C(Card.Rank.Two,   Card.Suit.Spades),
            C(Card.Rank.Three, Card.Suit.Hearts),
            C(Card.Rank.Four,  Card.Suit.Diamonds),
            C(Card.Rank.Five,  Card.Suit.Clubs),
            C(Card.Rank.Six,   Card.Suit.Spades)
        }, null);
        Assert.IsTrue(SetValidator.Beats(wildStraight, natStraight, Card.Rank.Two));
    }

    [Test]
    public void Beats_WildcardBomb4_BeatsNonBomb()
    {
        Debug.Log("Wildcard 4-Bomb should beat any non-bomb regardless of rank");
        var bomb = VT(Card.Rank.Two,
            C(Card.Rank.Three), C(Card.Rank.Three, Card.Suit.Hearts),
            C(Card.Rank.Three, Card.Suit.Diamonds),
            W(Card.Rank.Two));
        var straight = SetValidator.Validate(new[] {
            C(Card.Rank.Nine),  C(Card.Rank.Ten,   Card.Suit.Hearts),
            C(Card.Rank.Jack,   Card.Suit.Diamonds),
            C(Card.Rank.Queen), C(Card.Rank.King)
        }, null);
        Assert.IsTrue(SetValidator.Beats(bomb, straight, Card.Rank.Two));
    }
}
