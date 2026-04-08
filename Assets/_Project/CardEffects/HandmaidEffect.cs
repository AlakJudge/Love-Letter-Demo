using UnityEngine;

[CreateAssetMenu(fileName = "HandmaidEffect", menuName = "Love Letter/Effects/Handmaid")]
public class HandmaidEffect : CardEffect
{
    public override void Resolve(GameState game, PlayerState source, PlayerState target, int? guessValue)
    {
        source.isProtected = true;
        Debug.Log($"'{source.name}' is protected until their next turn.");
        TurnLogger.Instance.Log($"'{source.name}' is protected until their next turn.", game.turnNumber);
    }
}