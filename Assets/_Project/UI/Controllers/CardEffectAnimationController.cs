using System;
using System.Collections;
using UnityEngine;

public class CardEffectAnimationController : MonoBehaviour
{
    public CardPlayAnimator cardPlayAnimator;
    public int localPlayerId;
    
    private float animationDelay => 
    GameController.Instance != null ? GameController.Instance.botDelay : 1f;

    // For showing countess rule warning when player tries to play prince or king with countess in hand
    private CardData lastCountessWarningCard; 

    private UIController ui;
    private GameState game;
    private PlayerView playerArea;

    public void Bind(UIController uiController, GameState gameState, int localId, PlayerView playerAreaView)
    {
        ui = uiController;
        game = gameState;
        localPlayerId = localId;
        playerArea = playerAreaView;
        Debug.Log($"CardEffectAnimationController bound. ui={ui != null}, game={game != null}, playerArea={playerArea != null}, cardPlayAnimator={cardPlayAnimator != null}");

    }

    public IEnumerator ShowCardEffect(PlayerState source, PlayerState target, CardData card)
    {
        if (game == null) yield break;

        switch (card.type)
        {
            case CardType.Princess:
                // If player plays princess, show a warning effect on the princess card since this will eliminate them
                if (source.id == localPlayerId && playerArea != null)
                {
                    CardView princessView = playerArea.handView.FindViewForCard(card);
                    if (princessView != null)
                    {
                        princessView.SetColor(Color.softRed);
                        yield return new WaitForSeconds(animationDelay);
                        princessView.SetColor(Color.white);
                    }
                }
                ui.CancelCardSelection();
                break;
            case CardType.Prince or CardType.King:
                // Check if Countess is in hand
                if (source.hand.Exists(c => c.type == CardType.Countess))
                {
                    // Only highlight Countess if this is the second time we're warning about this card play if player keeps trying to play prince/king with countess in hand
                    bool highlightCountess = lastCountessWarningCard == card; 
                    lastCountessWarningCard = card;
                    
                    if (playerArea != null)
                    {
                        // Highlight the prince or king in red if player tried to play it with countess in hand
                        CardView princeOrKingView = playerArea.handView.FindViewForCard(card);
                        if (princeOrKingView != null)
                            princeOrKingView.SetColor(Color.softRed);

                        var countessView = source.hand.Find(c => c.type == CardType.Countess);
                        CardView countessCardView = null;
                        
                        // If we've already shown the warning for this card once, show the countess in green to really point the user to play the countess
                        if (countessView != null && highlightCountess)
                        {
                            countessCardView = playerArea.handView.FindViewForCard(countessView);
                            if (countessCardView != null)
                                countessCardView.SetColor(Color.softGreen);
                        }
                        // Wait, then reset
                        yield return new WaitForSeconds(animationDelay);

                        if (princeOrKingView != null)
                            princeOrKingView.SetColor(Color.white);

                        if (countessCardView != null)
                            countessCardView.SetColor(Color.white);
                    }
                }
                break;
            case CardType.Baron:
                if (target != null && source.hand.Count > 0 && target.hand.Count > 0)
                {
                    if (source.id == target.id) // don't show effect when targeting self with no effect
                        yield break;
                    
                    CardData sourceCard = null;
                    bool isLocalBaronOwner = source.id == localPlayerId;

                    // Find the card that's not a baron, unless both are
                    for (int i = 0; i < source.hand.Count; i++)
                    {
                        if (source.hand[i].type != CardType.Baron)
                        {
                            sourceCard = source.hand[i];
                            break;
                        }
                        else
                        {
                            sourceCard = source.hand[1]; // in case both cards are barons
                        }
                    }
                    // Show tha baron card and the back of the card to all plaeyrs
                    CardData targetCard = target.hand[target.hand.Count - 1];
                    
                    // Only Baron player and target see target's card face‑up
                    bool canSeeTarget =
                        localPlayerId == source.id ||
                        localPlayerId == target.id;

                    yield return AnimateCompareCards(
                        source, target,
                        sourceCard, targetCard,
                        revealSource: canSeeTarget, 
                        revealTarget: canSeeTarget,
                        destroyAtEnd: !isLocalBaronOwner); // Only reveal to baron player

                    yield return new WaitForSeconds(animationDelay);
                    cardPlayAnimator.DestroyLastCompare();
                }


                break;
            case CardType.Spy:
                // Show target back at first, then Spy reveals the target's card
                if (target != null && source.hand.Count > 0 && target.hand.Count > 0)
                {
                    if (source.id == target.id) // don't show effect when targeting self with no effect
                        yield break;

                    // Find the spy in hand
                    CardData sourceCard = null;
                    for (int i = 0; i < source.hand.Count; i++)
                    {
                        if (source.hand[i].type == CardType.Spy)
                        {  
                            sourceCard = source.hand[i];
                        }
                    }
                    CardData targetCard = target.hand[target.hand.Count - 1];
                    bool isLocalSpyOwner = source.id == localPlayerId;

                    // Phase 1: Spy shown, target hidden
                    yield return AnimateCompareCards(
                        source, target,
                        sourceCard, targetCard,
                        revealSource: true,
                        revealTarget: false,
                        destroyAtEnd: !isLocalSpyOwner); // Only reveal to spy player

                    // Phase 2: Spy reveals target card
                    yield return cardPlayAnimator.RevealLastCompare(
                        revealSource: true,
                        revealTarget: true);

                    yield return new WaitForSeconds(animationDelay);
                    cardPlayAnimator.DestroyLastCompare();
                }
                break;
            case CardType.Guard:
                if (target != null && source.hand.Count > 0 && target.hand.Count > 0)
                {
                    if (source.id == target.id) // don't show effect when targeting self with no effect
                        yield break;

                    // Find the guard in hand
                    CardData sourceCard = null;
                    for (int i = 0; i < source.hand.Count; i++)
                    {
                        if (source.hand[i].type == CardType.Guard)
                        {  
                            sourceCard = source.hand[i];
                        }
                    }
                    CardData targetCard = target.hand[target.hand.Count - 1];

                    // Phase 1: Guard shown, target hidden
                    yield return AnimateCompareCards(
                        source, target,
                        sourceCard, targetCard,
                        revealSource: true,
                        revealTarget: false,
                        destroyAtEnd: false);
                    
                    // Phase 2: Check if guess was correct and show result
                    bool guessCorrect = game.lastGuardGuessType == targetCard.type;
                    
                    // Phase 3: Guard reveals target card
                    if (guessCorrect)
                    {                        
                        yield return cardPlayAnimator.RevealLastCompare(
                            revealSource: true,
                            revealTarget: true);
                    }
                    yield return new WaitForSeconds(animationDelay);
                    cardPlayAnimator.DestroyLastCompare();
                }
                break;
        }
    }

    public IEnumerator AnimateCardPlay(PlayerState player, CardData card, Func<IEnumerator> afterEffect = null)
    {
        if (cardPlayAnimator == null || ui == null || game == null) yield break;

        var sourceView = ui.FindCardView(player.id, card);
        if (sourceView == null) yield break;
        
        // fly-in
        yield return cardPlayAnimator.PlaySingleCardRoutine(sourceView, card);

        // optional effect (compare, etc.)
        if (afterEffect != null)
            yield return afterEffect();
    }

    public IEnumerator AnimateCompareCards(
        PlayerState source, PlayerState target,
        CardData sourceCard, CardData targetCard,
        bool revealSource, bool revealTarget, bool destroyAtEnd = true)
    {
        if (cardPlayAnimator == null || ui == null || game == null) yield break;

        CardView sourceView = ui.FindCardView(source.id, sourceCard);
        CardView targetView = target != null ? ui.FindCardView(target.id, targetCard) : null;

        if (sourceView == null || targetView == null) yield break;

        yield return cardPlayAnimator.PlayCompareRoutine(
            sourceView, sourceCard,
            targetView, targetCard,
            revealSource, revealTarget, destroyAtEnd);
    }
}
