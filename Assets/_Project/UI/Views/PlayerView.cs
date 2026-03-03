using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class PlayerView : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI nameLabel;
    public HandView handView;
    public DiscardPileView discardPileView;
    public Button discardPileExpandButtonPlayer;
    public Button targetButton;
    

    [Header("Containers")]
    public Transform tokensContainer;
    public Transform statusContainer;

    [Header("Prefabs")]
    public GameObject protectedStatusPrefab;
    public GameObject eliminatedStatusPrefab;
    public GameObject tokenPrefab;
    
    public event Action<int> OnTargetSelected;
    public event Action OnDiscardPileExpanded;

    private PlayerState player;
    private Image buttonImage;

    private void Awake()
    {
        if (targetButton != null)
        {
            buttonImage = targetButton.GetComponent<Image>();
            targetButton.onClick.AddListener(() => OnTargetSelected?.Invoke(player.id));
            targetButton.interactable = false;

            if (buttonImage != null)
                buttonImage.raycastTarget = false;
        }

        // Hook up discard pile expand buttons to show zoom view with local player's discard pile
        if (discardPileExpandButtonPlayer != null)
            discardPileExpandButtonPlayer.onClick.AddListener(() => 
            {
                OnDiscardPileExpanded?.Invoke();
            });
    }

    public void Bind(PlayerState player, string displayName)
    {
        this.player = player;
        nameLabel.text = displayName;
        Refresh();
    }

    public void SetTargetable(bool targetable)
    {
        if (targetButton != null)
            targetButton.interactable = targetable;

        if (buttonImage != null)
            buttonImage.raycastTarget = targetable;
    }

    public void Refresh()
    {
        if (player == null) return;
        
        RefreshTokens();
        RefreshStatus();
        
        // Discard pile (icons)
        if (discardPileView != null)
            discardPileView.UpdateDiscardPile(player);     
    }

    private void RefreshStatus()
    {
        if (statusContainer == null) return;

        // Clear existing status indicators
        for (int i = statusContainer.childCount - 1; i >= 0; i--)
            Destroy(statusContainer.GetChild(i).gameObject);

        // Show protected/eliminated status
        if (player.isProtected)
            Instantiate(protectedStatusPrefab, statusContainer);
        else if (player.isEliminated)
            Instantiate(eliminatedStatusPrefab, statusContainer);
    }

    private void RefreshTokens()
    {
        if (tokensContainer == null || tokenPrefab == null) return;

        // Clear existing tokens
        for (int i = tokensContainer.childCount - 1; i >= 0; i--)
            Destroy(tokensContainer.GetChild(i).gameObject);

        // Instantiate token sprites
        for (int i = 0; i < player.tokens; i++)
        {
            Instantiate(tokenPrefab, tokensContainer);
        }
    }  

    public void SetName(string displayName) => nameLabel.text = displayName;
}