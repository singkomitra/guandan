using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks the state of the current trick: the controlling set on the table and the play history.
/// State is driven exclusively by TurnManager RPCs — never by local SelectionManager events.
///
/// TurnManager calls:
///   ApplyPlay  — after the server validates a card play and broadcasts it to all clients.
///   ApplyPass  — after the server broadcasts a pass.
///   StartTrick — after all remaining players pass, starting a new trick.
///   SetTrumpRank — once at game start, before the first trick.
/// </summary>
public class TrickManager : MonoBehaviour
{
    public static TrickManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Nested types ─────────────────────────────────────────────────────────

    /// <summary>
    /// One play within a trick. PlayerId is the seat index of the player who played.
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

    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private Card.Rank _trumpRank = Card.Rank.Two;

    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>
    /// The set currently on the table that subsequent players must beat.
    /// Null when the trick is open (the leader has not yet played).
    /// </summary>
    public SetValidator.ValidationResult ControllingSet { get; private set; }

    /// <summary>All plays in the current trick, oldest first. Cleared on StartTrick.</summary>
    public IReadOnlyList<PlayRecord> TrickHistory => _history;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired after a set is applied and recorded. PlayRecord.PlayerId is the seat index.
    /// TableDisplay uses this to render remote plays; local plays are already animated
    /// via HandManager.CardsPlayed before this fires.
    /// </summary>
    public event Action<PlayRecord> SetPlayed;

    /// <summary>Fired after trick state is reset. Subscribe to clear the table display.</summary>
    public event Action TrickStarted;

    // ── Private ───────────────────────────────────────────────────────────────

    private readonly List<PlayRecord> _history = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Records a validated play from any player. Called by TurnManager.RpcApplyPlay on all clients.
    /// Updates the controlling set, pushes context into SelectionManager, and fires SetPlayed.
    /// </summary>
    public void ApplyPlay(int seatIndex, IReadOnlyList<Card.CardId> cards, SetValidator.ValidationResult result)
    {
        ControllingSet = result;
        var record = new PlayRecord(seatIndex, cards, result);
        _history.Add(record);
        PushContext();
        SetPlayed?.Invoke(record);
    }

    /// <summary>
    /// Records a pass from a player. Called by TurnManager.RpcApplyPass on all clients.
    /// Currently a no-op beyond logging; pass history could be surfaced via TrickHistory if needed.
    /// </summary>
    public void ApplyPass(int seatIndex)
    {
        Debug.Log($"[TrickManager] Seat {seatIndex} passed.");
    }

    /// <summary>
    /// Resets trick state. Called by TurnManager.RpcStartNewTrick and at game start.
    /// </summary>
    [ContextMenu("Start New Trick")]
    public void StartTrick()
    {
        ControllingSet = null;
        _history.Clear();
        PushContext();
        TrickStarted?.Invoke();
    }

    /// <summary>
    /// Sets the trump rank for the current deal. Called by TurnManager.RpcSyncGameStart.
    /// </summary>
    public void SetTrumpRank(Card.Rank rank)
    {
        _trumpRank = rank;
        PushContext();
    }

    // ── Private ───────────────────────────────────────────────────────────────

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
