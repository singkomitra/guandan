using System.Collections.Generic;

/// <summary>
/// Immutable data record for one team in a game session.
///
/// In a 4-player game each team has 2 members; in a 6-player game, 3 members.
/// Teammates always occupy seats that differ by 2 (directly across the table).
/// </summary>
public sealed class TeamData
{
    /// <summary>0 or 1. Equal to <c>seatIndex % 2</c> for any member of this team.</summary>
    public int TeamIndex { get; }

    /// <summary>All players on this team, ordered by ascending seat index.</summary>
    public IReadOnlyList<PlayerData> Players { get; }

    public TeamData(int teamIndex, IReadOnlyList<PlayerData> players)
    {
        TeamIndex = teamIndex;
        Players   = new System.Collections.ObjectModel.ReadOnlyCollection<PlayerData>(
                        new List<PlayerData>(players));
    }
}
