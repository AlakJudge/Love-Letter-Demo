using UnityEngine;

public class DiscardPileZoomView : MonoBehaviour
{
    [Header("UI")]
    public Transform container;
    public CardView cardPrefab;

    public void Show(PlayerState player)
    {
        if (player == null || container == null || cardPrefab == null)
        {
            Debug.LogError("DiscardPileZoomView not configured correctly.");
            return;
        }

        // Clear old cards
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        // Add one full-size CardView per discard card
        foreach (var card in player.discardPile)
        {
            var view = Instantiate(cardPrefab, container);
            view.Set(card);
            // No interaction from here
            view.onClick            = null;
            view.onLongPress        = null;
            view.onLongPressRelease = null;
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
