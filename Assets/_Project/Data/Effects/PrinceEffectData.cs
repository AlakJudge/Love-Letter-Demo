using UnityEngine;

[CreateAssetMenu(fileName = "PrinceEffect", menuName = "Love Letter/Effects/Prince")]
public class PrinceEffect : CardEffect
{
    public override void Resolve(GameState game, PlayerState source, PlayerState target, int? guessValue)
    {
        // Target discards hand and draws a card
        var discardedCard = target.hand[0];
        target.discardPile.Add(discardedCard);

        // hand and revealed cards cleanup
        target.hand.Clear();
        target.revealedCards.Remove(discardedCard);

        // Check if discarded card is Princess -> eliminate
        if (discardedCard.type == CardType.Princess)
        {
            target.isEliminated = true;
            Debug.Log($"Player {target.id + 1} discarded Princess and is eliminated!");
            TurnLogger.Instance.Log($"Player {target.id + 1} discarded Princess and is eliminated!", game.turnNumber);
        }
        else
        {
            target.DrawCard(game.deck);
            Debug.Log($"Player {target.id + 1} discarded {discardedCard.type} and drew a new card.");
            TurnLogger.Instance.Log($"Player {target.id + 1} discarded {discardedCard.type} and drew a new card.", game.turnNumber);
        }
    }
}