using System.Collections.Generic;
using UnityEngine;

public class GameState
{
    public readonly List<PlayerState> players = new();
    public readonly Stack<CardData> deck = new();
    public readonly List<CardData> discard = new();
    public int currentPlayerIndex;
    public int turnNumber;

    public PlayerState CurrentPlayer => players[currentPlayerIndex];

    public GameState(IEnumerable<PlayerState> players, IEnumerable<CardData> deckCards)
    {
        foreach (var player in players) this.players.Add(player);
        foreach (var card in deckCards) deck.Push(card);
        currentPlayerIndex = 0;
        turnNumber = 1;
    }

    public void AdvanceToNextPlayer()
    {

        int start = currentPlayerIndex;
        do
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
            Debug.Log("Advancing to player " + (currentPlayerIndex + 1));

        }
        while (players[currentPlayerIndex].isEliminated && currentPlayerIndex != start);
        
        // Increment turn number if we wrapped around to the first player
        if (currentPlayerIndex == 0)
            turnNumber++;
            Debug.Log("Turn number: " + turnNumber);
    }
}