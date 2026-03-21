using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

// Attach to TableArea. Requires a raycastable Image on the same GameObject.
// Handles cards played from the hand onto the table.
public class TableDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        var drag = eventData.pointerDrag?.GetComponent<CardDrag>();
        if (drag == null || !drag.IsFromHand) return;

        // Stage the dragged card if it wasn't already part of the selection.
        var card = drag.GetComponent<Card>();
        if (card != null && !SelectionManager.Instance.Staged.Contains(card.Id))
            SelectionManager.Instance.Toggle(card.Id);

        // Validate and commit. Only mark the drop as handled on success so that
        // CardDrag's OnEndDrag returns the card to hand when validation fails.
        bool committed = SelectionManager.Instance.Commit();
        if (committed) drag.NotifyDropHandled();
    }
}
