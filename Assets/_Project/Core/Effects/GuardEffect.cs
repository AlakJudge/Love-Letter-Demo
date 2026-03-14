using UnityEngine;

[CreateAssetMenu(fileName = "GuardEffect", menuName = "Love Letter/Effects/Guard")]
public class GuardEffect : CardEffect
{
    public override void Resolve(GameState game, PlayerState source, PlayerState target, int? guessValue)
    {
        if (!guessValue.HasValue || target.hand.Count == 0) return;
        
        var targetCard = target.hand[0];

        // translate gussed value to card type for logging
        CardType guessedCardType = guessValue switch
        {
            1 => CardType.Guard,
            2 => CardType.Spy,
            3 => CardType.Baron,
            4 => CardType.Handmaid,
            5 => CardType.Prince,
            6 => CardType.King,
            7 => CardType.Countess,
            8 => CardType.Princess,
            _ => CardType.Guard // default case, should not happen due to validation
        };

        if (source == target) // In case of no valid targets, player will target self with no effect.
        {
            TurnLogger.Instance.Log("No valid targets for Guard. No effect.", game.turnNumber);
            Debug.Log("No valid targets for Guard. No effect.");
        }

        else if (targetCard.cardValue == guessValue.Value)
        {
            // Correct guess - eliminate target
            target.isEliminated = true;
            // Move card from hand to discard pile
            target.hand.Remove(targetCard);
            target.discardPile.Add(targetCard);
            target.revealedCards.Clear();
            Debug.Log($"'{source.name}' guessed correctly! '{target.name}' had {guessedCardType} and is eliminated!");
            TurnLogger.Instance.Log($"'{source.name}' guessed correctly! '{target.name}' had {guessedCardType} and is eliminated!", game.turnNumber);
        }
        else
        {
            Debug.Log($"'{source.name}' guessed {guessedCardType}, but '{target.name}' has {targetCard.type}. Guess failed.");
            TurnLogger.Instance.Log($"'{source.name}' guessed {guessedCardType}, but '{target.name}' doesn't have that card.", game.turnNumber);
        }
    }
}