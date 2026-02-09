using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BotController
{
    // Returns a sequence of commands for the bot to execute this turn
    public static List<PlayerCommand> GetTurnCommands(GameState game, int botId, RuleValidation rules)
    {
        var commands = new List<PlayerCommand>();
        var bot = game.players.First(p => p.id == botId);

        // Choose which card to play
        int cardIndex = ChooseCardToPlay(game, bot, rules);
        var card = bot.hand[cardIndex];

        commands.Add(new PlayerCommand
        {
            type = CommandType.PlayCard,
            playerId = botId,
            cardIndex = cardIndex,
            targetPlayerId = -1,
            guessValue = 0
        });

        // If card needs target, choose one
        if (CardNeedsTarget(card.type))
        {
            int targetId = ChooseTarget(game, bot, card.type, rules);
            commands.Add(new PlayerCommand
            {
                type = CommandType.SelectTarget,
                playerId = botId,
                cardIndex = cardIndex,
                targetPlayerId = targetId,
                guessValue = 0
            });

            // If Guard, choose guess
            if (card.type == CardType.Guard && targetId != botId)
            {
                int guess = ChooseGuess(game, bot, targetId);
                commands.Add(new PlayerCommand
                {
                    type = CommandType.SelectGuess,
                    playerId = botId,
                    cardIndex = cardIndex,
                    targetPlayerId = targetId,
                    guessValue = guess
                });
            }
        }

        return commands;
    }

    private static int ChooseCardToPlay(GameState game, PlayerState bot, RuleValidation rules)
    {
        // PRIORITY 1: Must play Countess if holding Prince or King
        if (rules.MustPlayCountess(bot, out int countessIndex))
            return countessIndex;

        // PRIORITY 2: Never play princess
        int princessIndex = -1;
        int nonPrincessIndex = -1;
        for (int i = 0; i < bot.hand.Count; i++)
        {
            if (bot.hand[i].type == CardType.Princess)
                princessIndex = i;
            else
                nonPrincessIndex = i;
        }
        if (princessIndex >= 0) // Play non-Princess card if holding a Princess
            return nonPrincessIndex;         

        // PRIORITY 4: Play Prince if you know opponent holds a Princess
        for (int i = 0; i < bot.hand.Count; i++)
        {
            if (bot.hand[i].type == CardType.Prince)
            {
                // Check if any opponent has revealed a Princess
                bool opponentHasPrincess = game.players.Any(player => 
                    player.id != bot.id && 
                    !player.isEliminated &&  
                    player.revealedCards.Count > 0 && 
                    player.revealedCards[0].type == CardType.Princess
                    );
                if (opponentHasPrincess)
                    return i;
            }
        }

        // PRIORITY 3: Play Guard if any opponent has revealed their card to you
        for (int i = 0; i < bot.hand.Count; i++)
        {
            if (bot.hand[i].type == CardType.Guard)
            {
                bool hasKnownTarget = game.players.Any(player => 
                    player.id != bot.id && 
                    !player.isEliminated &&  
                    player.revealedCards.Count > 0
                    );
                if (hasKnownTarget)
                    return i;
            }
        }

        // PRIORITY 5: Play Baron if your card value is 5 or higher
        for (int i = 0; i < bot.hand.Count; i++)
        {
            if (bot.hand[i].type == CardType.Baron)
            {
                int otherCardValue = bot.hand[1 - i].cardValue;
                if (otherCardValue >= 5)
                    return i;
            }
        }

        // PRIORITY 6: Play Handmaid to protect yourself if no better options
        for (int i = 0; i < bot.hand.Count; i++)
        {
            if (bot.hand[i].type == CardType.Handmaid)
                return i;
        }

        // PRIORITY 7: Player random card
        return Random.Range(0, bot.hand.Count);
    }

    private static int ChooseTarget(GameState game, PlayerState bot, CardType cardType, RuleValidation rules)
    {
        var validTargets = game.players
            .Where(p => p.id != bot.id && !p.isEliminated && !p.isProtected)
            .ToList();

        // If no valid targets, must target self
        if (validTargets.Count == 0)
            return bot.id;

        switch (cardType)
        {
            case CardType.Guard:
                // Target opponent that has revealed a card to you
                var knownTargets = validTargets.Where(player => player.revealedCards.Count > 0).ToList();
                if (knownTargets.Count > 0)
                    return knownTargets[Random.Range(0, knownTargets.Count)].id;

                return validTargets[Random.Range(0, validTargets.Count)].id; // or random if none

            case CardType.Spy:
                // Look at random opponent's hand, if they don't have any revealed cards already
                var unknownTargets = validTargets.Where(player => player.revealedCards.Count == 0).ToList();
                if (unknownTargets.Count > 0)
                    return unknownTargets[Random.Range(0, unknownTargets.Count)].id;

                return validTargets[Random.Range(0, validTargets.Count)].id; // or random

            case CardType.Baron:
                // Compare with opponent that has revealed card if it's lower value than yours
                var weakerTargets = validTargets.Where(player => 
                    player.revealedCards.Count > 0 && 
                    player.revealedCards[0].cardValue < bot.hand[0].cardValue)
                    .ToList();
                if (weakerTargets.Count > 0)
                    return weakerTargets[Random.Range(0, weakerTargets.Count)].id;

                return validTargets[Random.Range(0, validTargets.Count)].id; // or random

            case CardType.Prince:
                // Prioritise targeting opponent with Princess
                var princessTarget = validTargets.FirstOrDefault(player => 
                    player.revealedCards.Count > 0 && 
                    player.revealedCards[0].type == CardType.Princess);
                if (princessTarget != null)
                    return princessTarget.id;

                // Prioritise revealed cards of value 5 or higher
                var strongTargets = validTargets.Where(player =>
                    player.revealedCards.Count > 0 &&
                    player.revealedCards[0].cardValue >= 5)
                    .ToList();
                if (strongTargets.Count > 0)
                    return strongTargets[Random.Range(0, strongTargets.Count)].id;

                return validTargets[Random.Range(0, validTargets.Count)].id; // or random

            case CardType.King:
                // Swap hands with target that has revealed card of higher value than yours
                var betterTargets = validTargets.Where(player =>
                    player.revealedCards.Count > 0 &&
                    player.revealedCards[0].cardValue > bot.hand[0].cardValue)
                    .ToList();
                if (betterTargets.Count > 0)
                    return betterTargets[Random.Range(0, betterTargets.Count)].id;
                    
                return validTargets[Random.Range(0, validTargets.Count)].id; // or random

            default:
                return validTargets[Random.Range(0, validTargets.Count)].id;
        }
    }

    private static int ChooseGuess(GameState game, PlayerState bot, int targetId)
    {
        var targetPlayer = game.players.First(p => p.id == targetId);

        // Guess a revealed card if they have one
        if (targetPlayer.revealedCards.Count > 0)
            return targetPlayer.revealedCards[0].cardValue;

        // Guess most common cards
        // Weighted random based on card distribution in deck
        // 2 Priests, 2 Barons, 2 Handmaids, 2 Princes, 1 King, 1 Countess, 1 Princess
        var weights = new Dictionary<int, int>
        {
            { 2, 2 },  // Priest
            { 3, 2 },  // Baron
            { 4, 2 },  // Handmaid
            { 5, 2 },  // Prince
            { 6, 1 },  // King
            { 7, 1 },  // Countess
            { 8, 1 }   // Princess
        };

        // Check all discard piles, from each player, and readjust weight accordingly
        foreach (var player in game.players)
        {
            foreach (var card in player.discardPile)
            {
                if (weights.ContainsKey(card.cardValue))
                    weights[card.cardValue]--;
            }
            if (player.revealedCards.Count > 0)
            {
                var revealedCard = player.revealedCards[0];
                if (weights.ContainsKey(revealedCard.cardValue))
                    weights[revealedCard.cardValue]--;
            }
        }

        // Get the card(s) with the highest remaining weight and guess randomly among them
        var highestWeightCards = weights.Where(v=> v.Value == weights.Values.Max()).Select(kv => kv.Key).ToList();
        return highestWeightCards[Random.Range(0, highestWeightCards.Count)];
    }

    private static bool CardNeedsTarget(CardType type)
    {
        return type == CardType.Guard || type == CardType.Spy || type == CardType.Baron 
            || type == CardType.Prince || type == CardType.King;
    }
}