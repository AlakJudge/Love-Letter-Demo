using UnityEngine;

public abstract class CardEffect : ScriptableObject
{
    public abstract void Resolve(GameState game, PlayerState source, PlayerState target, int? guessValue);
}
