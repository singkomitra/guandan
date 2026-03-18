using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles click-to-toggle selection for a hand card.
/// Drives CardHover's Y offset and adds a blue ring when selected.
/// Composes with CardHover and CardDrag — one responsibility each.
/// </summary>
[RequireComponent(typeof(Card))]
public class CardSelectable : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Color   _ringColor    = new Color(0.45f, 0.75f, 1f, 0.9f);
    [SerializeField] private Vector2 _ringDistance = new Vector2(6f, 6f);

    private Card      _card;
    private CardHover _hover;
    private Outline   _ring;

    private void Awake()
    {
        _card  = GetComponent<Card>();
        _hover = GetComponent<CardHover>();

        if (GetComponent<Image>() != null)
        {
            _ring = gameObject.AddComponent<Outline>();
            _ring.effectColor    = _ringColor;
            _ring.effectDistance = _ringDistance;
            _ring.enabled        = false;
        }
    }

    private void OnEnable()  => SelectionManager.Instance.SelectionChanged += OnSelectionChanged;
    private void OnDisable()
    {
        SelectionManager.Instance.SelectionChanged -= OnSelectionChanged;
        // Ensure visual state is cleared if component is disabled mid-selection.
        _hover?.SetSelected(false);
        if (_ring != null) _ring.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!SelectionManager.Instance.IsPlayerTurn) return;
        SelectionManager.Instance.Toggle(_card.Id);
    }

    private void OnSelectionChanged(IReadOnlyList<Card.CardId> staged)
    {
        bool isSelected = staged.Contains(_card.Id);
        _hover?.SetSelected(isSelected);
        if (_ring != null) _ring.enabled = isSelected;
    }
}
