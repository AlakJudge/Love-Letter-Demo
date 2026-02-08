public enum CommandType { PlayCard, SelectTarget, SelectGuess }

public class PlayerCommand
{
    public CommandType type;
    public int playerId;
    public int cardIndex;        // Index in hand
    public int targetPlayerId;   // -1 if no target
    public int guessValue;       // 0 if not Guard

    // For network serialization
    public byte[] Serialize()
    {
        // Will implement when adding Photon
        return null;
    }

    public static PlayerCommand Deserialize(byte[] data)
    {
        // Will implement when adding Photon
        return default;
    }
}
