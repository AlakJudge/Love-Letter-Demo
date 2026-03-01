using UnityEngine;

[CreateAssetMenu(fileName = "SpyEffect", menuName = "Love Letter/Effects/Spy")]
public class SpyEffect : CardEffect
{
    public override void Resolve(GameState game, PlayerState source, PlayerState target, int? guessValue)
    {
        if (source == target) // In case of no valid targets, player will target self with no effect.
        {
            TurnLogger.Instance.Log("No valid targets for Spy. No effect.", game.turnNumber);
            Debug.Log("No valid targets for Spy. No effect.");
            return;
        }

        var revealedCard = target.hand[0];

        // Remove any previous reveal this source had about this target (keep latest only)
        game.spyReveals.RemoveAll(r =>
            r.sourcePlayerId == source.id &&
            r.targetPlayerId == target.id
        );

        game.spyReveals.Add(new SpyRevealInfo
        {
            sourcePlayerId = source.id,
            targetPlayerId = target.id,
            handIndex = 0
        });

        // Only send the name of the card if they're the source or the target, otherwise just say they see a card.
        if (GameController.Instance.IsLocalOwner(source) || GameController.Instance.IsLocalOwner(target))
        {
            Debug.Log($"Player {source.id + 1} spies on Player {target.id + 1}'s hand and sees a {revealedCard.type}");
            TurnLogger.Instance.Log($"Player {source.id + 1} spies on Player {target.id + 1}'s hand and sees a {revealedCard.type}", game.turnNumber);
        }            
        else
        {
            Debug.Log($"Player {source.id + 1} spies on Player {target.id + 1}'s hand and sees a card");
            TurnLogger.Instance.Log($"Player {source.id + 1} spies on Player {target.id + 1}'s hand and sees a card", game.turnNumber);
        }
    }
}