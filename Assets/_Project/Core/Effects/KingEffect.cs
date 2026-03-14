using UnityEngine;

[CreateAssetMenu(fileName = "KingEffect", menuName = "Love Letter/Effects/King")]
public class KingEffect : CardEffect
{
    public override void Resolve(GameState game, PlayerState source, PlayerState target, int? guessValue)
    {
        if (source == target) // In case of no valid targets, player will target self with no effect.
        {
            TurnLogger.Instance.Log("No valid targets for King. No effect.", game.turnNumber);
            Debug.Log("No valid targets for King. No effect.");
            return;
        }

        game.ClearSpyRevealsForPlayer(target.id);

        // Swap hands with target
        var sourceCard = source.hand[0];
        var targetCard = target.hand[0];
        source.hand[0] = targetCard;
        target.hand[0] = sourceCard;
        Debug.Log($"'{source.name}' swaps hands with '{target.name}'");
        TurnLogger.Instance.Log($"'{source.name}' swaps hands with '{target.name}'.", game.turnNumber);
    }
}