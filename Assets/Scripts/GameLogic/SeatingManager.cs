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
///   <see cref="NextSeat(int, int)"/> wraps automatically.
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

    // ── Nested type ───────────────────────────────────────────────────────────

    /// <summary>Minimal player descriptor used by <see cref="BuildSeatingData"/>.</summary>
    public readonly struct PlayerSlot
    {
        public readonly string DisplayName;
        public readonly uint   NetId;
        public PlayerSlot(string displayName, uint netId) { DisplayName = displayName; NetId = netId; }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SeatingManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
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
        var slots = new PlayerSlot[orderedPlayers.Count];
        for (int i = 0; i < orderedPlayers.Count; i++)
            slots[i] = new PlayerSlot(orderedPlayers[i].playerName, orderedPlayers[i].netId);

        var (players, teams, order) = BuildSeatingData(slots);
        Players   = players;
        Teams     = teams;
        TurnOrder = order;

        Debug.Log($"[SeatingManager] Seating built: {players.Length} players | " +
                  $"Team 0 seats: [{SeatList(teams[0].Players)}] | " +
                  $"Team 1 seats: [{SeatList(teams[1].Players)}]");
    }

    /// <summary>
    /// Pure seating logic with no Unity or Mirror dependencies. Assigns seats, teams,
    /// and turn order from an ordered list of player descriptors.
    /// Exposed as <c>public static</c> so it can be called directly from tests.
    /// </summary>
    public static (PlayerData[] players, TeamData[] teams, int[] turnOrder)
        BuildSeatingData(IReadOnlyList<PlayerSlot> slots)
    {
        int n = slots.Count;
        var players   = new PlayerData[n];
        var teamLists = new List<PlayerData>[2] { new(), new() };

        for (int seat = 0; seat < n; seat++)
        {
            var pd = new PlayerData(seat, slots[seat].DisplayName, slots[seat].NetId);
            players[seat] = pd;
            teamLists[pd.TeamIndex].Add(pd);
        }

        var teams = new TeamData[]
        {
            new TeamData(0, teamLists[0]),
            new TeamData(1, teamLists[1]),
        };

        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;

        return (players, teams, order);
    }

    /// <summary>
    /// Returns the seat index that acts after <paramref name="currentSeat"/>,
    /// wrapping from the last seat back to seat 0.
    /// </summary>
    public int NextSeat(int currentSeat) => NextSeat(currentSeat, Players.Count);

    /// <summary>
    /// Stateless overload — usable without an instance and in tests.
    /// </summary>
    public static int NextSeat(int currentSeat, int playerCount) => (currentSeat + 1) % playerCount;

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

    private static string SeatList(IReadOnlyList<PlayerData> players)
    {
        var parts = new string[players.Count];
        for (int i = 0; i < players.Count; i++) parts[i] = players[i].SeatIndex.ToString();
        return string.Join(", ", parts);
    }
}
