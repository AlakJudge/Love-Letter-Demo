using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HandView : MonoBehaviour
{
    public Transform container; 
    public CardView cardPrefab;

    public event Action<CardData> OnCardClicked;
    public event Action<CardData> OnCardLongPressed;
    public event Action<CardData> OnCardReleased;

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
            view.onLongPress = () => OnCardLongPressed?.Invoke(card);
            view.onLongPressRelease = () => OnCardReleased?.Invoke(card);
        }
    }
    public void ShowCardBack(PlayerState player, IReadOnlyCollection<int> revealedIndicesForViewer = null)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        int index = 0;

        foreach (var card in player.hand)
        {
            var objPrefab = card.uiPrefab != null ? card.uiPrefab : cardPrefab.gameObject;
            var view = Instantiate(objPrefab, container).GetComponent<CardView>();
            if (view == null)
            {
                Debug.LogError("CardView missing on instantiated prefab.");
                index++;
                continue;
            }

            bool isRevealed = revealedIndicesForViewer != null && revealedIndicesForViewer.Contains(index);

            // Show front if revealed, back otherwise
            if (isRevealed)
            {
                view.Set(card); // show revealed card
                view.isRevealed = true;
                // Allow it to be zoomed in
                view.onLongPress = () => OnCardLongPressed?.Invoke(card);
                view.onLongPressRelease = () => OnCardReleased?.Invoke(card);
            }
            else
            {
                view.ShowBack(card); // show back
                view.isRevealed = false;
            }
            index++;
        }
    }
    
    public CardView FindViewForCard(CardData card)
    {
        for (int i = 0; i < container.childCount; i++)
        {
            var view = container.GetChild(i).GetComponent<CardView>();
            if (view != null && view.boundCard == card)
                return view;
        }
        return null;
    }

}