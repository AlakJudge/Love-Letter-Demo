using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public static NetworkManager Instance { get; private set; }

    [Header("Photon")]
    public string gameVersion = "1.0";

    [Tooltip("Name for created rooms when auto-joining.")]
    public string roomNamePrefix = "GameRoom";

    public bool IsReadyForRooms => PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby;
    
    private void Awake()
    {
        if (Instance != null && Instance != this) // Avoid duplicate NetworkManagers
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Connect();
    }

    public void Connect()
    {
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("NetworkManager is already connected to Photon.");
            return;
        }

        Debug.Log("NetworkManager is connecting to Photon...");
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("NetworkManager connected to Master.");

        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("NetworkManager joined Lobby.");
    }

    public void CreateRoom(string roomName)
    {
        if (!IsReadyForRooms)
        {
            Debug.LogWarning("CreateRoom called while not in lobby yet.");
            return;
        }

        if (string.IsNullOrWhiteSpace(roomName))
        {
            roomName = $"{roomNamePrefix}_{Random.Range(0, 999)}";
        }

        var options = new RoomOptions
        {
            MaxPlayers = 4,
            IsVisible = true,
            IsOpen = true
        };

        PhotonNetwork.CreateRoom(roomName, options);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"CreateRoom failed ({returnCode}): {message}");
    }
    
    // If unable to join a room, create one instead.
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"NetworkManager JoinRandom failed ({returnCode}): {message}. Creating room...");

        string roomName = $"{roomNamePrefix}_{Random.Range(0, 999)}";
        var roomOptions = new RoomOptions
        {
            MaxPlayers = 4,
            IsVisible = true,
            IsOpen = true
        };

        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"NetworkManager joined room '{PhotonNetwork.CurrentRoom.Name}' as player #{PhotonNetwork.LocalPlayer.ActorNumber}.");

        SceneManager.LoadScene("OnlineLobbyScene");
        // TODO
        // - map Photon players to PlayerState ids
        // - trigger GameController to start the game when everyone is ready
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left room, loading OnlineRoomsScene.");
        SceneManager.LoadScene("OnlineRoomsScene");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"NetworkManager disconnected from Photon: {cause}");
        // TODO: Auto-reconnect and/or show a UI message
    }
}