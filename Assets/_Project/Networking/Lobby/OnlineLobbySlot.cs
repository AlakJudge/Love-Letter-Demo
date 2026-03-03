using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NUnit.Framework;

// This class represents a single slot in the online lobby UI, showing player name and icon.
// It will forward button clicks to LobbyManager and update its display based on the assigned player or bot.

public class OnlineLobbySlot : MonoBehaviour
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

    private bool isHost;
    private bool isEmpty;
    private bool isHuman;
    private bool isBot;
    private bool isLocalHuman;

    private void Awake()
    {
        if (addBotButton != null)
            addBotButton.onClick.AddListener(OnAddBotClicked);

        if (kickButton != null)
            kickButton.onClick.AddListener(OnKickClicked);
    }
    public void SetEmpty(bool isHost)
    {
        this.isHost = isHost;
        isEmpty = true;
        isHuman = false;
        isBot   = false;
        isLocalHuman = false;

        playerNameText.text = "Empty Slot";

        iconImage.sprite = addBotIcon;

        if (addBotButton != null)
            addBotButton.interactable = isHost;   // host can click to add bot

        if (kickButton != null)
            kickButton.gameObject.SetActive(false);
    }

    public void SetHuman(string playerName, bool isHost, bool isLocal)
    {
        this.isHost = isHost;
        isEmpty = false;
        isHuman = true;
        isBot   = false;
        isLocalHuman = isLocal;

        playerNameText.text = playerName;

        iconImage.sprite = HumanIcon;

        if (addBotButton != null)
            addBotButton.gameObject.SetActive(false);    // no add-bot click on occupied slot

        // host can kick any human/bot except self
        bool canKick = isHost && !isLocalHuman;
        if (kickButton != null)
            kickButton.gameObject.SetActive(canKick);
    }

    public void SetBot(string botName, bool isHost)
    {
        this.isHost = isHost;
        isEmpty = false;
        isHuman = false;
        isBot   = true;
        isLocalHuman = false;

        playerNameText.text = botName;

        iconImage.sprite = botIcon;

        if (addBotButton != null)
            addBotButton.gameObject.SetActive(false);

        if (kickButton != null)
            kickButton.gameObject.SetActive(isHost); // host can kick bots
    }

    private void OnAddBotClicked()
    {
        if (!isHost || !isEmpty)
            return;

        var lobby = FindFirstObjectByType<OnlineLobbyManager>();

        if (lobby != null)
            lobby.AddBotToSlot(slotIndex);
    }

    private void OnKickClicked()
    {
        if (!isHost)
            return;

        var lobby = FindFirstObjectByType<OnlineLobbyManager>();
        if (lobby != null)
            lobby.KickSlot(slotIndex);
    }
}
