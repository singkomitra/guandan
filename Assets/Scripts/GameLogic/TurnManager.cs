using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Server-authoritative turn manager. Validates plays server-side, broadcasts
/// accepted plays and passes to all clients, and enforces turn order.
///
/// Flow for a card play:
///   1. Local player commits → SelectionManager.SelectionCommitted fires
///   2. TurnManager hears it → calls Player.LocalPlayer.CmdPlayCards
///   3. Server: ServerHandlePlay validates the play; rejects or broadcasts RpcApplyPlay
///   4. All clients: TrickManager.ApplyPlay updates state and fires SetPlayed
///   5. Server: AdvanceTurn → _currentSeat SyncVar propagates → IsPlayerTurn updated
///
/// Flow for a pass:
///   1. Local player passes → SelectionManager.Passed fires
///   2. TurnManager → Player.LocalPlayer.CmdPass → ServerHandlePass
///   3. Server: RpcApplyPass broadcast; trick-end check; AdvanceTurn or RpcStartNewTrick
///   4. All clients: TrickManager updated; IsPlayerTurn updated via SyncVar hook
///
/// Must be placed as a scene NetworkBehaviour (NetworkIdentity on the same GameObject).
/// </summary>
public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance { get; private set; }

    // ── Networked state ────────────────────────────────────────────────────────

    /// <summary>
    /// Seat index of the player whose turn it is. -1 means the game has not started.
    /// The hook fires on all clients (including host) whenever this changes.
    /// </summary>
    [SyncVar(hook = nameof(OnCurrentSeatChanged))]
    private int _currentSeat = -1;

    /// <summary>Trump rank for the current deal. Synced to all clients at game start.</summary>
    [SyncVar]
    private Card.Rank _trumpRank = Card.Rank.Two;
    public Card.Rank TrumpRank => _trumpRank;

    public int CurrentSeat => _currentSeat;

    /// <summary>True in the editor without a network connection. Bypasses all network paths.</summary>
    public static bool IsSoloMode { get; private set; }

    // ── Server-only state ──────────────────────────────────────────────────────

    /// <summary>Consecutive passes since the last play. Reset to 0 whenever a card set is played.</summary>
    private int _passCount;

    /// <summary>Seat that last played a card set. Used to determine who leads the next trick.</summary>
    private int _lastPlaySeat = -1;

    // ── Properties ────────────────────────────────────────────────────────────

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

#if UNITY_EDITOR
        IsSoloMode = !NetworkClient.active;
