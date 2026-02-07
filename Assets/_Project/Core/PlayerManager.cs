using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private int id;
    [SerializeField] private string displayName;
    [SerializeField] private int tokens;
    [SerializeField] private bool isProtected;
    [SerializeField] private bool isEliminated;
    [SerializeField] private HashSet<CardData> revealedCards = new();

    [SerializeField] private int handCount;
    [SerializeField] private int discardCount;
    private PlayerState state; 
    
    public void Bind(PlayerState state, string name)
    {
        this.state = state;
        this.displayName = name;
        Sync();
    }

    public void Sync()
    {
        if (state == null) return;
        id = state.id;
        tokens = state.tokens;
        isProtected = state.isProtected;
        isEliminated = state.isEliminated;
        revealedCards = new HashSet<CardData>(state.revealedCards);
        handCount = state.hand.Count;
        discardCount = state.discardPile.Count;
    }
}