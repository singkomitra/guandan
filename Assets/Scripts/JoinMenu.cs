using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Mirror;
using kcp2k;

public class JoinMenu : MonoBehaviour
{
    private GuandanNetworkManager networkManager;
    public TMP_InputField ipInputField;
    public TMP_InputField portInputField;

    private void Start()
    {
        networkManager = FindFirstObjectByType<GuandanNetworkManager>();
    }

    public void ConnectToGame()
    {
        if (networkManager != null && ipInputField != null)
        {
            string address = ipInputField.text;
            if (!string.IsNullOrEmpty(address))
            {
                if (portInputField != null &&
                    ushort.TryParse(portInputField.text, out ushort port) &&
                    Transport.active is KcpTransport kcp)
                {
                    kcp.port = port;
                }

                networkManager.onlineScene = "LobbyScene";
                networkManager.StartClient(address);
            }
            else
            {
                Debug.LogWarning("Please enter a valid IP address.");
            }
        }
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}