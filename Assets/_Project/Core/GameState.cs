using System.Collections.Generic;
using UnityEngine;

public class SpyRevealInfo
{
    public int sourcePlayerId;
    public int targetPlayerId;
    public int handIndex;
}

public class GameState
{
    public readonly List<PlayerState> players = new();
    public readonly Stack<CardData> deck = new();
    public readonly List<CardData> discard = new();
    public int currentPlayerIndex;
    public int turnNumber;
    public CardData removedCard;

    // Last Guard effect info
    public int  lastGuardSourcePlayerId = -1;
    public int  lastGuardTargetPlayerId = -1;
    public CardType? lastGuardGuessType = null;
    public bool lastGuardGuessCorrect   = false;

    public readonly List<SpyRevealInfo> spyReveals = new();

    public PlayerState CurrentPlayer => players[currentPlayerIndex];

    public GameState(IEnumerable<PlayerState> players, IEnumerable<CardData> deckCards)
    {
        foreach (var player in players) this.players.Add(player);
        foreach (var card in deckCards) deck.Push(card);
        currentPlayerIndex = 0;
        turnNumber = 1;
    }

    public void ClearSpyReveals()
    {
        spyReveals.Clear();
    }

    public void ClearSpyRevealsForPlayer(int targetPlayerId)
    {
        spyReveals.RemoveAll(r => r.targetPlayerId == targetPlayerId);
    }

    public IEnumerable<int> GetRevealedHandIndicesForSpyPlayer(int observerId, int targetId)
    {
        foreach (var r in spyReveals)
        {
            if (r.sourcePlayerId == observerId && r.targetPlayerId == targetId)
                yield return r.handIndex;
        }
    }

    public void ClearLastGuardInfo()
    {
        lastGuardSourcePlayerId = -1;
        lastGuardTargetPlayerId = -1;
        lastGuardGuessType      = null;
        lastGuardGuessCorrect   = false;
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

    public void SetAsideCard(CardData card)
    {
        // This can be used for Prince effect to set aside a card when deck is empty
        // and retrieve it later when needed
        removedCard = card;
    }
}