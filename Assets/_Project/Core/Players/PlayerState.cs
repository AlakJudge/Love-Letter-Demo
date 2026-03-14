using System.Collections.Generic;
using UnityEngine;

public class PlayerState
{
    public readonly int id;
    public string name;
    public int actorNumber;
    public bool isBot;
    public readonly List<CardData> hand = new();
    public readonly List<CardData> discardPile = new();
    public readonly List<CardData> revealedCards = new();
    public bool isProtected;
    public bool isEliminated;
    public int tokens;

    public PlayerState(int id, string name = null, int actorNumber = -1, bool isBot = false, List<CardData> hand = null) 
    { 
        this.id = id; 
        this.name = name;
        this.actorNumber = actorNumber;
        this.isBot       = isBot;
        if (hand != null)
        {
            this.hand.AddRange(hand);
        }
    }
}