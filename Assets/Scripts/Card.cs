using UnityEngine;
using UnityEngine.UI;
using TMPro; // For text

public class Card : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMP_Text _titleText;

    // Set card info at runtime
    public void SetupCard(Sprite icon, string title)
    {
        _iconImage.sprite = icon;
        _titleText.text = title;
    }
}
