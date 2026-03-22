using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Attach to each of the 4 player slot GameObjects in the LobbyScene
public class PlayerSlotUI : MonoBehaviour
{
    public TMP_Text nameText;
    public Image readyIndicator;   // Green = ready, grey = not ready
    public Color readyColor = Color.green;
    public Color notReadyColor = Color.gray;

    public void SetPlayer(Player player)
    {
        nameText.text = player.isHost ? $"{player.playerName} (Host)" : player.playerName;
        readyIndicator.color = player.isReady ? readyColor : notReadyColor;
    }

    public void SetEmpty()
    {
        nameText.text = "Waiting for player...";
        readyIndicator.color = notReadyColor;
    }
}
