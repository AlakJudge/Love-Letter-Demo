using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

[System.Serializable]
public class GameSetup
{
    [Tooltip("If true, the game will use the custom settings below instead of normal random setup")]
    public bool enableCustomSetup = false;
    [Tooltip("If < 0, use normal random starting player. Otherwise, set the index for the starting player (0-3)")]
    public int startingPlayerId = -1;
    [Tooltip("The starting hands for each player (1-4)")]
    public CardType player1StartingHand;
    public CardType player2StartingHand;
    public CardType player3StartingHand;
    public CardType player4StartingHand;

    [Tooltip("If non-empty, used as the exact top-to-bottom deck order")]
    public List<CardData> manualDeckOrder = new();
    [Tooltip("Optional fixed seed for RNG; 0 to ignore.")]
    public int fixedSeed = 0;
    public int playerCount = 0;

    public void AddBotsToGameConfig()
    {
        if (!enableCustomSetup) return;
        
        Debug.Log($"Using player count from GameSetup: {playerCount}");
        
        // Check if config has enough configured bots to match gameSetup's playerCount, if not, create more.
        int configuredBots = 0;
        for (int slotIndex = 1; slotIndex < playerCount; slotIndex++) // Start at 1 since we assume player 1 is always a human player 
        {
            int type = PlayerPrefs.GetInt($"SP_Slot_{slotIndex}_Type", 0); // 0 = Empty
            if (type == 2)
                configuredBots++;
        }

        if (configuredBots < playerCount)
        {
            int botsToAdd = playerCount - configuredBots;
            for (int i = 0; i < botsToAdd; i++)
            {
                int slotIndex = configuredBots + i;
                PlayerPrefs.SetInt($"SP_Slot_{slotIndex}_Type", 2); // 2 = Bot
                PlayerPrefs.SetString($"SP_Slot_{slotIndex}_BotName", $"Bot {slotIndex + 1}");
            }
        }       
    }

    public void ManualStartingHandsSetup(GameState game)
    {
        var startingHands = new List<CardType>() { CardType.None, CardType.None, CardType.None, CardType.None }; 
        // Only apply if any starting hand is set, to avoid accidentally overriding normal random hands
        if (player1StartingHand != CardType.None ||
            player2StartingHand != CardType.None ||
            player3StartingHand != CardType.None ||
            player4StartingHand != CardType.None)
        {
            startingHands[0] = player1StartingHand;
            startingHands[1] = player2StartingHand;
            startingHands[2] = player3StartingHand;
            startingHands[3] = player4StartingHand;
            
            // Go through deck stack and remove starting hand cards
            var tempStack = new Stack<CardData>();
            bool[] foundCard = new bool[4] { false, false, false, false };
            while (game.deck.Count > 0)
            {  
                var card = game.deck.Pop();
                for (int j = 0; j < startingHands.Count; j++)
                {
                    if (startingHands[j] != CardType.None && !foundCard[j])
                    {
                        if (card.type == startingHands[j])
                        {
                            foundCard[j] = true;
                            foreach (var player in game.players)
                            {
                                if (player.id == j)
                                {
                                    player.hand.Add(card);
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    if (j == startingHands.Count - 1) // If the last card in the list is reached and no match, push to temp stack
                        tempStack.Push(card);
                }
                if (game.deck.Count == 0) // Give warning if any specified starting hand cards were not added for some reason
                {
                    for (int j = 0; j < startingHands.Count; j++)
                    {
                        if (startingHands[j] != CardType.None && !foundCard[j])
                        {
                            Debug.LogWarning($"Starting hand card {startingHands[j]} for Player {j} is not valid.");
                        }
                    }
                }
            }
            // Replace the deck with the remaining cards after taking out the starting hands
            game.deck.Clear();
            while (tempStack.Count > 0)
            {
                game.deck.Push(tempStack.Pop());
            }
        }
    }

    public void SetManualDeckOrder(GameState game, List<CardData> deckTemplate)
    {
        // Create list of current cards from game deck (can be different from template if some cards were taken out for manual starting hands)
        List<CardData> deckCards = game.deck.ToList();

        // Create a dict with the card types and their quantities in the deck template for quick lookup
        Dictionary<CardType, int> cardTypeQuantities = new Dictionary<CardType, int>();
        for (int i = 0; i < deckCards.Count; i++)
        {
            var card = deckCards[i];
            if (cardTypeQuantities.ContainsKey(card.type))
                cardTypeQuantities[card.type]++;
            else
                cardTypeQuantities[card.type] = 1;
        }

        var tempStack = new Stack<CardData>();

        if (manualDeckOrder.Count <= deckCards.Count - 4) // Can't be longer than total cards minus the 4 set aside cards
        {
            while (game.deck.Count > 0)
            {
                var deckCard = game.deck.Pop();
                foreach (var card in manualDeckOrder)
                {         
                    if (card.type == deckCard.type)
                    {
                        tempStack.Push(deckCard);
                        deckCards.Remove(deckCard);
                        break;
                    }
                }
            }
        }

        Debug.Log($"Leftover cards count: {deckCards.Count}");

        // Set aside one card for prince effect and discard 3 cards face up for 2 player setup if necessary
        game.SetAsideCard(deckCards[0]);
        deckCards.RemoveAt(0);
        if (game.players.Count == 2)
        {
            for (int i = 0; i < 3; i++)
            {   
                game.setupFaceUpDiscards.Add(deckCards[0]);
                deckCards.RemoveAt(0);
            }
        }

        // Rebuild deck
        foreach (var card in deckCards)
            game.deck.Push(card);

        foreach (var card in tempStack)
            game.deck.Push(card);   
    }
}


