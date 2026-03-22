using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Mirror;

public class JoinMenu : MonoBehaviour
{
    private GuandanNetworkManager networkManager;
    public TMP_InputField ipInputField; // Assign this in the Inspector

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
                networkManager.StartClient(address);
                SceneManager.LoadScene("GameScene");
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