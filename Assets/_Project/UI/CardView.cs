using System;
using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    public Image image;
    public TMPro.TextMeshProUGUI label;
    public Button button;

    public Action onClick;

    public void Set(CardData card)
    {
        image.sprite = card.cardFront;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
    public void ShowBack(CardData card)
    {
        image.sprite = card.cardBack;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}