using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds the canonical seat-to-player mapping for the current game session and
/// derives team membership and turn order from it.
///
/// Seating rule:
///   Players are seated clockwise in ascending connectionId order (lowest → seat 0).
///   <c>teamIndex = seatIndex % 2</c>, so seats {0, 2, 4} form team 0 and
///   {1, 3, 5} form team 1. Teammates always sit directly across from each other
///   and the two teams alternate clockwise around the table.
///
/// Turn order:
///   Seats are visited in ascending order: 0 → 1 → 2 → … → (N−1) → 0.
///   <see cref="NextSeat"/> wraps automatically.
///
/// Populated by <see cref="DealManager"/> (server-side) once all players have connected.
/// Must be present as a scene object in GameScene.
/// </summary>
public class SeatingManager : MonoBehaviour
{
    public static SeatingManager Instance { get; private set; }

    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>
    /// All players indexed by seat. <c>Players[i].SeatIndex == i</c> is always true.
    /// Empty until <see cref="BuildSeating"/> is called.
    /// </summary>
    public IReadOnlyList<PlayerData> Players { get; private set; } = System.Array.Empty<PlayerData>();

    /// <summary>
    /// Both teams. <c>Teams[0].TeamIndex == 0</c>, <c>Teams[1].TeamIndex == 1</c>.
    /// Empty until <see cref="BuildSeating"/> is called.
    /// </summary>
    public IReadOnlyList<TeamData> Teams { get; private set; } = System.Array.Empty<TeamData>();

    /// <summary>
    /// Seat indices in clockwise play order: [0, 1, 2, …, N−1].
    /// The round/turn manager decides which seat leads each trick.
    /// </summary>
    public IReadOnlyList<int> TurnOrder { get; private set; } = System.Array.Empty<int>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the seating arrangement from a list of <see cref="Player"/> objects
    /// already sorted by connectionId (exactly the ordering used in <see cref="DealManager"/>).
    /// The player at index 0 gets seat 0, index 1 gets seat 1, and so on.
    /// </summary>
    public void BuildSeating(IReadOnlyList<Player> orderedPlayers)
    {
        int n = orderedPlayers.Count;
        var players = new PlayerData[n];
        var teamLists = new List<PlayerData>[2] { new(), new() };

        for (int seat = 0; seat < n; seat++)
        {
            var p  = orderedPlayers[seat];
            var pd = new PlayerData(seat, p.playerName, p.netId);
            players[seat] = pd;
            teamLists[pd.TeamIndex].Add(pd);
        }

        Players = players;
        Teams = new TeamData[]
        {
            new TeamData(0, teamLists[0]),
            new TeamData(1, teamLists[1]),
        };

        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        TurnOrder = order;

        Debug.Log($"[SeatingManager] Seating built: {n} players | " +
                  $"Team 0 seats: [{string.Join(", ", teamLists[0].ConvertAll(p => p.SeatIndex.ToString()))}] | " +
                  $"Team 1 seats: [{string.Join(", ", teamLists[1].ConvertAll(p => p.SeatIndex.ToString()))}]");
    }

    /// <summary>
    /// Returns the seat index that acts after <paramref name="currentSeat"/>,
    /// wrapping from the last seat back to seat 0.
    /// </summary>
    public int NextSeat(int currentSeat) => (currentSeat + 1) % Players.Count;

    /// <summary>
    /// Returns the <see cref="PlayerData"/> at the given seat, or <c>null</c> if the
    /// index is out of range.
    /// </summary>
    public PlayerData GetPlayer(int seatIndex)
        => seatIndex >= 0 && seatIndex < Players.Count ? Players[seatIndex] : null;

    /// <summary>
    /// Returns the <see cref="TeamData"/> for the team that owns the given seat,
    /// or <c>null</c> if the seat index is out of range.
    /// </summary>
    public TeamData GetTeamForSeat(int seatIndex)
    {
        var player = GetPlayer(seatIndex);
        return player != null ? Teams[player.TeamIndex] : null;
    }

    /// <summary>
    /// Returns the team index for a seat without requiring an instance.
    /// Rule: <c>seatIndex % 2</c>.
    /// </summary>
    public static int TeamIndexForSeat(int seatIndex) => seatIndex % 2;
}
