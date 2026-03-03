using System;
using UnityEngine;
using UnityEngine.UI;

public class DiscardedCardView : MonoBehaviour
{
    public Image image;

    public void Set(CardData card)
    {
        image.sprite = card.discardedIcon;
    }
}