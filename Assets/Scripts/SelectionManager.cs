using System;
using System.Collections.Generic;

/// <summary>
/// Coordinator for card selection state. Plain C# class — not a MonoBehaviour.
/// Owns the staged card list and is the single path to committing a play.
///
/// Dependency rule: knows SetValidator (logic layer) but has no Unity or UI references.
/// UI components subscribe to events and call methods; they never mutate state directly.
/// </summary>
public class SelectionManager
{
    public static readonly SelectionManager Instance = new();

    private readonly List<Card.CardId> _staged = new();
    private SetValidator.GameContext    _context;

    /// <summary>False when it is not this player's turn. Commit and Pass become no-ops.</summary>
    public bool IsPlayerTurn { get; set; } = true;

    public IReadOnlyList<Card.CardId> Staged => _staged;

    // --- Events ---

    /// <summary>Fired on every toggle. Carries the current staged list — no validity info.</summary>
    public event Action<IReadOnlyList<Card.CardId>> SelectionChanged;

    /// <summary>Fired after a valid commit. Carries the cards that were played.</summary>
    public event Action<IReadOnlyList<Card.CardId>> SelectionCommitted;

    /// <summary>Fired when selection is explicitly cleared (Clear or Pass).</summary>
    public event Action SelectionCleared;

    /// <summary>Fired when Commit is attempted but SetValidator rejects the set.</summary>
    public event Action<SetValidator.ValidationResult> CommitFailed;

    // --- Mutation ---

    /// <summary>Add or remove a card from the staged list.</summary>
    public void Toggle(Card.CardId id)
    {
        if (_staged.Contains(id))
            _staged.Remove(id);
        else
            _staged.Add(id);

        SelectionChanged?.Invoke(_staged);
    }

    /// <summary>
    /// Remove a specific card from staged without affecting others.
    /// No-op if the card is not currently staged.
    /// Called by HandManager when a card leaves the hand outside of a normal commit.
    /// </summary>
    public void Deselect(Card.CardId id)
    {
        if (!_staged.Remove(id)) return;
        SelectionChanged?.Invoke(_staged);
    }

    /// <summary>
    /// Attempt to play the staged set. Runs SetValidator — the only place validation occurs.
    /// On success: fires SelectionCommitted then resets state.
    /// On failure: fires CommitFailed, staged list unchanged.
    /// Returns true if the staged set passed validation and was committed.
    /// </summary>
    public bool Commit()
    {
        if (!IsPlayerTurn)
        {
            if (_staged.Count > 0)
                CommitFailed?.Invoke(new SetValidator.ValidationResult { IsValid = false, Code = SetValidator.FailCode.NotYourTurn, FailReason = "Not your turn" });
            return false;
        }
        if (_staged.Count == 0) return false;

        var result = SetValidator.Validate(_staged, _context);
        if (result.IsValid)
        {
            var committed = new List<Card.CardId>(_staged);
            _staged.Clear();
            SelectionCommitted?.Invoke(committed);
            SelectionChanged?.Invoke(_staged);
            return true;
        }
        else
        {
            CommitFailed?.Invoke(result);
            return false;
        }
    }

    /// <summary>Deselect all cards and return them to the hand.</summary>
    public void Clear()
    {
        if (_staged.Count == 0) return;
        _staged.Clear();
        SelectionCleared?.Invoke();
        SelectionChanged?.Invoke(_staged);
    }

    /// <summary>Clear selection and pass the turn (turn advance wired by TurnManager later).</summary>
    public void Pass()
    {
        if (!IsPlayerTurn) return;
        _staged.Clear();
        SelectionCleared?.Invoke();
        SelectionChanged?.Invoke(_staged);
    }

    /// <summary>Injected by TurnManager when turn context changes (required type, must-beat set).</summary>
    public void SetContext(SetValidator.GameContext context) => _context = context;
}
