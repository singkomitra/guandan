using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Attach to TableArea. Requires a raycastable Image on the same GameObject.
// Handles cards played from the hand onto the table.
public class TableDropZone : MonoBehaviour, IDropHandler
{
    public event Action<RectTransform> CardPlayed;

    public void OnDrop(PointerEventData eventData)
    {
        var drag = eventData.pointerDrag?.GetComponent<CardDrag>();
        if (drag == null || !drag.IsFromHand) return;

        drag.NotifyDropHandled();
        CardPlayed?.Invoke(drag.CardRect);
    }
}
