using UnityEngine;
using Mirror;

public class Player : NetworkBehaviour
{
    public override void OnStartLocalPlayer()
    {
        // Called when this player object is spawned on the client that owns it
        Debug.Log("Player started: " + netId);
    }

    public override void OnStopClient()
    {
        // Called when the client disconnects
        Debug.Log("Player stopped: " + netId);
    }
}