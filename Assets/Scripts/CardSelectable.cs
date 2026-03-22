using System.Collections;
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

    [Header("Failure flash")]
    [SerializeField] private Color  _flashColor    = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private float  _flashHoldTime = 0.15f;
    [SerializeField] private float  _flashFadeTime = 0.3f;

    private Card      _card;
    private CardHover _hover;
    private Outline   _ring;
    private Image     _image;
    private Color     _baseColor;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        _card  = GetComponent<Card>();
        _hover = GetComponent<CardHover>();
        _image = GetComponent<Image>();
        _baseColor = _image != null ? _image.color : Color.white;

        if (_image != null)
        {
            _ring = gameObject.AddComponent<Outline>();
            _ring.effectColor    = _ringColor;
            _ring.effectDistance = _ringDistance;
            _ring.enabled        = false;
        }
    }

    private void OnEnable()
    {
        SelectionManager.Instance.SelectionChanged += OnSelectionChanged;
        SelectionManager.Instance.SelectionFailed  += OnSelectionFailed;
    }

    private void OnDisable()
    {
        SelectionManager.Instance.SelectionChanged -= OnSelectionChanged;
        SelectionManager.Instance.SelectionFailed  -= OnSelectionFailed;
        StopFlash();
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

    private void OnSelectionFailed(SetValidator.ValidationResult _)
    {
        if (!SelectionManager.Instance.Staged.Contains(_card.Id)) return;
        StopFlash();
        _flashRoutine = StartCoroutine(FlashRed());
    }

    private void StopFlash()
    {
        if (_flashRoutine == null) return;
        StopCoroutine(_flashRoutine);
        _flashRoutine = null;
        if (_image != null) _image.color = _baseColor;
    }

    private IEnumerator FlashRed()
    {
        if (_image == null) yield break;
        _image.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _baseColor.a);
        yield return new WaitForSeconds(_flashHoldTime);
        float t = 0f;
        while (t < _flashFadeTime)
        {
            t += Time.deltaTime;
            _image.color = Color.Lerp(
                new Color(_flashColor.r, _flashColor.g, _flashColor.b, _baseColor.a),
                _baseColor,
                t / _flashFadeTime);
            yield return null;
        }
        _image.color = _baseColor;
        _flashRoutine = null;
    }
}
