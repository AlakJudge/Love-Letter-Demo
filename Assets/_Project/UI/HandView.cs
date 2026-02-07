using System;
using UnityEngine;

public class HandView : MonoBehaviour
{
    public Transform container; 
    public CardView cardPrefab;

    public event Action<CardData> OnCardClicked;

    public void ShowHand(PlayerState player)
    {
        if (container == null || cardPrefab == null)
        {
            Debug.LogError($"HandView not configured. container={container}, cardPrefab={cardPrefab}");
            return;
        }

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        foreach (var card in player.hand)
        {
            var objPrefab = card.uiPrefab != null ? card.uiPrefab : cardPrefab.gameObject;
            var view = Instantiate(objPrefab, container).GetComponent<CardView>();
            if (view == null)
            {
                Debug.LogError("CardView missing on instantiated prefab.");
                continue;
            }
            view.Set(card);
            view.onClick = () => OnCardClicked?.Invoke(card);
        }
    }
    public void ShowCardBack(PlayerState player)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        foreach (var card in player.hand)
        {
            var objPrefab = card.uiPrefab != null ? card.uiPrefab : cardPrefab.gameObject;
            var view = Instantiate(objPrefab, container).GetComponent<CardView>();
            if (view == null)
            {
                Debug.LogError("CardView missing on instantiated prefab.");
                continue;
            }
            // Show front if revealed, back otherwise
            if (player.revealedCards.Contains(card))
                view.Set(card); // show revealed card
            else
                view.ShowBack(card); // show back

            //view.onClick = () => OnCardClicked?.Invoke(card);
        }
    }
}