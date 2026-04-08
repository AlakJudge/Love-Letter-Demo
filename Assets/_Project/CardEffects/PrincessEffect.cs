using UnityEngine;

[CreateAssetMenu(fileName = "PrincessEffect", menuName = "Love Letter/Effects/Princess")]
public class PrincessEffect : CardEffect
{
    // Will never happen as Princess is not playable anymore
    // But in case of weird edge cases or future changes, just in case
    public override void Resolve(GameState game, PlayerState source, PlayerState target, int? guessValue)
    {
        // Player who plays Princess is eliminated
        source.isEliminated = true;
        // Send other card to discard pile too
        source.discardPile.Add(source.hand[0]);
        source.hand.Clear();
        game.ClearSpyRevealsForPlayer(source.id);
        Debug.Log($"'{source.name}' played the Princess and is eliminated!");
        TurnLogger.Instance.Log($"'{source.name}' played the Princess and is eliminated!", game.turnNumber);
    }
}