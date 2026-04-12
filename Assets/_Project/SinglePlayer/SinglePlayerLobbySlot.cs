using UnityEngine;
using UnityEngine.UI;
using TMPro;

// This class represents a single slot in the online lobby UI, showing player name and icon.
// It will forward button clicks to LobbyManager and update its display based on the assigned player or bot.

public class SinglePlayerLobbySlot : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text playerNameText;
    public Image iconImage;
    public Sprite HumanIcon;
    public Sprite botIcon;
    public Sprite addBotIcon;

    public Button addBotButton;
    public Button kickButton;
    
    [Header("Config")]
    public int slotIndex;


    private void Awake()
    {
        if (addBotButton != null)
            addBotButton.onClick.AddListener(OnAddBotClicked);

        if (kickButton != null)
            kickButton.onClick.AddListener(OnKickClicked);
    }
    public void SetEmpty()
    {
        playerNameText.text = "Empty Slot";

        iconImage.sprite = addBotIcon;

        if (kickButton != null)
            kickButton.gameObject.SetActive(false);

        if (addBotButton != null)
            addBotButton.gameObject.SetActive(true);
    }

    public void SetHuman(string playerName)
    {
        playerNameText.text = playerName;

        iconImage.sprite = HumanIcon;

        if (addBotButton != null)
            addBotButton.gameObject.SetActive(false); // no add-bot click on occupied slot

        if (kickButton != null)
            kickButton.gameObject.SetActive(false);
    }

    public void SetBot(string botName)
    {
        playerNameText.text = botName;
        iconImage.sprite = botIcon;

        if (addBotButton != null)
            addBotButton.gameObject.SetActive(false);

        if (kickButton != null)
            kickButton.gameObject.SetActive(true);
    }

    private void OnAddBotClicked()
    {
        var lobby = FindFirstObjectByType<SinglePlayerLobbyManager>();

        if (lobby != null)
            lobby.AddBotToSlot(slotIndex);
    }

    private void OnKickClicked()
    {
        var lobby = FindFirstObjectByType<SinglePlayerLobbyManager>();
        if (lobby != null)
            lobby.KickSlot(slotIndex);
    }
}
