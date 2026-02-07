using UnityEngine;

public class WinConditionChecker
{
    public bool CheckRoundWinCondition(GameState game, out PlayerState winner)
    {
        // Check if only one player remains
        var activePlayers = game.players.FindAll(p => !p.isEliminated);
        if (activePlayers.Count == 1)
        {
            winner = activePlayers[0];
            TurnLogger.Instance.Log($"Player {winner.id + 1} wins the round by elimination!\n---------\n", game.turnNumber);
            return true;
        }
    
        // Check if deck is empty to determine winner by highest card
        else if (game.deck.Count == 0)
        {
            int highestValue = -1;
            PlayerState roundWinner = null;
            foreach (var player in activePlayers)
            {
                int handValue = player.hand[0].cardValue;
                if (handValue > highestValue)
                {
                    highestValue = handValue;
                    roundWinner = player;
                }
            }
            winner = roundWinner;
            TurnLogger.Instance.Log($"Player {winner.id + 1} wins the round with the highest card!\n---------\n", game.turnNumber);
            return true;
        }
        winner = null;
        return false;
    }
    public bool CheckGameWinCondition(GameState game, out PlayerState gameWinner)
    {
        // Check if winner has enough tokens to win, based on total players
        int tokensToWin = game.players.Count switch
        {
            2 => 7,
            3 => 5,
            4 => 4,
            _ => 4
        };
        foreach (var player in game.players)
        {
            if (player.tokens >= tokensToWin)
            {
                gameWinner = player;
                return true;
            }
        }
        gameWinner = null;
        return false;
    }
}
