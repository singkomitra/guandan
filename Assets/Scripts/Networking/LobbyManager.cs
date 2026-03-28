using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("UI")]
    public LobbyUI lobbyUI;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RefreshPlayerSlots()
    {
        if (lobbyUI != null)
            lobbyUI.UpdateSlots(GetAllPlayers());
    }

    [ClientRpc]
    public void RpcRefreshPlayerSlots()
    {
        RefreshPlayerSlots();
    }

    public static List<Player> GetAllPlayers()
    {
        var players = new List<Player>();
        foreach (var identity in NetworkClient.spawned.Values)
        {
            if (identity.TryGetComponent(out Player player))
                players.Add(player);
        }
        return players;
    }

    // Called by the Start Game button on the host client
    [Command(requiresAuthority = false)]
    public void CmdStartGame(NetworkConnectionToClient sender = null)
    {
        if (sender?.connectionId != 0)
        {
            Debug.LogWarning("[Server] Non-host attempted to start the game.");
            return;
        }

        if (!CanStartGame())
        {
            Debug.LogWarning("[Server] Cannot start game: not all players are ready.");
            return;
        }

        NetworkManager.singleton.ServerChangeScene("GameScene");
    }

    [Server]
    bool CanStartGame()
    {
        var players = GetAllPlayers();
        #if !DEV_BUILD
            if (players.Count != 4 && players.Count != 6) return false;
        #else
            if (players.Count < 1) return false;
        #endif
        foreach (var p in players)
            if (!p.isReady) return false;
        return true;
    }
}
