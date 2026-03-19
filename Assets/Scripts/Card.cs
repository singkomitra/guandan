using UnityEngine;
using UnityEngine.UI;
using System;

public class Card : MonoBehaviour
{
    public enum Suit { Hearts, Spades, Diamonds, Clubs, Joker }
    public enum Rank { Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace, BlackJoker = 15, RedJoker = 16 }

    [Serializable]
    public struct CardId
    {
        public Suit suit;
        public Rank rank;
        public CardId(Suit s, Rank r) { suit = s; rank = r; }
        public override string ToString() =>
            rank == Rank.BlackJoker ? "Black Joker" :
            rank == Rank.RedJoker   ? "Red Joker"   :
            $"{rank} of {suit}";
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
