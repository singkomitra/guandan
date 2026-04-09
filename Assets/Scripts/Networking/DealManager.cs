using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Server-authoritative deal manager. Lives as a scene NetworkBehaviour in GameScene.
///
/// Flow:
///   1. GuandanNetworkManager.OnServerAddPlayer calls OnConnectionReady() for each client.
///   2. Once every connected client is scene-ready, DealCards() runs once on the server.
///   3. Each player receives only their own hand via TargetRpc — no client sees another's cards.
///
/// </summary>
public class DealManager : NetworkBehaviour
{
    public static DealManager Instance { get; private set; }

    [SerializeField] private CardManager _cardManager;

    [Header("Dev")]
    [Tooltip("Cards per player in editor and dev builds. 0 = use deck / player count.")]
    [SerializeField] private readonly int _devHandSize = 26;

    private bool _hasDealt;
    private int _readyCount;

    /// <summary>
    /// Server-side copy of each player's remaining hand, keyed by netId.
    /// Used by TurnManager to verify a player holds the cards they claim to play.
    /// </summary>
    private readonly Dictionary<uint, HashSet<Card.CardId>> _serverHands = new();

    private void Awake()  => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>
    /// Called via Player.CmdClientReady after OnStartLocalPlayer completes on each client.
    /// Guarantees Player.LocalPlayer is set on every client before dealing begins.
    /// Deals once all connected clients have confirmed ready.
    /// </summary>
    [Server]
    public void OnClientReady()
    {
        Debug.Log($"[DealManager] OnClientReady — _readyCount={_readyCount}, connections={NetworkServer.connections.Count}, _hasDealt={_hasDealt}");
        if (_hasDealt) return;

        _readyCount++;
        if (_readyCount < NetworkServer.connections.Count) return;

        _hasDealt = true;
        DealCards();
    }

    [Server]
    private void DealCards()
    {
        if (_cardManager == null)
        {
            Debug.LogError("[DealManager] CardManager is not assigned. Wire it in the Inspector.");
            return;
        }

        // Collect players ordered by connectionId for a deterministic deal order.
        var players = new List<Player>();
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity != null && conn.identity.TryGetComponent(out Player p))
                players.Add(p);
        }

        if (players.Count == 0)
        {
            Debug.LogError("[DealManager] No Player objects found — cannot deal.");
            return;
        }

        #if !DEV_BUILD && !UNITY_EDITOR
            if (players.Count != 4 && players.Count != 6)
            {
                Debug.LogError($"[DealManager] Invalid player count ({players.Count}). Guandan requires 4 or 6 players.");
                return;
            }
        #endif

        players.Sort((a, b) =>
            a.connectionToClient.connectionId.CompareTo(b.connectionToClient.connectionId));

        if (SeatingManager.Instance == null)
        {
            Debug.LogError("[DealManager] SeatingManager.Instance is null — aborting deal.");
            return;
        }
        SeatingManager.Instance.BuildSeating(players);

        int seed = Random.Range(1, int.MaxValue); // server owns the entropy
        var deck = _cardManager.GetShuffledDoubleDeck(seed);

        #if UNITY_EDITOR || DEV_BUILD
            int handSize = players.Count < 4 ? _devHandSize : deck.Count / players.Count;
        #else
            if (deck.Count % players.Count != 0)
                Debug.LogWarning($"[DealManager] {deck.Count} cards not evenly divisible among {players.Count} players; {deck.Count % players.Count} card(s) will not be dealt.");

            int handSize = deck.Count / players.Count;
        #endif
        Debug.Log($"[DealManager] Dealing {deck.Count} cards to {players.Count} players ({handSize} each, seed={seed})");

        _serverHands.Clear();
        for (int i = 0; i < players.Count; i++)
        {
            players[i].SeatIndex = i;
            var hand = deck.GetRange(i * handSize, handSize).ToArray();
            _serverHands[players[i].netId] = new HashSet<Card.CardId>(hand);
            TargetRpcReceiveHand(players[i].connectionToClient, hand);
        }

        // Seat 0 leads the first trick; trump rank defaults to Two until round scoring is implemented.
        if (TurnManager.Instance != null)
            TurnManager.Instance.StartGame(leadSeat: 0, trumpRank: Card.Rank.Two);
        else
            Debug.LogError("[DealManager] TurnManager.Instance is null — turn management will not start.");
    }

    /// <summary>
    /// Checks that <paramref name="netId"/> holds all <paramref name="cards"/>, then removes
    /// them from the server-side hand. Returns false (and removes nothing) if any card is missing.
    /// Called by TurnManager before accepting a play.
    /// </summary>
    [Server]
    public bool TryConsumeCards(uint netId, Card.CardId[] cards)
    {
        if (!_serverHands.TryGetValue(netId, out var hand)) return false;
        foreach (var card in cards)
            if (!hand.Contains(card)) return false;
        foreach (var card in cards)
            hand.Remove(card);
        return true;
    }

    /// <summary>Sends a player their hand. Only the targeted client receives this message.</summary>
    [TargetRpc]
    private void TargetRpcReceiveHand(NetworkConnectionToClient _, Card.CardId[] hand)
    {
        if (HandManager.Instance == null)
        {
            Debug.LogError("[DealManager] HandManager.Instance is null on client — cannot populate hand.");
            return;
        }
        HandManager.Instance.ReceiveHand(hand);
    }
}
