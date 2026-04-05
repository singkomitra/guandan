using Mirror;

/// <summary>
/// Mirror NetworkWriter/NetworkReader extensions for Card.CardId.
/// Mirror's weaver picks these up automatically — no manual registration needed.
/// Each CardId serializes as 3 bytes: suit, rank, deckIndex.
/// </summary>
public static class CardIdSerializer
{
    public static void WriteCardId(this NetworkWriter writer, Card.CardId id)
    {
        writer.WriteByte((byte)id.suit);
        writer.WriteByte((byte)id.rank);
        writer.WriteByte(id.deckIndex);
    }

    public static Card.CardId ReadCardId(this NetworkReader reader)
    {
        return new Card.CardId(
            (Card.Suit)reader.ReadByte(),
            (Card.Rank)reader.ReadByte(),
            reader.ReadByte()
        );
    }
}
