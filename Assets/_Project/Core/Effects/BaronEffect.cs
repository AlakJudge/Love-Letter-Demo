using UnityEngine;

[CreateAssetMenu(fileName = "BaronEffect", menuName = "Love Letter/Effects/Baron")]
public class BaronEffect : CardEffect
{
    public override void Resolve(GameState game, PlayerState source, PlayerState target, int? guessValue)
    {
        // Compare card values and eliminate lowest
        var sourceCard = source.hand[0];
        var targetCard = target.hand[0];
        if (source == target)
        {
            TurnLogger.Instance.Log("No valid targets for Baron. No effect.", game.turnNumber);
            Debug.Log("No valid targets for Baron. No effect.");
        }
        else if (sourceCard.cardValue > targetCard.cardValue)
        {
            // Discard target's hand and mark as eliminated
            target.hand.Remove(targetCard);
            target.revealedCards.Clear();
            target.discardPile.Add(targetCard);
            target.isEliminated = true;
            Debug.Log($"Player {target.id + 1}'s card value is lower and they're eliminated!");
            TurnLogger.Instance.Log($"Player {target.id + 1}'s card value is lower and they're eliminated!", game.turnNumber);
        }
        else if (sourceCard.cardValue < targetCard.cardValue)
        {
            // Discard owner's hand and mark as eliminated
            source.hand.Remove(sourceCard);
            source.revealedCards.Clear();
            source.discardPile.Add(sourceCard);
            source.isEliminated = true;
            Debug.Log($"Player {source.id + 1}'s card value is lower and they're eliminated!");
            TurnLogger.Instance.Log($"Player {source.id + 1}'s card value is lower and they're eliminated!", game.turnNumber);
        }
        else
        {
            TurnLogger.Instance.Log("Values are a tie, no elimination.", game.turnNumber);
            Debug.Log("Values are a tie, no elimination.");
        }
    }
}