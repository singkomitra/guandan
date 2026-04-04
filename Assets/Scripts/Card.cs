using UnityEngine;
using UnityEngine.UI;
using System;

public class Card : MonoBehaviour
{
    public enum Suit { Hearts, Spades, Diamonds, Clubs, Joker }
    public enum Rank { Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace, BlackJoker = 15, RedJoker = 16 }

    [Serializable]
    public struct CardId : IEquatable<CardId>
    {
        public Suit suit;
        public Rank rank;
        /// <summary>0 for the first deck, 1 for the second. Distinguishes duplicate cards in the double-deck.</summary>
        public byte deckIndex;

        public CardId(Suit s, Rank r, byte d = 0) { suit = s; rank = r; deckIndex = d; }

        public bool Equals(CardId other) =>
            suit == other.suit && rank == other.rank && deckIndex == other.deckIndex;

        public override bool Equals(object obj) => obj is CardId other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)suit, (int)rank, deckIndex);

        public override string ToString() =>
            rank == Rank.BlackJoker ? $"Black Joker (d{deckIndex})" :
            rank == Rank.RedJoker   ? $"Red Joker (d{deckIndex})"   :
            $"{rank} of {suit} (d{deckIndex})";
    }

    private Image _cardImage; // private, will be set dynamically
    public CardId Id { get; private set; }

    // Set the card's image and details at runtime
    public void SetupCard(Sprite sprite, CardId id)
    {
        Id = id;
        if (_cardImage == null)
            _cardImage = GetComponent<Image>();

        if (_cardImage != null)
            _cardImage.sprite = sprite;
        else
            Debug.LogError("No Image component found on this Card!");
    }
}
