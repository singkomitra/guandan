using NUnit.Framework;

/// <summary>
/// Tests for SeatingManager's pure seating logic.
/// All tests call SeatingManager.BuildSeatingData directly — no scene or Mirror required.
/// </summary>
public class SeatingTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    static SeatingManager.PlayerSlot[] Slots(int count)
    {
        var slots = new SeatingManager.PlayerSlot[count];
        for (int i = 0; i < count; i++)
            slots[i] = new SeatingManager.PlayerSlot($"Player {i}", (uint)i);
        return slots;
    }

    // ── Team assignment — 4 players ───────────────────────────────────────────

    [Test]
    public void FourPlayers_Team0_HasSeats0And2()
    {
        var (_, teams, _) = SeatingManager.BuildSeatingData(Slots(4));
        Assert.AreEqual(2, teams[0].Players.Count);
        Assert.AreEqual(0, teams[0].Players[0].SeatIndex);
        Assert.AreEqual(2, teams[0].Players[1].SeatIndex);
    }

    [Test]
    public void FourPlayers_Team1_HasSeats1And3()
    {
        var (_, teams, _) = SeatingManager.BuildSeatingData(Slots(4));
        Assert.AreEqual(2, teams[1].Players.Count);
        Assert.AreEqual(1, teams[1].Players[0].SeatIndex);
        Assert.AreEqual(3, teams[1].Players[1].SeatIndex);
    }

    // ── Team assignment — 6 players ───────────────────────────────────────────

    [Test]
    public void SixPlayers_Team0_HasSeats0_2_4()
    {
        var (_, teams, _) = SeatingManager.BuildSeatingData(Slots(6));
        Assert.AreEqual(3, teams[0].Players.Count);
        Assert.AreEqual(0, teams[0].Players[0].SeatIndex);
        Assert.AreEqual(2, teams[0].Players[1].SeatIndex);
        Assert.AreEqual(4, teams[0].Players[2].SeatIndex);
    }

    [Test]
    public void SixPlayers_Team1_HasSeats1_3_5()
    {
        var (_, teams, _) = SeatingManager.BuildSeatingData(Slots(6));
        Assert.AreEqual(3, teams[1].Players.Count);
        Assert.AreEqual(1, teams[1].Players[0].SeatIndex);
        Assert.AreEqual(3, teams[1].Players[1].SeatIndex);
        Assert.AreEqual(5, teams[1].Players[2].SeatIndex);
    }

    // ── PlayerData fields ─────────────────────────────────────────────────────

    [Test]
    public void PlayerData_TeamIndex_EqualsSeatModTwo()
    {
        var (players, _, _) = SeatingManager.BuildSeatingData(Slots(6));
        foreach (var p in players)
            Assert.AreEqual(p.SeatIndex % 2, p.TeamIndex,
                $"Seat {p.SeatIndex} should be team {p.SeatIndex % 2}");
    }

    [Test]
    public void PlayerData_SeatIndex_MatchesPositionInArray()
    {
        var (players, _, _) = SeatingManager.BuildSeatingData(Slots(4));
        for (int i = 0; i < players.Length; i++)
            Assert.AreEqual(i, players[i].SeatIndex);
    }

    // ── Turn order ────────────────────────────────────────────────────────────

    [Test]
    public void TurnOrder_4Players_IsSequential()
    {
        var (_, _, order) = SeatingManager.BuildSeatingData(Slots(4));
        Assert.AreEqual(new[] { 0, 1, 2, 3 }, order);
    }

    [Test]
    public void TurnOrder_6Players_IsSequential()
    {
        var (_, _, order) = SeatingManager.BuildSeatingData(Slots(6));
        Assert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, order);
    }

    // ── NextSeat wrapping ─────────────────────────────────────────────────────

    [Test]
    public void NextSeat_4Players_WrapsAt3()
    {
        Assert.AreEqual(0, SeatingManager.NextSeat(3, 4));
    }

    [Test]
    public void NextSeat_6Players_WrapsAt5()
    {
        Assert.AreEqual(0, SeatingManager.NextSeat(5, 6));
    }

    [Test]
    public void NextSeat_MidTable_Increments()
    {
        Assert.AreEqual(2, SeatingManager.NextSeat(1, 4));
        Assert.AreEqual(3, SeatingManager.NextSeat(2, 6));
    }

    // ── TeamIndexForSeat ──────────────────────────────────────────────────────

    [Test]
    public void TeamIndexForSeat_AlternatesCorrectly()
    {
        for (int seat = 0; seat < 6; seat++)
            Assert.AreEqual(seat % 2, SeatingManager.TeamIndexForSeat(seat),
                $"Seat {seat} should map to team {seat % 2}");
    }
}
