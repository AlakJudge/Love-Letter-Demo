using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class OnlineRoomsManager : MonoBehaviour
{
    public TMP_InputField playerNameInput;
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button backButton;

    // For storing player name in PlayerPrefs
    const string PlayerNameKey = "PlayerName";

    private void Start()
    {
        createRoomButton.onClick.AddListener(OnCreateClicked);
        joinRoomButton.onClick.AddListener(OnJoinClicked);
        backButton.onClick.AddListener(OnBackButtonClicked);

        // Load saved player name
        if (PlayerPrefs.HasKey(PlayerNameKey))
        {
            playerNameInput.text = PlayerPrefs.GetString(PlayerNameKey);
        }
    }

    private void Update()
    {
        var network = NetworkManager.Instance;
        if (network == null)
        {
            createRoomButton.interactable = false;
            joinRoomButton.interactable = false;
            return;
        }

        if (!PhotonNetwork.IsConnected)
        {
            createRoomButton.interactable = false;
            joinRoomButton.interactable = false;
        }
        else if (!PhotonNetwork.InLobby)
        {
            createRoomButton.interactable = false;
            joinRoomButton.interactable = false;
        }
        else
        {
            createRoomButton.interactable = true;
            joinRoomButton.interactable = true;
        }
    }

    private void ApplyPlayerName()
    {
        var name = playerNameInput.text.Trim();
        if (string.IsNullOrEmpty(name))
            name = string.Format("Player{0}", Random.Range(1, 1000));
        
        PlayerPrefs.SetString(PlayerNameKey, name);
        PlayerPrefs.Save();

        PhotonNetwork.NickName = name;

        // Custom property used by lobby script
        var props = new ExitGames.Client.Photon.Hashtable
        {
            ["displayName"] = name
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private void OnCreateClicked()
    {
        var network = NetworkManager.Instance;
        if (network == null) return;

        ApplyPlayerName();

        network.CreateRoom($"LoveLetterRoom_{Random.Range(0, 999)}");
    }

    private void OnJoinClicked()
    {
        var network = NetworkManager.Instance;
        if (network == null) return;

        ApplyPlayerName();
        PhotonNetwork.JoinRandomRoom();
    }

    private void OnBackButtonClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }
}
