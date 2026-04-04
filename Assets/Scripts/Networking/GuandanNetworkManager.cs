using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public class GuandanNetworkManager : NetworkManager
{
    // SERVER-SIDE: logs appear in the host's console
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log($"[Server] Client connected — connId={conn.connectionId}, total={NetworkServer.connections.Count}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        Debug.Log($"[Server] Client disconnected — connId={conn.connectionId}, total={NetworkServer.connections.Count - 1}");
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RpcRefreshPlayerSlots();
    }

    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);
        Debug.Log($"[Server] Client ready — connId={conn.connectionId}");

        if (SceneManager.GetActiveScene().name == "GameScene")
            DealManager.Instance?.OnConnectionReady();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        Debug.Log($"[Server] Player spawned for connId={conn.connectionId}");
    }

    // CLIENT-SIDE: logs appear in the joining client's console
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[Client] Connected to server");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("[Client] Disconnected from server");
    }

    // Method to start as client
    public void StartClient(string address)
    {
        networkAddress = address;
        base.StartClient();
    }

    // Method to stop
    public void Stop()
    {
        if (mode == NetworkManagerMode.Host)        StopHost();
        else if (mode == NetworkManagerMode.ClientOnly) StopClient();
        else if (mode == NetworkManagerMode.ServerOnly)  StopServer();
    }
}