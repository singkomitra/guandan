using UnityEngine;

public class CardManager : MonoBehaviour
{
    [SerializeField] private GameObject _cardPrefab;
    [SerializeField] private Transform _cardParent;

    // Customize spacing and fan angle
    [SerializeField] private float _spacing = 100f;       // horizontal distance between cards
    [SerializeField] private float _fanAngle = 5f;        // rotation per card

    private void Start()
    {
        string[] suits = { "Hearts", "Spades", "Diamonds", "Clubs" };

        int cardIndex = 0; // keeps track of total cards for positioning

        foreach (string suit in suits)
        {
            Sprite[] suitSprites = Resources.LoadAll<Sprite>($"Cards/{suit}");

            foreach (Sprite sprite in suitSprites)
            {
                GameObject cardObj = Instantiate(_cardPrefab, _cardParent);
                Card cardScript = cardObj.GetComponent<Card>();
                cardScript.SetupCard(sprite);

                RectTransform rt = cardObj.GetComponent<RectTransform>();

                // Calculate fan position (centered around parent)
                float totalCards = suitSprites.Length * suits.Length;
                float startX = -((totalCards - 1) / 2f) * _spacing; // start position so cards are centered
                rt.anchoredPosition = new Vector2(startX + cardIndex * _spacing, 0);

                // Apply rotation for fan effect
                float startAngle = -((totalCards - 1) / 2f) * _fanAngle;
                rt.localRotation = Quaternion.Euler(0, 0, startAngle + cardIndex * _fanAngle);

                cardIndex++;
            }
        }
    }
}
