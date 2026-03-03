using System.Linq;
using UnityEngine;

public class RuleValidation
{
    public bool HasCountessRule(GameState game, CardData card, out string error)
    {
        error = null;

        // Countess rule: if player holds King or Prince, must play Countess
        var hasPrinceOrKing = game.CurrentPlayer.hand.Any(card => card.type == CardType.Prince || card.type == CardType.King);
        var hasCountess = game.CurrentPlayer.hand.Any(card => card.type == CardType.Countess);

        if (hasPrinceOrKing && hasCountess && card.type != CardType.Countess)
        {
            error = "Must play Countess when holding King or Prince.";
            return false;
        }
        return true;
    }

    // For BotController - returns index of Countess if it must be played
    public bool MustPlayCountess(PlayerState player, out int countessIndex)
    {
        countessIndex = -1;

        var hasPrinceOrKing = player.hand.Any(card => card.type == CardType.Prince || card.type == CardType.King);
        
        if (!hasPrinceOrKing)
            return false; // No Prince or King, so no need to play Countess

        for (int i = 0; i < player.hand.Count; i++)
        {
            if (player.hand[i].type == CardType.Countess)
            {
                countessIndex = i;
                return true; // Must play Countess
            }
        }
        return false; // No Countess in hand, so no rule violation
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