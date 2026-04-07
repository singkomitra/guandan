using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks the state of the current trick: the controlling set and the required set type.
/// Builds and injects GameContext into SelectionManager on every state change so that
/// SetValidator always validates against the current table.
///
/// Turn enforcement (whose turn it is, pass counting) is not yet implemented.
/// When TurnManager arrives it will call StartTrick() and gate player input;
/// this class's interface will not change.
/// </summary>
public class TrickManager : MonoBehaviour
{
    // ── Nested types ─────────────────────────────────────────────────────────

    /// <summary>
    /// One play within a trick. PlayerId is -1 until TurnManager assigns real player IDs.
    /// Designed to be network-serialization-friendly (value types + immutable list).
    /// </summary>
    public readonly struct PlayRecord
    {
        public readonly int                           PlayerId;
        public readonly IReadOnlyList<Card.CardId>    Cards;
        public readonly SetValidator.ValidationResult Result;

        public PlayRecord(int playerId, IReadOnlyList<Card.CardId> cards, SetValidator.ValidationResult result)
        {
            PlayerId = playerId;
            Cards    = cards;
            Result   = result;
        }
    }

    // ── Singleton ─────────────────────────────────────────────────────────────

    public static TrickManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Dev-time default used until DealManager exists to supply the trump rank per deal.
    /// At that point this field will be set via a method call and the SerializeField removed.
    /// </summary>
    [SerializeField] private Card.Rank _trumpRank = Card.Rank.Two;

    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>
    /// The set currently on the table that subsequent players must beat.
    /// Null when the trick is open (the leader has not yet played).
    /// Callers must not hold onto the returned reference across frames;
    /// it is replaced (not mutated) on every <see cref="StartTrick"/> or commit.
    /// </summary>
    public SetValidator.ValidationResult ControllingSet { get; private set; }

    /// <summary>All plays in the current trick, oldest first. Cleared on StartTrick.</summary>
    public IReadOnlyList<PlayRecord> TrickHistory => _history;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired after a set is committed and recorded. Subscribe for display/networking.</summary>
    public event Action<PlayRecord> SetPlayed;

    /// <summary>Fired after trick state is reset. Subscribe to clear the table display.</summary>
    public event Action TrickStarted;

    // ── Private ───────────────────────────────────────────────────────────────

    private readonly List<PlayRecord> _history = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()     => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void OnEnable()  => SelectionManager.Instance.SelectionCommitted += OnSetCommitted;
    private void OnDisable() => SelectionManager.Instance.SelectionCommitted -= OnSetCommitted;

    // TODO: remove once DealManager exists. DealManager should call StartTrick() for the
    // first trick of each deal; TurnManager should call it for every subsequent trick.
    private void Start() => StartTrick();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets trick state. Call when all other players have passed and a new trick begins.
    /// Also exposed as a context menu item for manual testing in the Inspector.
    /// </summary>
    [ContextMenu("Start New Trick")]
    public void StartTrick()
    {
        ControllingSet = null;
        _history.Clear();
        PushContext();
        TrickStarted?.Invoke();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnSetCommitted(IReadOnlyList<Card.CardId> cards, SetValidator.ValidationResult result)
    {
        ControllingSet = result;
        var record = new PlayRecord(-1, cards, result);
        _history.Add(record);
        PushContext();
        SetPlayed?.Invoke(record);
    }

    private void PushContext()
    {
        SelectionManager.Instance.SetContext(new SetValidator.GameContext
        {
            MustBeat     = ControllingSet,
            RequiredType = ControllingSet?.Type,
            TrumpRank    = _trumpRank,
        });
    }
}
