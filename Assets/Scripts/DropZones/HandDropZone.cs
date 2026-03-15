using UnityEngine;
using UnityEngine.EventSystems;

// Attach to HandViewPort (or HandView). Requires a raycastable Image on the same GameObject.
// Handles cards dropped back into the hand from anywhere.
public class HandDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        var drag = eventData.pointerDrag?.GetComponent<CardDrag>();
        if (drag == null || drag.IsFromTable) return;

        drag.ReturnToHand();
    }
}
