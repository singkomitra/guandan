using UnityEngine;
using Mirror;

public class Player : NetworkBehaviour
{
    [SyncVar] public string playerName;
    [SyncVar(hook = nameof(OnReadyChanged))] public bool isReady;
    [SyncVar] public bool isHost;

    public override void OnStartServer()
    {
        isHost = connectionToClient.connectionId == 0;
        playerName = $"Player {connectionToClient.connectionId + 1}";
    }

    public override void OnStartClient()
    {
        LobbyManager.Instance?.RefreshPlayerSlots();
    }

    public override void OnStopClient()
    {
        LobbyManager.Instance?.RefreshPlayerSlots();
    }

    public override void OnStartLocalPlayer()
    {
        Debug.Log($"[Client] Local player started: netId={netId}");
    }

    [Command]
    public void CmdSetReady(bool ready) => isReady = ready;

    [Command]
    public void CmdLeave()
    {
        Debug.Log($"[Server] Player {playerName} (connId={connectionToClient.connectionId}) is leaving.");
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RefreshPlayerSlots();
    }

    void OnReadyChanged(bool _, bool __)
        => LobbyManager.Instance?.RefreshPlayerSlots();
}
