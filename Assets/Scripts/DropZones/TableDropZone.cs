using UnityEngine;
using UnityEngine.EventSystems;

// Attach to TableArea. Requires a raycastable Image on the same GameObject.
// Handles cards played from the hand onto the table.
public class TableDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        var drag = eventData.pointerDrag?.GetComponent<CardDrag>();
        if (drag == null || drag.IsFromTable) return;

        drag.PlayToTable();
    }
}
