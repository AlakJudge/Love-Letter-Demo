using System;
using System.Collections.Generic;
using UnityEngine;

public enum TurnPhase { StartTurn, Draw, ChooseCard, SelectTarget, SelectGuess, ResolveEffect, CheckOutcome, EndTurn, EndRound, GameOver }

public class TurnController
{
    public TurnPhase Phase { get; private set; } = TurnPhase.StartTurn;
    public static TurnController Instance { get; private set; }
    
    // Pending state (will be synced in multiplayer)
    public int pendingCardIndex = -1;
    public int pendingTargetId = -1;

    // Events for GameController to listen to
    public event Action OnNeedTargetSelection;
    public event Action OnNeedGuessSelection;
    public event Action OnTurnComplete;
    public event Action<PlayerState> OnRoundWin;
    public event Action<PlayerState> OnGameWin;
    public event Action<PlayerState, PlayerState, CardData> OnCardEffectResolved;
    public event Action<string,int> OnLog; // message, turn number, etc. For logging purposes.
    public event Action<PlayerState, CardData> OnCardDrawn;


    public bool ExecuteCommand(GameState game, PlayerCommand cmd, RuleValidation rules, out string error)
    {
        switch (cmd.type)
        {
            case CommandType.PlayCard:
                return ProcessPlayCard(game, cmd, rules, out error);
            
            case CommandType.SelectTarget:
                ProcessSelectTarget(game, cmd, rules, out error);
                return true;
            
            case CommandType.SelectGuess:
                return ProcessSelectGuess(game, cmd, rules, out error);
            
            default:
                error = "Unknown command type";
                return false;
        }
    }

    public void StartNewRound(GameState game, List<CardData> deckTemplate, int seed)
    {
        var rng = new System.Random(seed);
        
        // Reset player states
        foreach (var player in game.players)
        {
            player.hand.Clear();
            player.discardPile.Clear();
            player.revealedCards.Clear();
            player.isProtected = false;
            player.isEliminated = false;
            // Tokens persist across rounds
        }
        game.ClearSpyReveals();
        
        // Reset turn controller state
        pendingCardIndex = -1;
        pendingTargetId = -1;

        // Rebuild and shuffle deck
        game.deck.Clear();
        var newDeck = new List<CardData>(deckTemplate);
        for (int i = newDeck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (newDeck[i], newDeck[j]) = (newDeck[j], newDeck[i]);
        }
        foreach (var card in newDeck)
            game.deck.Push(card);
        
        game.SetAsideCard(game.deck.Pop()); // Set one card aside to use for prince effect, if necessary
        
        OnLog?.Invoke($"New round started! '{game.CurrentPlayer.name}' goes first. Removed a card and set it aside face down.", 1);
        Debug.Log($"Removed card: {game.removedCard.type}");

        // Deal initial hands
        foreach (var player in game.players)
        {
            if (game.deck.Count > 0)
                player.hand.Add(game.deck.Pop());
        }

        // Pick random starting player and reset turn number
        game.currentPlayerIndex = rng.Next(0, game.players.Count);
        game.turnNumber = 1;

        Log(game, $"'{game.CurrentPlayer.name}' starts the round.");
    }

    public void StartTurn(GameState game)
    {
        Phase = TurnPhase.StartTurn;
        game.CurrentPlayer.isProtected = false; // handmaid protection from previous turn wears off
        Log(game, $"'{game.CurrentPlayer.name}' begins their turn.");
        
        Phase = TurnPhase.Draw;
        Draw(game);
    }

    public void Draw(GameState game)
    {
        DrawCardForPlayer(game, game.CurrentPlayer);
        Log(game, $"'{game.CurrentPlayer.name}' draws a card.");
        Phase = TurnPhase.ChooseCard;
    }
    
    public void DrawCardForPlayer(GameState game, PlayerState player)
    {
        var card = game.deck.Pop();
        player.hand.Add(card);

        OnCardDrawn?.Invoke(player, card);
    }

    public bool ProcessPlayCard(GameState game, PlayerCommand cmd, RuleValidation rules, out string error)
    {
        if (Phase != TurnPhase.ChooseCard) { error = "Not in ChooseCard phase."; return false; }
        if (game.CurrentPlayer.id != cmd.playerId) { error = "Not your turn."; return false; }
        if (cmd.cardIndex < 0 || cmd.cardIndex >= game.CurrentPlayer.hand.Count) { error = "Invalid card index."; return false; }
        
        var card = game.CurrentPlayer.hand[cmd.cardIndex];

        // Do not allow player to play Princess
        if (card.type == CardType.Princess)
        {
            error = "Cannot play the Princess. Otherwise you are eliminated.";            
            // Trigger a visual warning effect
            OnCardEffectResolved?.Invoke(game.CurrentPlayer, null, card);
            return false;
        }

        if (!rules.HasCountessRule(game, card, out error))
        {
            OnCardEffectResolved?.Invoke(game.CurrentPlayer, null, card);
            return false;
        }
        
        pendingCardIndex = cmd.cardIndex;  

        // Check if card needs a target
        if (CardNeedsTarget(card.type))
        {
            Phase = TurnPhase.SelectTarget;
            OnNeedTargetSelection?.Invoke();
            return true;
        }
        // No target needed, resolve immediately
        return ResolveCard(game, cmd.cardIndex, -1, 0);
    }