#endif

        // Subscribe here — Awake runs before Mirror disables scene NetworkBehaviours.
        // C# delegate subscriptions persist regardless of component enabled state.
        SelectionManager.Instance.IsPlayerTurn = false;
        SelectionManager.Instance.SelectionCommitted += OnLocalCommit;
        SelectionManager.Instance.Passed             += OnLocalPass;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SelectionManager.Instance.SelectionCommitted -= OnLocalCommit;
        SelectionManager.Instance.Passed             -= OnLocalPass;
    }

    // ── Local event handlers → Commands ───────────────────────────────────────

    private void OnLocalCommit(IReadOnlyList<Card.CardId> cards, SetValidator.ValidationResult result)
    {
        if (IsSoloMode)
        {
            TrickManager.Instance.ApplyPlay(0, cards, result);
            return;
        }
        if (Player.LocalPlayer == null) return;
        var arr = new Card.CardId[cards.Count];
        for (int i = 0; i < cards.Count; i++) arr[i] = cards[i];
        Player.LocalPlayer.CmdPlayCards(arr);
    }

    private void OnLocalPass()
    {
        if (IsSoloMode)
        {
            TrickManager.Instance.StartTrick();
            return;
        }
        if (Player.LocalPlayer != null) Player.LocalPlayer.CmdPass();
    }

    // ── Public server API ──────────────────────────────────────────────────────

    /// <summary>
    /// Called by DealManager after all hands have been dealt.
    /// Sets the trump rank, designates the lead seat, and opens the first trick.
    /// </summary>
    [Server]
    public void StartGame(int leadSeat, Card.Rank trumpRank)
    {
        _trumpRank   = trumpRank;
        _passCount   = 0;
        _lastPlaySeat = leadSeat;

        RpcSyncGameStart(trumpRank);

        // _currentSeat SyncVar change triggers OnCurrentSeatChanged on all clients.
        _currentSeat = leadSeat;
        Debug.Log($"[TurnManager] Game started. Lead seat: {leadSeat}, Trump: {trumpRank}");
    }

    // ── Server-side action handlers (called from Player Commands) ─────────────

    /// <summary>
    /// Validates a play submitted by <paramref name="player"/> and, if valid, broadcasts
    /// it to all clients. Called from Player.CmdPlayCards on the server.
    /// </summary>
    [Server]
    public void ServerHandlePlay(Player player, Card.CardId[] cards)
    {
        int seat = player.SeatIndex;
        if (seat < 0 || seat != _currentSeat)
        {
            TargetRpcRejectAction(player.connectionToClient, "Not your turn");
            return;
        }

        var context = new SetValidator.GameContext
        {
            MustBeat     = TrickManager.Instance.ControllingSet,
            RequiredType = TrickManager.Instance.ControllingSet?.Type,
            TrumpRank    = TrumpRank,
        };

        SetValidator.ValidationResult result = SetValidator.Validate(cards, context);
        if (!result.IsValid)
        {
            TargetRpcRejectAction(player.connectionToClient, result.FailReason);
            return;
        }

        if (!DealManager.Instance.TryConsumeCards(player.netId, cards))
        {
            TargetRpcRejectAction(player.connectionToClient, "You don't hold those cards");
            return;
        }

        _passCount    = 0;
        _lastPlaySeat = seat;

        RpcApplyPlay(seat, cards, result.Type, result.KeyRank, result.Description);
        AdvanceTurn();
    }

    /// <summary>
    /// Records a pass submitted by <paramref name="player"/>. If all other players have
    /// passed, ends the current trick and starts a new one. Called from Player.CmdPass.
    /// </summary>
    [Server]
    public void ServerHandlePass(Player player)
    {
        int seat = player.SeatIndex;
        if (seat < 0 || seat != _currentSeat) return;

        // The leader of a new trick must play — passing is only valid once a set is on the table.
        if (TrickManager.Instance.ControllingSet == null)
        {
            TargetRpcRejectAction(player.connectionToClient, "Must play to lead a trick");
            return;
        }

        _passCount++;
        RpcApplyPass(seat);

        int playerCount = SeatingManager.Instance.Players.Count;
        if (_passCount >= playerCount - 1)
        {
            // Everyone else has passed; the player who last played leads the next trick.
            _passCount   = 0;
            _currentSeat = _lastPlaySeat;
            RpcStartNewTrick(_lastPlaySeat);
        }
        else
        {
            AdvanceTurn();
        }
    }

    // ── Server helpers ─────────────────────────────────────────────────────────

    [Server]
    private void AdvanceTurn()
    {
        _currentSeat = SeatingManager.Instance.NextSeat(_currentSeat);
    }

    // ── Client RPCs ────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a server-validated play on all clients. The result fields are passed
    /// directly from the server — no re-validation needed on the client.
    /// </summary>
    [ClientRpc]
    private void RpcApplyPlay(int seatIndex, Card.CardId[] cards, SetValidator.SetType type, Card.Rank keyRank, string description)
    {
        var result = new SetValidator.ValidationResult
        {
            IsValid     = true,
            Type        = type,
            KeyRank     = keyRank,
            Description = description,
        };
        TrickManager.Instance.ApplyPlay(seatIndex, cards, result);
    }

    [ClientRpc]
    private void RpcApplyPass(int seatIndex)
    {
        TrickManager.Instance.ApplyPass(seatIndex);
    }

    [ClientRpc]
    private void RpcStartNewTrick(int leadSeatIndex)
    {
        TrickManager.Instance.StartTrick();
        Debug.Log($"[TurnManager] New trick. Leader: seat {leadSeatIndex}");
    }

    [ClientRpc]
    private void RpcSyncGameStart(Card.Rank trumpRank)
    {
        TrickManager.Instance.SetTrumpRank(trumpRank);
        TrickManager.Instance.StartTrick();
    }

    /// <summary>Sends rejection feedback only to the player whose action was rejected.</summary>
    [TargetRpc]
    private void TargetRpcRejectAction(NetworkConnectionToClient conn, string reason)
    {
        Debug.LogWarning($"[TurnManager] Action rejected: {reason}");
        AnnouncementOverlay.Show(AnnouncementType.Error, reason);
    }

    // ── Dev helpers ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
    /// <summary>
    /// Bypasses networking and lobby for editor solo testing.
    /// Gives the local player turn control and opens the first trick.
    /// </summary>
    public void DevStartSolo()
    {
        TrickManager.Instance.SetTrumpRank(Card.Rank.Two);
        TrickManager.Instance.StartTrick();
        SelectionManager.Instance.IsPlayerTurn = true;
        Debug.Log("[TurnManager] Dev solo mode active — local player has the turn.");
    }
#endif

    // ── SyncVar hook ───────────────────────────────────────────────────────────

    /// <summary>
    /// Fires on all clients (including host) when _currentSeat changes.
    /// Gates local input so only the active player can submit actions.
    /// </summary>
    private void OnCurrentSeatChanged(int _, int newSeat)
    
    {
        int localSeat = Player.LocalPlayer != null ? Player.LocalPlayer.SeatIndex : -1;
        bool isMyTurn = newSeat >= 0 && newSeat == localSeat;
        SelectionManager.Instance.IsPlayerTurn = isMyTurn;
        Debug.Log($"[TurnManager] Seat {newSeat}'s turn | local seat {localSeat} | myTurn={isMyTurn}");
    }
}
