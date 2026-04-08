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
        // Clear current spy reveals for both players, since after the swap, previous info could be outdated.
        game.ClearSpyRevealsForPlayer(target.id);
        game.ClearSpyRevealsForPlayer(source.id);
        // Add new spy reveal info for both players to see each other's original card (since they swap hands)
        game.spyReveals.Add(new SpyRevealInfo
        {
            sourcePlayerId = source.id,
            targetPlayerId = target.id,
            handIndex = 0
        });
        game.spyReveals.Add(new SpyRevealInfo
        {
            sourcePlayerId = target.id,
            targetPlayerId = source.id,
            handIndex = 0
        });

        // Swap hands with target
        var sourceCard = source.hand[0];
        var targetCard = target.hand[0];
        source.hand[0] = targetCard;
        target.hand[0] = sourceCard;
        Debug.Log($"'{source.name}' swaps hands with '{target.name}'");
        TurnLogger.Instance.Log($"'{source.name}' swaps hands with '{target.name}'.", game.turnNumber);
    }
}