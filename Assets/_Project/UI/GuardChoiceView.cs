using System;
using UnityEngine;

public class GuardChoiceView : MonoBehaviour
{
    [Header("Card Options")]
    public CardData Spy;
    public CardData Baron;
    public CardData Handmaid;
    public CardData Prince;
    public CardData King;
    public CardData Countess;
    public CardData Princess;

    [Header("UI")]
    public Transform cardContainer;
    public CardView cardPrefab;

    public event Action<int> OnGuessSelected;

    public void Show()
    {
        gameObject.SetActive(true);
        BuildCards();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        ClearCards();
    }

    private void BuildCards()
    {
        ClearCards();

        CardData[] options = { Spy, Baron, Handmaid, Prince, King, Countess, Princess };

        foreach (var card in options)
        {
            if (card == null) continue;

            var view = Instantiate(cardPrefab, cardContainer);
            view.Set(card);
            view.onClick = () =>
            {
                OnGuessSelected?.Invoke(card.cardValue);
                Hide();
            };
        }
    }

    private void ClearCards()
    {
        if (cardContainer == null) return;
        for (int i = cardContainer.childCount - 1; i >= 0; i--)
            Destroy(cardContainer.GetChild(i).gameObject);
    }
}
