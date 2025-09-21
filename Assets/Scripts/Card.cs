using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    private Image _cardImage; // private, will be set dynamically

    // Set the card's image at runtime
    public void SetupCard(Sprite sprite)
    {
        // Lazy grab Image if not assigned
        if (_cardImage == null)
            _cardImage = GetComponent<Image>();

        if (_cardImage != null)
            _cardImage.sprite = sprite;
        else
            Debug.LogError("No Image component found on this Card!");
    }
}
