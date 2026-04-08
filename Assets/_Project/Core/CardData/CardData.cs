using UnityEngine;

[CreateAssetMenu(fileName = "Card", menuName = "Love Letter/Card")]
    
public class CardData : ScriptableObject
{
    [Header("Card Info")]
    public CardType type;
    [Tooltip("Value is derived from CardType and kept in sync.")]
    public int cardValue;
    [Tooltip("Effect script that resolves when this card is played.")]
    public CardEffect effect;
    
    [Header("Visual Assets")]
    public Sprite cardBack;
    public Sprite cardFront;
    public Sprite discardedIcon;
    public GameObject uiPrefab;

    private void OnValidate()
    {
        cardValue = (int)type; // keep value synced with enum
    }
}

public enum CardType
{
    Guard = 1,
    Spy = 2,
    Baron = 3,
    Handmaid = 4,
    Prince = 5,
    King = 6,
    Countess = 7,
    Princess = 8
}