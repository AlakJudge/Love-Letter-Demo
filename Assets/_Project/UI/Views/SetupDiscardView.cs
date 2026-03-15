using System.Collections.Generic;
using UnityEngine;

public class SetupDiscardView : MonoBehaviour
{
    public Transform container;
    public DiscardedCardView cardPrefab;

    public void UpdateSetupDiscards(IReadOnlyList<CardData> cards)
    {
        if (container == null || cardPrefab == null)
        {
            Debug.LogError("SetupDiscardView not configured.");
            return;
        }

        // Clear existing
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        // Render each setup‑discarded card as an icon
        foreach (var card in cards)
        {
            var view = Instantiate(cardPrefab, container);
            view.Set(card);
        }
    }

    public void Clear()
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }
}