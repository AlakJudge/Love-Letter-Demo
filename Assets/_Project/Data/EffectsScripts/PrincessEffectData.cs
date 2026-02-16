using UnityEngine;

[CreateAssetMenu(fileName = "PrincessEffect", menuName = "Love Letter/Effects/Princess")]
public class PrincessEffect : CardEffect
{
    public override void Resolve(GameState game, PlayerState source, PlayerState target, int? guessValue)
    {
        // Player who plays Princess is eliminated
        source.isEliminated = true;
        // Send both cards to discard pile
        source.discardPile.Add(source.hand[1]);
        source.discardPile.Add(source.hand[0]);
        source.hand.Clear();
        source.revealedCards.Clear();
        Debug.Log($"Player {source.id + 1} played the Princess and is eliminated!");
        TurnLogger.Instance.Log($"Player {source.id + 1} played the Princess and is eliminated!", game.turnNumber);
    }
}