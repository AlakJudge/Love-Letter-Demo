using System.Collections.Generic;
using UnityEngine;

public class PlayerState
{
    public readonly int id;
    public readonly List<CardData> hand = new();
    public readonly List<CardData> discardPile = new();
    public readonly HashSet<CardData> revealedCards = new();
    public bool isProtected;
    public bool isEliminated;
    public int tokens;

    public PlayerState(int id) { this.id = id; }

    public void DrawCard(Stack<CardData> deck)
    {
        if (deck.Count > 0)
        {
            var card = deck.Pop();
            hand.Add(card);
            Debug.Log($"Player {id + 1} drew {card.type}");
        }
        else
        {
            Debug.LogWarning($"Player {id + 1} cannot draw - deck is empty");
        }
    }
}