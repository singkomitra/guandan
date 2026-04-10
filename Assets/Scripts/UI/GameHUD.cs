using UnityEngine;

/// <summary>
/// Bridges UI button events to game actions.
/// Attach to the HUD or ActionBar GameObject and wire buttons in the Inspector.
/// </summary>
public class GameHUD : MonoBehaviour
{
    public void OnPassClicked() => SelectionManager.Instance.Pass();

    public void OnPlayClicked() => SelectionManager.Instance.Commit();
}
