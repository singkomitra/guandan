using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Mirror;

public class LobbyUI : MonoBehaviour
{
    [Header("Player Slots")]
    // Assign 6 slot root GameObjects in the Inspector (top to bottom)
    public PlayerSlotUI[] playerSlots;

    [Header("Buttons")]
    public Button readyButton;
    public Button startButton;   // Host only
    public Button leaveButton;

    [Header("Session Info")]
    public TMP_Text sessionCodeText;

    void Start()
    {
        bool isHost = NetworkServer.active;
        startButton.gameObject.SetActive(isHost);

        readyButton.onClick.AddListener(OnReadyClicked);
        startButton.onClick.AddListener(OnStartClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);

        sessionCodeText.text = string.Empty;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RefreshPlayerSlots();
    }

    void OnReadyClicked()
    {
        var localPlayer = NetworkClient.localPlayer?.GetComponent<Player>();
        if (localPlayer == null) return;
        localPlayer.CmdSetReady(!localPlayer.isReady);
        EventSystem.current.SetSelectedGameObject(null);
    }

    void OnStartClicked()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.CmdStartGame();
    }

    void OnLeaveClicked()
    {
        if (NetworkClient.localPlayer != null &&
            NetworkClient.localPlayer.TryGetComponent(out Player localPlayer))
            localPlayer.CmdLeave();
        ((GuandanNetworkManager)NetworkManager.singleton).Stop();
    }

    // Called by LobbyManager whenever player state changes
    public void UpdateSlots(List<Player> players)
    {
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (i < players.Count)
                playerSlots[i].SetPlayer(players[i]);
            else
                playerSlots[i].SetEmpty();
        }

        if (startButton.gameObject.activeSelf)
        {
            int count = players.Count;
#if !DEV_BUILD
            bool validCount = count == 4 || count == 6;
#else
            bool validCount = count >= 1;
#endif
            bool allReady = validCount;
            foreach (var p in players)
                if (!p.isReady) { allReady = false; break; }
            startButton.interactable = allReady;
        }

        var local = NetworkClient.localPlayer?.GetComponent<Player>();
        if (local != null)
            readyButton.GetComponentInChildren<TMP_Text>().text = local.isReady ? "Unready" : "Ready";
    }

}
