using UnityEngine;

public class DiscardPileView : MonoBehaviour
{
    public Transform container;
    public DiscardedCardView cardPrefab;

    public void UpdateDiscardPile(PlayerState player)
    {
        if (container == null || cardPrefab == null)
        {
            Debug.LogError($"DiscardPileView not configured.");
            return;
        }

        // Clear existing
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        // Render each discarded card as an icon
        foreach (var card in player.discardPile)
        {
            var view = Instantiate(cardPrefab, container);
            view.Set(card);
        }
    }
}
