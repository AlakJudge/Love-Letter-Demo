public class WinConditionChecker
{
    public bool CheckRoundWinCondition(GameState game, out PlayerState winner, out string logMessage)
    {
        // Check if only one player remains
        var activePlayers = game.players.FindAll(p => !p.isEliminated);
        if (activePlayers.Count == 1)
        {
            winner = activePlayers[0];
            logMessage = $"\n---------\nPlayer {winner.id + 1} wins the round by elimination!\n---------\n";
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
            logMessage = $"\n---------\nPlayer {winner.id + 1} wins the round with the highest card!\n---------\n";
            return true;
        }
        logMessage = null;
        winner = null;
        return false;
    }
    public bool CheckGameWinCondition(GameState game, out PlayerState gameWinner, out string logMessage)
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
                logMessage = $"\n---------\nPlayer {gameWinner.id + 1} wins the game with {gameWinner.tokens} tokens!\n---------\n";
                return true;
            }
        }
        logMessage = null;
        gameWinner = null;
        return false;
    }
}
