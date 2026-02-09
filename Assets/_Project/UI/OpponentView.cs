using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class OpponentView : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI nameLabel;
    public HandView handView;
    public DiscardPileView discardPileView;
    public Button targetButton;

    [Header("Containers")]
    public Transform tokensContainer;
    public Transform statusContainer;

    [Header("Prefabs")]
    public GameObject protectedStatusPrefab;
    public GameObject eliminatedStatusPrefab;
    public GameObject activeStatusPrefab;
    public GameObject tokenPrefab;

    public event Action<int> OnTargetSelected;
    
    private PlayerState player;
    private bool showHand;
    private Image buttonImage; 

    private void Awake()
    {
        if (targetButton != null)
        {
            buttonImage = targetButton.GetComponent<Image>();
            targetButton.onClick.AddListener(() => OnTargetSelected?.Invoke(GetPlayerId()));
            targetButton.interactable = false;
        }

        // Raycast disabled by default so it doesn't block card clicks
        if (buttonImage != null)
            buttonImage.raycastTarget = false;
    }

    public void Bind(PlayerState player, string displayName, bool showHand)
    {
        this.player = player;
        this.showHand = showHand;
        nameLabel.text = displayName;
        Refresh();
    }
    public int GetPlayerId() => player?.id ?? -1;
    
    public void SetTargetable(bool targetable)
    {
        if (targetButton != null)
            targetButton.interactable = targetable;

            // Only allow player targetting during targetting phase
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
        
        if (showHand)
            handView?.ShowHand(player); // for debugging
        else
            handView?.ShowCardBack(player); // shows revealed cards as front, others as back
    }

    private void RefreshStatus()
    {
        if (statusContainer == null) return;

        // Clear existing status
        for (int i = statusContainer.childCount - 1; i >= 0; i--)
            Destroy(statusContainer.GetChild(i).gameObject);

        // Instantiate status indicators
        if (player.isProtected)
            Instantiate(protectedStatusPrefab, statusContainer);
        else if (player.isEliminated)
            Instantiate(eliminatedStatusPrefab, statusContainer);
        else
            Instantiate(activeStatusPrefab, statusContainer);
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