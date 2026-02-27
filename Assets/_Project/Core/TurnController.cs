using System;
using System.Collections.Generic;
using UnityEngine;

public enum TurnPhase { StartTurn, Draw, ChooseCard, SelectTarget, SelectGuess, ResolveEffect, CheckOutcome, EndTurn, EndRound, GameOver }

public class TurnController
{
    public TurnPhase Phase { get; private set; } = TurnPhase.StartTurn;
    
    // Pending state (will be synced in multiplayer)
    public int pendingCardIndex = -1;
    public int pendingTargetId = -1;

    private TurnLogger turnLogger = TurnLogger.Instance;    

    // Events for GameController to listen to
    public event Action OnNeedTargetSelection;
    public event Action OnNeedGuessSelection;
    public event Action OnTurnComplete;
    public event Action<PlayerState> OnRoundWin;
    public event Action<PlayerState> OnGameWin;
    public event Action<PlayerState, PlayerState, CardData> OnCardEffectResolved;

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

    public void StartNewRound(GameState game, List<CardData> deckTemplate)
    {
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
            int j = UnityEngine.Random.Range(0, i + 1);
            (newDeck[i], newDeck[j]) = (newDeck[j], newDeck[i]);
        }
        foreach (var card in newDeck)
            game.deck.Push(card);
        
        game.SetAsideCard(game.deck.Pop()); // Set one card aside to use for prince effect, if necessary
        
        Debug.Log($"New round started! Player {game.CurrentPlayer.id + 1} goes first. Removed card: {game.removedCard.type}");
        TurnLogger.Instance.Log($"New round started! Player {game.CurrentPlayer.id + 1} goes first. Removed a card and set it aside face down.", 1);

        // Deal initial hands
        foreach (var player in game.players)
        {
            if (game.deck.Count > 0)
                player.hand.Add(game.deck.Pop());
        }

        // Pick random starting player and reset turn number
        game.currentPlayerIndex = UnityEngine.Random.Range(0, game.players.Count);
        game.turnNumber = 1;

        // Clear log and start logging
        TurnLogger.Instance.Clear();
        TurnLogger.Instance.Log($"New round started! Player {game.CurrentPlayer.id + 1} goes first.", 1);
    }

    public void StartTurn(GameState game)
    {
        Phase = TurnPhase.StartTurn;
        game.CurrentPlayer.isProtected = false; // handmaid protection from previous turn wears off
        turnLogger.Log($"Player {game.CurrentPlayer.id + 1}'s turn begins.", game.turnNumber);
        
        Phase = TurnPhase.Draw;
        Draw(game);
    }

    public void Draw(GameState game)
    {
        game.CurrentPlayer.DrawCard(game.deck);
        turnLogger.Log($"Player {game.CurrentPlayer.id + 1} draws a card.", game.turnNumber);
        Phase = TurnPhase.ChooseCard;
    }

    public bool ProcessPlayCard(GameState game, PlayerCommand cmd, RuleValidation rules, out string error)
    {
        if (Phase != TurnPhase.ChooseCard) { error = "Not in ChooseCard phase."; return false; }
        if (game.CurrentPlayer.id != cmd.playerId) { error = "Not your turn."; return false; }
        if (cmd.cardIndex < 0 || cmd.cardIndex >= game.CurrentPlayer.hand.Count) { error = "Invalid card index."; return false; }
        
        var card = game.CurrentPlayer.hand[cmd.cardIndex];

        if (!rules.HasCountessRule(game, card, out error))
        {
            OnCardEffectResolved?.Invoke(game.CurrentPlayer, null, card);
            return false;
        }
        
        pendingCardIndex = cmd.cardIndex;  
        turnLogger.Log($"Player {game.CurrentPlayer.id + 1} played {card.type}.", game.turnNumber);

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
                Debug.Log("No valid targets available.");
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
                    TurnLogger.Instance.Log($"Player {game.CurrentPlayer.id + 1} targeted themselves with Guard. No effect.", game.turnNumber);
                    return ResolveCard(game, pendingCardIndex, cmd.targetPlayerId, 0);
                }
            else
            {
                Phase = TurnPhase.SelectGuess;
                TurnLogger.Instance.Log($"Player {game.CurrentPlayer.id + 1} targets Player {cmd.targetPlayerId + 1} with Guard. Now choose a card to guess.", game.turnNumber);
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

        return ResolveCard(game, pendingCardIndex, pendingTargetId, cmd.guessValue);
    }

    public bool ResolveCard(GameState game, int cardIndex, int targetId, int guessValue)
    {
        var player = game.CurrentPlayer;
        var card = game.CurrentPlayer.hand[cardIndex];
    
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
        if (checker.CheckRoundWinCondition(game, out PlayerState winner))
        {
            Debug.Log($"Player {winner.id + 1} wins the round!");
            winner.tokens++;

            // Check if Game Over
            if (checker.CheckGameWinCondition(game, out PlayerState gameWinner))
            {
                Debug.Log($"Player {gameWinner.id + 1} wins the game with {gameWinner.tokens} tokens!");
                TurnLogger.Instance.Log($"Player {gameWinner.id + 1} wins the game with {gameWinner.tokens} tokens!", game.turnNumber);
                Phase = TurnPhase.GameOver;
                OnGameWin.Invoke(gameWinner);
                return false;
            }
            else // If not, go to next round.
            {
                Phase = TurnPhase.EndRound;
                OnRoundWin.Invoke(winner);
                return false;
            }
        }
        Phase = TurnPhase.EndTurn;
        OnTurnComplete.Invoke();
        return true;
    }
    private bool CardNeedsTarget(CardType type)
    {
        return type == CardType.Guard || type == CardType.Spy || type == CardType.Baron 
            || type == CardType.Prince || type == CardType.King;
    }
}