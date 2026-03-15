using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Attach to HandViewPort. Requires a raycastable Image on the same GameObject.
public class HandDropZone : MonoBehaviour, IDropHandler
{
    public event Action<RectTransform, int> CardReturned;

    public void OnDrop(PointerEventData eventData)
    {
        var drag = eventData.pointerDrag?.GetComponent<CardDrag>();
        if (drag == null || !drag.IsFromHand) return;

        int targetIndex = drag.PlaceholderSiblingIndex;
        drag.CardRect.SetParent(drag.StartParent, true);
        drag.NotifyDropHandled();
        CardReturned?.Invoke(drag.CardRect, targetIndex);
    }
}
