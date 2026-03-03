using System.Linq;

public class EffectResolver
{
    public void Resolve(GameState game, PlayerCommand cmd, CardData card)
    {
        // If countess is played, skip effect resolution
        if (card.type == CardType.Countess)
            return;
        
        var source = game.CurrentPlayer;
        var target = cmd.targetPlayerId >= 0 ? game.players.FirstOrDefault(p => p.id == cmd.targetPlayerId) : null;

        // Resolve the effect of the played card
        card.effect.Resolve(game, source, target, cmd.guessValue);
    }
}