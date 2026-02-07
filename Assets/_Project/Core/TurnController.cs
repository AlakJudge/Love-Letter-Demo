using UnityEngine;

public enum TurnPhase { StartTurn, Draw, ChooseCard, SelectTarget, SelectGuess, ResolveEffect, CheckOutcome, EndTurn, EndRound, GameOver }

public struct PlayCardCommand
{
    public int playerId;
    public CardData card;
    public int? targetPlayerId; // For cards that target another player
    public int? guessValue; // for Guard
}

public class TurnController
{
    public TurnPhase Phase { get; private set; } = TurnPhase.StartTurn;
    public CardData pendingCard; // for cards that require additional input (e.g., target, guess)
    public int? pendingTargetId; // to store target for Guard
    private TurnLogger turnLogger = TurnLogger.Instance;

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

    public bool TryPlayCard(GameState game, CardData card, RuleValidation rules, EffectResolver resolver, out string error)
    {
        error = null;

        if (Phase != TurnPhase.ChooseCard) { error = "Not in ChooseCard phase."; return false; }
        if (game.CurrentPlayer.id != game.currentPlayerIndex) { error = "Not your turn."; return false; }
        if (!game.CurrentPlayer.hand.Contains(card)) { error = "Card not in hand."; return false; }

        if (!rules.CanPlay(game, card, out error)) return false;

        // Check if card needs a target
        if (CardNeedsTarget(card.type))
        {
            pendingCard = card;
            Phase = TurnPhase.SelectTarget;
            Debug.Log($"Player {game.CurrentPlayer.id + 1} selected {card.name}. Choose a target.");
            return true;
        }

        // No target needed, play immediately
        var cmd = new PlayCardCommand
        {
            playerId = game.CurrentPlayer.id,
            card = card,
            targetPlayerId = null,
            guessValue = null
        };

        turnLogger.Log($"Player {game.CurrentPlayer.id + 1} played {card.type}.", game.turnNumber);

        return PlayTurn(game, cmd, rules, resolver, out error);
    }

    public bool TrySelectTarget(GameState game, int targetPlayerId, RuleValidation rules, EffectResolver resolver, out string error)
    {
        error = null;
        var cmd = new PlayCardCommand();

        if (Phase != TurnPhase.SelectTarget) { error = "Not in target selection phase."; return false; }
        if (pendingCard == null) { error = "No pending card."; return false; }

        // If card is Guard, Spy, Baron or King, can't target self. Unless there are no valid targets.
        if (pendingCard.type == CardType.Guard || pendingCard.type == CardType.Spy 
            || pendingCard.type == CardType.Baron || pendingCard.type == CardType.King)
        {
            // If there are no unprotected and active players, allow target self without effect.
            if (rules.NoValidTargets(game))
            {
                Debug.Log("No valid targets available.");
                targetPlayerId = game.CurrentPlayer.id; // allow targeting self, but card effect will handle it as no target
            }
            else if (targetPlayerId == game.CurrentPlayer.id)
            {
                error = "Cannot target yourself with this card.";
                return false;
            }
        }

        TurnLogger.Instance.Log($"Player {game.CurrentPlayer.id + 1} played {pendingCard.type}.", game.turnNumber);

        // If Guard, go to guess selection phase
        if (pendingCard.type == CardType.Guard)
        {
            pendingTargetId = targetPlayerId;
            
            if (targetPlayerId == game.CurrentPlayer.id)
                {
                    // skip guess phase and end turn if targeting self with Guard (no effect)
                    // Still creates command for consistency and potential future use, but with no guess value.
                    cmd = new PlayCardCommand
                    {
                        playerId = game.CurrentPlayer.id,
                        card = pendingCard,
                        targetPlayerId = targetPlayerId,
                        guessValue = null
                    };
                    TurnLogger.Instance.Log($"Player {game.CurrentPlayer.id + 1} targeted themselves with Guard. No effect.", game.turnNumber);
                }
            else
            {
                Phase = TurnPhase.SelectGuess;
                Debug.Log($"Player {game.CurrentPlayer.id + 1} targets Player {targetPlayerId + 1}. Now choose a card to guess.");
                TurnLogger.Instance.Log($"Player {game.CurrentPlayer.id + 1} targets Player {targetPlayerId + 1} with Guard. Now choose a card to guess.", game.turnNumber);
                return true;
            }
        }  
        else
        {
            // For other cards, resolve immediately
            cmd = new PlayCardCommand
            {
                playerId = game.CurrentPlayer.id,
                card = pendingCard,
                targetPlayerId = targetPlayerId,
                guessValue = null
            };
            TurnLogger.Instance.Log($"Player {game.CurrentPlayer.id + 1} selected Player {targetPlayerId + 1} as target.", game.turnNumber);
        }
        var success = PlayTurn(game, cmd, rules, resolver, out error);
        if (success)
        {
            pendingCard = null;
            pendingTargetId = null;  
        } 
        return success;
    }

    public bool TrySelectGuess(GameState game, int guessValue, RuleValidation rules, EffectResolver resolver, out string error)
    {
        error = null;
        if (Phase != TurnPhase.SelectGuess) { error = "Not in guess selection phase."; return false; }
        if (pendingCard == null || !pendingTargetId.HasValue) { error = "No pending Guard to play."; return false; }

        // Command with turn play info
        var cmd = new PlayCardCommand
        {
            playerId = game.CurrentPlayer.id,
            card = pendingCard,
            targetPlayerId = pendingTargetId,
            guessValue = guessValue
        };

        var success = PlayTurn(game, cmd, rules, resolver, out error);

        // Clear pending state to avoid unexpected issues
        if (success)
        {
            pendingCard = null;
            pendingTargetId = null;
        }
        return success;
    }

    public bool PlayTurn(GameState game, PlayCardCommand cmd, RuleValidation rules, EffectResolver resolver, out string error)
    {
        error = null;
        var controller = Object.FindFirstObjectByType<GameController>();
        
        // play card and discard from hand
        game.CurrentPlayer.hand.Remove(cmd.card);
        game.CurrentPlayer.discardPile.Add(cmd.card);

        Phase = TurnPhase.ResolveEffect;
        resolver.Resolve(game, cmd);

        Phase = TurnPhase.CheckOutcome;

        // Check win conditions
        WinConditionChecker checker = new WinConditionChecker();
        if (checker.CheckRoundWinCondition(game, out PlayerState winner))
        {
            Debug.Log($"Player {winner.id + 1} wins the round!");
            winner.tokens++;

            // Check if Game Over
            if (checker.CheckGameWinCondition(game, out PlayerState gameWinner))
            {
                controller.UpdateUI();
                Debug.Log($"Player {gameWinner.id + 1} wins the game with {gameWinner.tokens} tokens!");
                TurnLogger.Instance.Log($"Player {gameWinner.id + 1} wins the game with {gameWinner.tokens} tokens!", game.turnNumber);
                controller.OnGameOver(gameWinner);
                Phase = TurnPhase.GameOver;
                return false;
            }
            else // If not, go to next round.
            {
                controller.OnRoundOver(winner);
                Phase = TurnPhase.EndRound;
                return false;
            }
        }
        Phase = TurnPhase.EndTurn;
        game.AdvanceToNextPlayer();

        return true;
    }
    private bool CardNeedsTarget(CardType type)
    {
        return type == CardType.Guard || type == CardType.Spy || type == CardType.Baron 
            || type == CardType.Prince || type == CardType.King;
    }
}