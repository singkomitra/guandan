using UnityEngine;
using Mirror;

public class GuandanNetworkManager : NetworkManager
{
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log("Client connected: " + conn.connectionId);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        Debug.Log("Client disconnected: " + conn.connectionId);
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("Connected to server");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("Disconnected from server");
    }

    // Method to start as host
    public void StartHost()
    {
        base.StartHost();
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
        base.StopHost();
        base.StopClient();
    }
}