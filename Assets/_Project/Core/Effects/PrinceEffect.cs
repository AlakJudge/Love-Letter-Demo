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
        game.ClearSpyRevealsForPlayer(target.id);

        // Check if discarded card is Princess -> eliminate
        if (discardedCard.type == CardType.Princess)
        {
            target.isEliminated = true;
            TurnLogger.Instance.Log($"'{target.name}' discarded Princess and is eliminated!", game.turnNumber);
        }
        else
        {
            if (game.deck.Count == 0)
            {
                TurnLogger.Instance.Log($"'{target.name}' discarded {discardedCard.type}. But the deck is empty, drawing removed card.", game.turnNumber);
                target.hand.Add(game.removedCard);
                game.removedCard = null;
                return;
            }
            var drawnCard = game.deck.Pop();
            target.hand.Add(drawnCard);
            
            TurnLogger.Instance.Log($"'{target.name}' discarded {discardedCard.type} and drew a new card.", game.turnNumber);
        }
    }
}