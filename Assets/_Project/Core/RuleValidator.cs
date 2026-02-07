using System.Linq;
using UnityEngine;

public class RuleValidation
{
    public bool CanPlay(GameState game, CardData card, out string error)
    {
        error = null;
        Debug.Log("Checking rules...");

        // Countess rule: if player holds King or Prince, must play Countess
        var hasPrinceOrKing = game.CurrentPlayer.hand.Any(card => card.type == CardType.Prince || card.type == CardType.King);
        var hasCountess = game.CurrentPlayer.hand.Any(card => card.type == CardType.Countess);
        Debug.Log($"Has Prince or King: {hasPrinceOrKing}, Has Countess: {hasCountess}");
        Debug.Log(card.type);
        if (hasPrinceOrKing && hasCountess && card.type != CardType.Countess)
        {
            error = "Must play Countess when holding King or Prince.";
            return false;
        }

        return true;
    }
    public bool NoValidTargets(GameState game)
    {
        // Check if the target is active, not the player and not protected by Handmaid effect.
        var validTargets = game.players.Where(
            p => !p.isEliminated && p.id != game.CurrentPlayer.id && !p.isProtected)
            .ToList();
        return validTargets.Count == 0;
    }
}