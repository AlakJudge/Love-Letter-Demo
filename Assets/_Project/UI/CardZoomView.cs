using UnityEngine;
using UnityEngine.UI;

public class CardZoomView : MonoBehaviour
{
    public Image artwork;

    public void Show(CardData card)
    {
        if (card == null) 
            return;
        
        
        if (artwork != null)
            artwork.sprite = card.cardFront;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
