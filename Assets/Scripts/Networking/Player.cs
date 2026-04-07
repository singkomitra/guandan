using UnityEngine;
using Mirror;

public class Player : NetworkBehaviour
{
    /// <summary>The local player's Player instance. Set in OnStartLocalPlayer.</summary>
    public static Player LocalPlayer { get; private set; }

    [SyncVar] public string playerName;
    [SyncVar(hook = nameof(OnReadyChanged))] public bool isReady;
    [SyncVar] public bool isHost;
    /// <summary>Clockwise seat index assigned by DealManager. -1 until seating is built.</summary>
    [SyncVar] public int SeatIndex = -1;

    public override void OnStartServer()
    {
        isHost = connectionToClient.connectionId == 0;
        playerName = $"Player {NetworkServer.connections.Count}";
    }

    public override void OnStartClient()
    {
        if (LobbyManager.Instance != null) LobbyManager.Instance.RefreshPlayerSlots();
    }

    public override void OnStopClient()
    {
        if (LobbyManager.Instance != null) LobbyManager.Instance.RefreshPlayerSlots();
    }

    public override void OnStartLocalPlayer()
    {
        LocalPlayer = this;
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

    /// <summary>Sends the local player's card play to the server for validation and broadcast.</summary>
    [Command]
    public void CmdPlayCards(Card.CardId[] cards)
    {
        if (TurnManager.Instance != null) TurnManager.Instance.ServerHandlePlay(this, cards);
    }

    /// <summary>Sends the local player's pass to the server.</summary>
    [Command]
    public void CmdPass()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.ServerHandlePass(this);
    }

    void OnReadyChanged(bool _, bool __)
    {
        if (LobbyManager.Instance != null) LobbyManager.Instance.RefreshPlayerSlots();
    }
}
