using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public class MainMenu : MonoBehaviour
{
    private GuandanNetworkManager networkManager;

    private void Start()
    {
        networkManager = FindFirstObjectByType<GuandanNetworkManager>();
    }

    public void PlayGame()
    {
        SceneManager.LoadScene("GameScene");
    }

    public void HostGame()
    {
        if (networkManager != null)
        {
            networkManager.StartHost();
            SceneManager.LoadScene("GameScene");
        }
    }

    public void GoToJoinMenu()
    {
        SceneManager.LoadScene("JoinMenu");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }
}
