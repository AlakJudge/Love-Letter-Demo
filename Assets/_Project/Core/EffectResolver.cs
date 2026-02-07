using System.Linq;

public class EffectResolver
{
    public void Resolve(GameState game, PlayCardCommand cmd)
    {
        var source = game.CurrentPlayer;
        var target = cmd.targetPlayerId.HasValue ? game.players.First(p => p.id == cmd.targetPlayerId.Value) : null;

        // Resolve the effect of the played card
        cmd.card.effect?.Resolve(game, source, target, cmd.guessValue);
    }
}