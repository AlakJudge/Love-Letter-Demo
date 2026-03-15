using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SinglePlayerLobbyManager : MonoBehaviour
{
    public SinglePlayerLobbySlot[] slots = new SinglePlayerLobbySlot[4];
    public Button startGameButton;
    public Button backButton;
    private SlotType[] slotTypes = new SlotType[4];
    private string[] botNames = new string[4];

    private enum SlotType
    {
        Empty = 0,
        Human = 1,
        Bot = 2
    }

    private void Start()
    {
        // Listeners
        startGameButton.onClick.AddListener(OnStartGameButtonClicked);
        backButton.onClick.AddListener(OnBackButtonClicked);

        // Slot 0 is always the human host
        slotTypes[0] = SlotType.Human;
        for (int i = 1; i < slotTypes.Length; i++)
        {
            slotTypes[i] = SlotType.Empty;
            botNames[i] = null;
        }
        RefreshSlots();
    }
    
    public void AddBotToSlot(int slotIndex)
    {
        if (slotIndex <= 0 || slotIndex >= slotTypes.Length) return; // keep 0 as human

        if (slotTypes[slotIndex] != SlotType.Empty) return;

        slotTypes[slotIndex] = SlotType.Bot;
        botNames[slotIndex] = $"Bot {slotIndex}";
        RefreshSlots();
    }

    public void KickSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotTypes.Length) return;

        if (slotTypes[slotIndex] == SlotType.Bot)
        {
            slotTypes[slotIndex] = SlotType.Empty;
            botNames[slotIndex] = null;
            slots[slotIndex].SetEmpty();
            RefreshSlots();
        }
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            switch (slotTypes[i])
            {
                case SlotType.Human:
                    // In single-player, only slot 0 is human
                    string playerName = PlayerPrefs.GetString("PlayerName", $"Player{Random.Range(1, 1000)}");
                    slot.SetHuman(playerName);
                    break;

                case SlotType.Bot:
                    string botName = string.IsNullOrEmpty(botNames[i])
                        ? $"Bot {i + 1}"
                        : botNames[i];
                    slot.SetBot(botName);
                    break;

                default:
                    slot.SetEmpty();
                    break;
            }
        }

        // Enable Start when we have at least 2 players (human + >=1 bot)
        int totalPlayers = slotTypes.Count(t => t == SlotType.Human || t == SlotType.Bot);
        startGameButton.interactable = totalPlayers >= 2;
    }

    void OnStartGameButtonClicked()
    {
        int seed = Random.Range(int.MinValue, int.MaxValue);
        PlayerPrefs.SetInt("SP_Seed", seed);
        Debug.Log($"Starting game with seed {seed}");

        // Save slot layout
        for (int i = 0; i < slotTypes.Length; i++)
        {
            PlayerPrefs.SetInt($"SP_Slot_{i}_Type", (int)slotTypes[i]);

            if (slotTypes[i] == SlotType.Bot)
            {
                string botName = botNames[i];
                PlayerPrefs.SetString($"SP_Slot_{i}_BotName", botName);
            }
        }

        int totalPlayers = slotTypes.Count(t => t == SlotType.Human || t == SlotType.Bot);
        PlayerPrefs.SetInt("SP_PlayerCount", totalPlayers);

        PlayerPrefs.Save();

        UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
    }

    void OnBackButtonClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }
}
