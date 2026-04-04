using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Server-authoritative deal manager. Lives as a scene NetworkBehaviour in GameScene.
///
/// Flow:
///   1. GuandanNetworkManager.OnServerReady calls OnConnectionReady() for each client.
///   2. Once every connected client is scene-ready, DealCards() runs once on the server.
///   3. Each player receives only their own hand via TargetRpc — no client sees another's cards.
///
/// </summary>
public class DealManager : NetworkBehaviour
{
    public static DealManager Instance { get; private set; }

    private bool _hasDealt;

    private void Awake()  => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>
    /// Called by GuandanNetworkManager.OnServerReady each time a client finishes loading
    /// the GameScene. Deals once all connected clients are ready.
    /// </summary>
    [Server]
    public void OnConnectionReady()
    {
        if (_hasDealt) return;

        foreach (var conn in NetworkServer.connections.Values)
            if (!conn.isReady) return; // at least one client not yet ready

        _hasDealt = true;
        DealCards();
    }

    [Server]
    private void DealCards()
    {
        var cardManager = FindFirstObjectByType<CardManager>();
        if (cardManager == null)
        {
            Debug.LogError("[DealManager] No CardManager found in GameScene.");
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

        #if !DEV_BUILD
            if (players.Count != 4 && players.Count != 6)
            {
                Debug.LogError($"[DealManager] Invalid player count ({players.Count}). Guandan requires 4 or 6 players.");
                return;
            }
        #endif

        players.Sort((a, b) =>
            a.connectionToClient.connectionId.CompareTo(b.connectionToClient.connectionId));

        int seed = Random.Range(1, int.MaxValue); // server owns the entropy
        var deck = cardManager.GetShuffledDoubleDeck(seed);

        if (deck.Count % players.Count != 0)
            Debug.LogWarning($"[DealManager] {deck.Count} cards not evenly divisible among {players.Count} players; {deck.Count % players.Count} card(s) will not be dealt.");

        int handSize = deck.Count / players.Count;
        Debug.Log($"[DealManager] Dealing {deck.Count} cards to {players.Count} players ({handSize} each, seed={seed})");

        for (int i = 0; i < players.Count; i++)
        {
            var hand = deck.GetRange(i * handSize, handSize).ToArray();
            TargetRpcReceiveHand(players[i].connectionToClient, hand);
        }
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
