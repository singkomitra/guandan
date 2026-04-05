/// <summary>
/// Immutable data record for one player in a game session.
///
/// Seat index is determined by connection order: the player with the lowest Mirror
/// connectionId gets seat 0, the next gets seat 1, and so on clockwise.
/// Team membership is always <c>seatIndex % 2</c>, so seats {0, 2, 4} form team 0
/// and seats {1, 3, 5} form team 1, guaranteeing teammates sit directly across from
/// each other and the two teams alternate clockwise around the table.
/// </summary>
public sealed class PlayerData
{
    /// <summary>Clockwise seat index, 0-based. Seat 0 is the first player to connect.</summary>
    public int SeatIndex { get; }

    /// <summary>
    /// Team this player belongs to. Always <c>SeatIndex % 2</c>.
    /// Team 0 holds seats 0, 2, 4; team 1 holds seats 1, 3, 5.
    /// </summary>
    public int TeamIndex { get; }

    /// <summary>Display name, sourced from <see cref="Player.playerName"/>.</summary>
    public string DisplayName { get; }

    /// <summary>Mirror network ID. Links back to the <see cref="Player"/> NetworkBehaviour.</summary>
    public uint NetId { get; }

    public PlayerData(int seatIndex, string displayName, uint netId)
    {
        SeatIndex   = seatIndex;
        TeamIndex   = seatIndex % 2;
        DisplayName = displayName;
        NetId       = netId;
    }
}
