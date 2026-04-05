using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Attach to the Backdrop Image. Dismisses the overlay on click.</summary>
public class BackdropClickHandler : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData _) => TrickHistoryOverlay.Hide();
}