    public bool ProcessSelectTarget(GameState game, PlayerCommand cmd, RuleValidation rules, out string error)
    {
        error = null;

        if (Phase != TurnPhase.SelectTarget) { error = "Not in target selection phase."; return false; }
        if (pendingCardIndex < 0) { error = "No pending card."; return false; }

        var card = game.CurrentPlayer.hand[pendingCardIndex];
    
        // If card is Guard, Spy, Baron or King, can't target self. Unless there are no valid targets.
        if (card.type == CardType.Guard || card.type == CardType.Spy 
            || card.type == CardType.Baron || card.type == CardType.King)
        {
            // If there are no unprotected and active players, allow target self without effect.
            if (rules.NoValidTargets(game))
            {
                cmd.targetPlayerId = game.CurrentPlayer.id; // allow targeting self, but card effect will handle it as no target
            }
            else if (cmd.targetPlayerId == game.CurrentPlayer.id)
            {
                error = "Cannot target yourself with this card.";
                return false;
            }
        }

        pendingTargetId = cmd.targetPlayerId;

        // If Guard, go to guess selection phase
        if (card.type == CardType.Guard)
        {            
            if (cmd.targetPlayerId == game.CurrentPlayer.id)
                {
                    // skip guess phase and end turn if targeting self with Guard (no effect)
                    // Still creates command for consistency and potential future use, but with no guess value.
                    Log(game, $"'{game.CurrentPlayer.name}' targeted themselves with Guard. No effect.");
                    return ResolveCard(game, pendingCardIndex, cmd.targetPlayerId, 0);
                }
            else
            {
                Phase = TurnPhase.SelectGuess;
                Log(game, $"'{game.CurrentPlayer.name}' targets Player '{game.players[cmd.targetPlayerId].name}' with Guard. Now choose a card to guess.");
                OnNeedGuessSelection?.Invoke();
                return true;
            }
        }
        return ResolveCard(game, pendingCardIndex, cmd.targetPlayerId, 0);
    }

    public bool ProcessSelectGuess(GameState game, PlayerCommand cmd, RuleValidation rules, out string error)
    {
        error = null;
        if (Phase != TurnPhase.SelectGuess) { error = "Not in guess selection phase."; return false; }
        if (pendingCardIndex < 0 || pendingTargetId < 0) { error = "No pending Guard to play."; return false; }

        // Guard state is set here so it's synchronized across all clients
        game.lastGuardSourcePlayerId = cmd.playerId;
        game.lastGuardTargetPlayerId = pendingTargetId;
        game.lastGuardGuessType = (CardType)cmd.guessValue;
      
        var target = game.players[pendingTargetId];
        game.lastGuardGuessCorrect = game.lastGuardGuessType == target.hand[0].type;

        return ResolveCard(game, pendingCardIndex, pendingTargetId, cmd.guessValue);
    }

    public bool ResolveCard(GameState game, int cardIndex, int targetId, int guessValue)
    {
        var player = game.CurrentPlayer;
        var card = game.CurrentPlayer.hand[cardIndex];

        Log(game, $"'{game.CurrentPlayer.name}' played {card.type}.");

        // Invoke event for showing targeting animation for cards with targets
        PlayerState target = null;
        if (targetId >= 0)
            target = game.players[targetId];
        OnCardEffectResolved?.Invoke(game.CurrentPlayer, target, card); 

        // play card and discard from hand
        game.CurrentPlayer.hand.RemoveAt(cardIndex);
        game.CurrentPlayer.discardPile.Add(card);

        // Create full command for effect resolution
        var fullCmd = new PlayerCommand
        {
            playerId = game.CurrentPlayer.id,
            cardIndex = cardIndex,
            targetPlayerId = targetId >= 0 ? targetId : -1,
            guessValue = guessValue > 0 ? guessValue : 0
        };
        
        Phase = TurnPhase.ResolveEffect;
        var resolver = new EffectResolver();
        resolver.Resolve(game, fullCmd, card);

        // Once player has played a card, preivous spy knowledge is gone.
        game.ClearSpyRevealsForPlayer(game.CurrentPlayer.id);

        Phase = TurnPhase.CheckOutcome;

        // Clear pending state
        pendingCardIndex = -1;
        pendingTargetId = -1;

        // Check win conditions
        var checker = new WinConditionChecker();
        if (checker.CheckRoundWinCondition(game, out PlayerState winner, out string roundLogMessage))
        {
            Log(game, roundLogMessage);
            winner.tokens++;

            // Check if Game Over
            if (checker.CheckGameWinCondition(game, out PlayerState gameWinner, out string gameLogMessage))
            {
                Log(game, gameLogMessage);
                Phase = TurnPhase.GameOver;
                OnGameWin?.Invoke(gameWinner);
                return false;
            }
            else // If not, go to next round.
            {
                Phase = TurnPhase.EndRound;
                OnRoundWin?.Invoke(winner);
                return false;
            }
        }
        Phase = TurnPhase.EndTurn;
        OnTurnComplete?.Invoke();
        return true;
    }
    private bool CardNeedsTarget(CardType type)
    {
        return type == CardType.Guard || type == CardType.Spy || type == CardType.Baron 
            || type == CardType.Prince || type == CardType.King;
    }

    public void ResetPhase()
    {
        Phase = TurnPhase.ChooseCard;
        pendingCardIndex = -1;
        pendingTargetId = -1;
    }

    public void Log(GameState game, string message)
    {
        OnLog?.Invoke(message, game.turnNumber);
    }
}