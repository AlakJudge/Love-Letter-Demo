using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    // Load Game Scene
    public void StartSinglePlayerGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("SinglePlayerRoomScene");
    }

    public void StartOnlineLobby()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("OnlineRoomsScene");
    }
}
