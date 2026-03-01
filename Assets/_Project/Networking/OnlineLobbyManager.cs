using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using ExitGames.Client.Photon;
using UnityEngine.SceneManagement;

public class OnlineLobbyManager : MonoBehaviourPunCallbacks
{
    public OnlineLobbySlot[] slots = new OnlineLobbySlot[4];
    public Button startGameButton;
    public Button backButton;

    private enum SlotType
    {
        Empty = 0,
        Human = 1,
        Bot   = 2
    }
    
    private void Start()
    {
        // Listeners
        startGameButton.onClick.AddListener(OnStartGameButtonClicked);
        backButton.onClick.AddListener(OnBackButtonClicked);
        
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            // Ensure existing players are assigned to slots when host enters lobby
            AssignExistingPlayersToSlots();
        }

        RefreshSlots();
    }

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            AssignExistingPlayersToSlots();
        }
        RefreshSlots();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"{newPlayer.NickName} entered the room.");
        
        if (PhotonNetwork.IsMasterClient)
        {
            AssignPlayerToFreeSlot(newPlayer);
        }

        RefreshSlots();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"{otherPlayer.NickName} left the room.");

        if (PhotonNetwork.IsMasterClient)
        {
            ClearSlot(otherPlayer.ActorNumber);
        }

        RefreshSlots();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        RefreshSlots();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        // If this client was marked as kicked, leave room and go back to menu
        if (targetPlayer.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber &&
            changedProps.ContainsKey("kicked") &&
            changedProps["kicked"] is bool kicked && kicked)
        {
            PhotonNetwork.LeaveRoom();
            SceneManager.LoadScene("OnlineRoomsScene");
        }

        RefreshSlots();
    }

    private string TypeKey(int i) => $"slot{i}_type";
    private string PlayerKey(int i) => $"slot{i}_player";
    private string BotNameKey(int i) => $"slot{i}_botName";

    private void AssignExistingPlayersToSlots()
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            AssignPlayerToFreeSlot(player);
        }
    }

    private void AssignPlayerToFreeSlot(Player player)
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        var props = room.CustomProperties;

        // First, check if player is already assigned
        foreach (int i in System.Linq.Enumerable.Range(0, slots.Length))
        {
            if (props.TryGetValue(TypeKey(i), out var typeObj) &&
                (int)typeObj == (int)SlotType.Human && // If human slot
                props.TryGetValue(PlayerKey(i), out var playerObj) &&
                (int)playerObj == player.ActorNumber) // if assigned to a player
            {
                return; // already assigned
            }
        }

        // Find first empty slot
        for (int i = 0; i < slots.Length; i++)
        {
            int type = 0;
            if (props.TryGetValue(TypeKey(i), out var typeObj)) // confirm slot type exists
                type = (int)typeObj;

            if (type == (int)SlotType.Empty || !props.ContainsKey(TypeKey(i)))
            {
                var hashTable = new Hashtable
                {
                    [TypeKey(i)]  = (int)SlotType.Human,
                    [PlayerKey(i)] = player.ActorNumber
                };
                PhotonNetwork.CurrentRoom.SetCustomProperties(hashTable);
                Debug.Log($"Assigned player {player.NickName} to slot {i}");
                RefreshSlots();
                return;
            }
        }
        Debug.LogWarning("No free slot available to assign player.");
    }
    
    private void ClearSlot(int actorNumber)
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        var props = room.CustomProperties;

        for (int i = 0; i < slots.Length; i++)
        {
            if (props.TryGetValue(TypeKey(i), out var typeObj) &&
                (int)typeObj == (int)SlotType.Human &&
                props.TryGetValue(PlayerKey(i), out var playerObj) &&
                (int)playerObj == actorNumber)
            {
                var hashTable = new Hashtable
                {
                    [TypeKey(i)]  = (int)SlotType.Empty
                };
                hashTable.Remove(PlayerKey(i));
                hashTable.Remove(BotNameKey(i));

                PhotonNetwork.CurrentRoom.SetCustomProperties(hashTable);
                return;
            }
        }
    }
    
    public void AddBotToSlot(int slotIndex)
    {
        if (!PhotonNetwork.IsMasterClient) return; // Only host can add bots
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        var props = room.CustomProperties;

        int type = 0;
        if (props.TryGetValue(TypeKey(slotIndex), out var typeObj)) // confirm slot type exists
            type = (int)typeObj;

        if (type != (int)SlotType.Empty && props.ContainsKey(TypeKey(slotIndex))) 
            return; // slot not empty

        string botName = $"Bot {slotIndex + 1}";

        var hashTable = new Hashtable
        {
            [TypeKey(slotIndex)]    = (int)SlotType.Bot,
            [BotNameKey(slotIndex)] = botName
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(hashTable);
    }

    public void KickSlot(int slotIndex)
    {
        if (!PhotonNetwork.IsMasterClient) return; // Only host can kick
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        var props = room.CustomProperties;

        if (!props.TryGetValue(TypeKey(slotIndex), out var typeObj)) // confirm slot type exists
            return;

        var type = (SlotType)(int)typeObj;

        // Clear bot slot
        if (type == SlotType.Bot) // Kick bot
        {
            var hashTable = new Hashtable
            {
                [TypeKey(slotIndex)] = (int)SlotType.Empty
            };
            hashTable.Remove(BotNameKey(slotIndex));
            PhotonNetwork.CurrentRoom.SetCustomProperties(hashTable);
        }
        // Kick human
        else if (type == SlotType.Human &&
                 props.TryGetValue(PlayerKey(slotIndex), out var actorObj)) 
        {
            int actor = (int)actorObj;
            var player = FindPlayerByActor(actor);
            if (player != null && !PhotonNetwork.OfflineMode)
            {
                // Mark this player as kicked. They will handle leaving on their side
                var kickProps = new Hashtable { ["kicked"] = true };
                player.SetCustomProperties(kickProps);
            }
        }
    }

    private Player FindPlayerByActor(int playerActorNumber)
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p.ActorNumber == playerActorNumber)
                return p;
        }
        return null;
    }

    private void RefreshSlots()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        bool isHost = PhotonNetwork.IsMasterClient;
        var props = room.CustomProperties;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            int kind = (int)SlotType.Empty; // default to empty if not set
            if (props.TryGetValue(TypeKey(i), out var kindObj)) // confirm slot type exists
                kind = (int)kindObj;

            if (kind == (int)SlotType.Bot)
            {
                string botName = $"Bot{i + 1}"; // default bot name if not set
                if (props.TryGetValue(BotNameKey(i), out var nameObj))
                    botName = (string)nameObj;

                slot.SetBot(botName, isHost);
            }
            else if (kind == (int)SlotType.Human &&
                     props.TryGetValue(PlayerKey(i), out var actorObj))
            {
                int actor = (int)actorObj;
                var player = FindPlayerByActor(actor);

                string playerName = $"Player{actor}"; // default player name if not found
                if (player != null)
                {
                    // Prefer a custom "displayName" property, fallback to NickName
                    if (player.CustomProperties != null &&
                        player.CustomProperties.TryGetValue("displayName", out var displayNameObj))
                    {
                        playerName = (string)displayNameObj;
                    }
                    else
                    {
                        playerName = player.NickName;
                    }
                }
                // Check if this player is the local player to allow name editing
                bool isLocal = player != null &&
                player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;

                slot.SetHuman(playerName, isHost, isLocal);
            }
            else
            {
                slot.SetEmpty(isHost);
            }
        }

        // Start condition = at least 1 occupied human slot + host is present
        int occupiedCount = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (props.TryGetValue(TypeKey(i), out var typeObj))
            {
                int kind = (int)typeObj;
                if (kind == (int)SlotType.Human)
                    occupiedCount++;
            }
        }

        startGameButton.interactable = isHost && occupiedCount >= 2; // Require at least 2 human players to start
    }

    void OnStartGameButtonClicked()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        int seed = Random.Range(int.MinValue, int.MaxValue);
        var hashTable = new Hashtable { ["gameSeed"] = seed };
        PhotonNetwork.CurrentRoom.SetCustomProperties(hashTable);

        Debug.Log($"Starting game with {PhotonNetwork.CurrentRoom.PlayerCount} players. Game seed: {seed}");
        photonView.RPC("LoadGameScene", RpcTarget.All);
    }


    void OnBackButtonClicked()
    {
        // Leave the room and return to the online menu
        PhotonNetwork.LeaveRoom();
    }

    [PunRPC]
    public void LoadGameScene()
    {
        PhotonNetwork.LoadLevel("GameScene");
    }
}
