using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    // Load Game Scene
    public void StartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
    }
}
