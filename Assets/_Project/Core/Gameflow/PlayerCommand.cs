public enum CommandType { PlayCard, SelectTarget, SelectGuess }

public class PlayerCommand
{
    public CommandType type;
    public int playerId;
    public int cardIndex;        // Index in hand
    public int targetPlayerId;   // -1 if no target
    public int guessValue;       // 0 if not Guard
}
