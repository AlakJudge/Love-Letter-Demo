using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private int id;
    [SerializeField] private bool isLocalPlayer;
    [SerializeField] private string displayName;
    [SerializeField] private int tokens;
    [SerializeField] private bool isProtected;
    [SerializeField] private bool isEliminated;

    [SerializeField] private int handCount;
    [SerializeField] private int discardCount;
    private PlayerState state; 
    
    public void Bind(PlayerState state, string name)
    {
        this.state = state;
        this.displayName = name;
        Sync();
    }

    public void Sync()
    {
        if (state == null) return;
        id = state.id;

        // derive "local" from GameController instead of storing it in PlayerState
        var gc = GameController.Instance;
        isLocalPlayer = gc != null && gc.IsLocalOwner(state);

        tokens = state.tokens;
        isProtected = state.isProtected;
        isEliminated = state.isEliminated;
        handCount = state.hand.Count;
        discardCount = state.discardPile.Count;
    }
}