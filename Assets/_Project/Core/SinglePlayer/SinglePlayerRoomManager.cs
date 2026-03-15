using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SinglePlayerRoomManager : MonoBehaviour
{
    public TMP_InputField playerNameInput;
    public Button createRoomButton;
    public Button backButton;
    
    // For storing player name in PlayerPrefs
    const string PlayerNameKey = "PlayerName";

    private void Start()
    {
        createRoomButton.onClick.AddListener(OnCreateClicked);
        backButton.onClick.AddListener(OnBackButtonClicked);

        // Load saved player name
        if (PlayerPrefs.HasKey(PlayerNameKey))
        {
            playerNameInput.text = PlayerPrefs.GetString(PlayerNameKey);
        }
    }

    private void OnCreateClicked()
    {
        ApplyPlayerName();
        UnityEngine.SceneManagement.SceneManager.LoadScene("SinglePlayerLobbyScene");}

    private void ApplyPlayerName()
    {
        var name = playerNameInput.text.Trim();
        if (string.IsNullOrEmpty(name))
            name = string.Format("Player{0}", Random.Range(1, 1000));
        
        PlayerPrefs.SetString(PlayerNameKey, name);
        PlayerPrefs.Save();
    }

    private void OnBackButtonClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }
}