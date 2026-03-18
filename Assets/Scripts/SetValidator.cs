using System.Collections.Generic;

/// <summary>
/// Pure logic class — zero Unity dependencies.
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
        ConsecutiveTriplePairs,
        TripleConsecutivePairs,
        Bomb4, Bomb5, Bomb6, Bomb7, Bomb8,
        StraightFlush,
        JokerBomb
    }

    public class ValidationResult
    {
        public bool   IsValid;
        public SetType Type;
        public string  Description;
        public string  FailReason;
    }

    /// <summary>
    /// Injected by TurnManager once turn enforcement is implemented.
    /// Null = no constraints (first player in round).
    /// </summary>
    public class GameContext
    {
        public SetType?          RequiredType;
        public ValidationResult  MustBeat;    // null = no constraint
    }

    /// <summary>
    /// Called only on commit — never during selection.
    /// Full rule implementation is tracked separately; stub returns valid for any non-empty selection.
    /// </summary>
    public static ValidationResult Validate(
        IReadOnlyList<Card.CardId> cards,
        GameContext context = null)
    {
        // TODO: implement full Guandan rule set (sets, bombs, type lock, beat-previous)
        if (cards.Count == 0)
            return new ValidationResult { IsValid = false, FailReason = "No cards selected" };

        return new ValidationResult
        {
            IsValid     = true,
            Type        = SetType.Unknown,
            Description = Describe(cards)
        };
    }

    /// <summary>
    /// Neutral identification shown in the staging bar during selection.
    /// Never returns a valid/invalid judgment — descriptive only.
    /// </summary>
    public static string Describe(IReadOnlyList<Card.CardId> cards)
    {
        if (cards.Count == 0) return string.Empty;
        // TODO: identify set type and return e.g. "Pair of 5s", "Straight 3–7"
        return $"{cards.Count} card{(cards.Count == 1 ? "" : "s")}";
    }
}
