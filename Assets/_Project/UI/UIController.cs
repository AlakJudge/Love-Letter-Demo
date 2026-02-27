using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [Header("Containers")]
    public Transform currentPlayerContainer;
    public Transform opponentsContainer;
    public GameObject guardChoiceContainer;
    
    [Header("Buttons")]
    public Button discardPileZoomExitButton;
    public Button discardPileExpandButtonOpponents;
    public Button rematchButton;
    public Button quitButton;

    [Header("Other UI Elements")]
    public TransitionView transitionView;
    public CardZoomView cardZoomView;
    public DiscardPileZoomView discardPileZoomView;

    [Header("Prefabs")]
    public PlayerView playerAreaPrefab;
    public OpponentView opponentAreaPrefab;

    [Header("Animation")]
    public CardPlayAnimator cardPlayAnimator;

    [Header("Config")]
    public int localPlayerId = 0; 
    public bool showOpponentHands = false; // set by GameController
    public bool manualControlBots = false; // set by GameController

    public PlayerManager[] playerManagers;

    private GameState game;
    private PlayerView playerArea;
    private OpponentView[] opponentAreas;
    private GuardChoiceView guardChoiceView;
    private CardData lastCountessWarningCard; // For showing countess rule warning when player tries to play prince or king with countess in hand
    
    private float animationDelay => 
    GameController.Instance != null ? GameController.Instance.botDelay : 1f;

    // Events to send card index
    public event Action<int, int> OnPlayCard;        // playerId, cardIndex
    public event Action<int, int> OnSelectTarget;    // playerId, targetId
    public event Action<int, int> OnSelectGuess;     // playerId, guess
    public event Action OnRoundContinueClicked;
    public event Action OnRematchClicked;
    public event Action OnQuitClicked;
    

    private void Awake()
    {
        if (rematchButton != null)
            rematchButton.onClick.AddListener(() => OnRematchClicked?.Invoke());

        if (quitButton != null)
            quitButton.onClick.AddListener(() => OnQuitClicked?.Invoke());

        // Close discard pile zoom when full-screen button is clicked
        if (discardPileZoomExitButton != null)
            discardPileZoomExitButton.onClick.AddListener(HideDiscardPile);
    }

    public void Bind(GameState game)
    {
        this.game = game;

       // Hide guard choice initially
        if (guardChoiceContainer != null)
        {
            guardChoiceView = guardChoiceContainer.GetComponent<GuardChoiceView>();
            if (guardChoiceView == null)
                Debug.LogError("GuardChoiceContainer does not have a GuardChoiceView component.");

            guardChoiceView.gameObject.SetActive(false);

            if (guardChoiceView != null)
            {
                // When guessed card is selected, run rest of the turn normally.
                guardChoiceView.OnGuessSelected += guessValue => 
                {
                    OnSelectGuess?.Invoke(game.CurrentPlayer.id, guessValue);
                    guardChoiceView.gameObject.SetActive(false);
                };
            }
        }
        BuildAreas();
        RefreshAll();
    }

    private void BuildAreas()
    {
        // Clear containers
        ClearChildren(currentPlayerContainer);
        ClearChildren(opponentsContainer);

        // Build player area
        playerArea = Instantiate(playerAreaPrefab, currentPlayerContainer);
        playerArea.Bind(game.players[localPlayerId], $"Player {localPlayerId + 1}");

        // Hook card interactions to views and convert to events
        if (playerArea.handView != null)
        {
            playerArea.handView.OnCardClicked += card =>
            {
                if (game.CurrentPlayer.id != localPlayerId && !manualControlBots) return;

                int cardIndex = game.CurrentPlayer.hand.IndexOf(card);
                if (cardIndex >= 0)
                {
                    OnPlayCard?.Invoke(game.CurrentPlayer.id, cardIndex);
                }
            };

            playerArea.handView.OnCardLongPressed += card =>
            {
                if (cardZoomView != null) cardZoomView.Show(card);
            };
            playerArea.handView.OnCardReleased += card =>
            {
                if (cardZoomView != null) cardZoomView.Hide();                
            };
        }
        // Hook up local player as target (for debugging)
        if (playerArea != null)
        {
            playerArea.OnTargetSelected += targetId =>
            {
                OnSelectTarget?.Invoke(game.CurrentPlayer.id, targetId);
            };
        }
        // When discard pile is expanded, show the exit overlay button
        playerArea.OnDiscardPileExpanded += () =>
        {
            if (discardPileZoomExitButton != null)
                discardPileZoomExitButton.gameObject.SetActive(true);
            
            if (discardPileZoomView != null)
                discardPileZoomView.Show(game.players[localPlayerId]);
        };

        // OPPONENTS
        int oppCount = game.players.Count - 1;
        opponentAreas = new OpponentView[oppCount];

        int idx = 0;
        for (int i = 0; i < game.players.Count; i++)
        {
            if (i == localPlayerId) continue;
            var oppView = Instantiate(opponentAreaPrefab, opponentsContainer);
            oppView.Bind(game.players[i], $"Player {i + 1}");
    
            // Hook target selection
            oppView.OnTargetSelected += targetId =>
            {
                OnSelectTarget?.Invoke(game.CurrentPlayer.id, targetId);
            };

            // Hook opponent hand clicks if manual control enabled
            if (oppView.handView != null)
            {
                oppView.handView.OnCardClicked += card =>
                {
                    if (!manualControlBots) return;
                    int botPlayerId = oppView.GetPlayerId();
                    if (game.CurrentPlayer.id != botPlayerId) return;

                    int cardIndex = game.players[botPlayerId].hand.IndexOf(card);
                    if (cardIndex >= 0)
                    {
                        OnPlayCard?.Invoke(botPlayerId, cardIndex);
                    }
                };
            
                // Hook card long press and release events
                oppView.handView.OnCardLongPressed += card =>
                {
                    Debug.Log("long press");
                    if (cardZoomView != null) cardZoomView.Show(card);
                    else Debug.Log("card zoom view null");
                };
                oppView.handView.OnCardReleased += card =>
                {
                    if (cardZoomView != null) cardZoomView.Hide();                
                };

                // When opponent discard is expanded, show the same exit overlay
                oppView.OnDiscardPileExpanded += () =>
                {
                    if (discardPileZoomExitButton != null)
                        discardPileZoomExitButton.gameObject.SetActive(true);
                
                    if (discardPileZoomView != null)
                        discardPileZoomView.Show(game.players[oppView.GetPlayerId()]);
                };
            }
            opponentAreas[idx++] = oppView;
        }
    }

    public void HandleRoundWin(PlayerState winner)
    {
        HideGuardChoice();
        DisableTargeting();
        RefreshAll();

        if (transitionView != null)
        {
            // Ensure we don't stack multiple handlers across rounds
            transitionView.OnTransitionFinished -= HandleRoundTransitionFinished;
            transitionView.OnTransitionFinished += HandleRoundTransitionFinished;

            transitionView.ShowTransition(
                $"Player {winner.id + 1} wins the round!\n\nGet ready for the next round...",
                "RoundOver"
            );
        }
    }
    
    private void HandleRoundTransitionFinished()
    {
        OnRoundContinueClicked?.Invoke();
    }

    public void HandleGameWin(PlayerState winner)
    {
        HideGuardChoice();
        DisableTargeting();
        RefreshAll();

        if (transitionView != null)
        {
            transitionView.ShowTransition(
                $"Player {winner.id + 1} wins the game with {winner.tokens} tokens!\n\nClick below for a rematch.",
                "GameOver"
            );
        }

        if (rematchButton != null) rematchButton.gameObject.SetActive(true);
        if (quitButton != null) quitButton.gameObject.SetActive(true);
    }

    public void HideDiscardPile()
    {
        if (discardPileZoomExitButton != null)
            discardPileZoomExitButton.gameObject.SetActive(false);

        if (discardPileZoomView != null)
            discardPileZoomView.Hide();
    }

    public IEnumerator AnimateCardPlay(PlayerState player, CardData card, Func<IEnumerator> afterEffect = null)
    {
        if (cardPlayAnimator == null || game == null) yield break;

        CardView sourceView = null;

        if (player.id == localPlayerId)
        {
            sourceView = playerArea.handView.FindViewForCard(card);
        }
        else if (opponentAreas != null)
        {
            foreach (var opp in opponentAreas)
            {
                if (opp.GetPlayerId() == player.id && opp.handView != null)
                {
                    sourceView = opp.handView.FindViewForCard(card);
                    break;
                }
            }
        }

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
        if (cardPlayAnimator == null || game == null) yield break;

        CardView sourceView = null;
        CardView targetView = null;

        if (source.id == localPlayerId)
        {
            sourceView = playerArea?.handView?.FindViewForCard(sourceCard);
        }
        else if (opponentAreas != null)
        {
            foreach (var opp in opponentAreas)
            {
                if (opp.GetPlayerId() == source.id && opp.handView != null)
                {
                    sourceView = opp.handView.FindViewForCard(sourceCard);
                    break;
                }
            }
        }

        if (target != null)
        {
            if (target.id == localPlayerId)
            {
                targetView = playerArea?.handView?.FindViewForCard(targetCard);
            }
            else if (opponentAreas != null)
            {
                foreach (var opp in opponentAreas)
                {
                    if (opp.GetPlayerId() == target.id && opp.handView != null)
                    {
                        targetView = opp.handView.FindViewForCard(targetCard);
                        break;
                    }
                }
            }
        }

        if (sourceView == null || targetView == null) yield break;

        yield return cardPlayAnimator.PlayCompareRoutine(
            sourceView, sourceCard,
            targetView, targetCard,
            revealSource, revealTarget, destroyAtEnd);
    }

    public void ShowGuardChoice()
    {
        guardChoiceView.Show();
    }

    public void HideGuardChoice()
    {
        guardChoiceView.Hide();
    }

    public void EnableTargeting()
    {
        SetTargetingMode(true);
    }

    public void DisableTargeting()
    {
        SetTargetingMode(false);
    }
    
    private void SetTargetingMode(bool enabled)
    {
        // Local player
        if (playerArea != null)
        {
            var localPlayer = game.players[localPlayerId];
            // Can only target players that are not protected or eliminated. If no valid targets, allow targeting self without effect.
            bool canTargetSelf = enabled && !localPlayer.isProtected && !localPlayer.isEliminated;
            playerArea.SetTargetable(canTargetSelf);
        }
        // Opponents
        if (opponentAreas == null) return;
        foreach (var opp in opponentAreas)
        {
            var player = game.players[opp.GetPlayerId()];
            bool canTarget = enabled && !player.isProtected && !player.isEliminated;
            opp.SetTargetable(canTarget);
        }
    }

    public IEnumerator ShowCardEffect(PlayerState source, PlayerState target, CardData card)
    {
        if (game == null) yield break;

        switch (card.type)
        {
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
                    if (source.id == target.id)
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
                    bool isLocalGuardOwner = source.id == localPlayerId;

                    // Phase 1: Guard shown, target hidden
                    yield return AnimateCompareCards(
                        source, target,
                        sourceCard, targetCard,
                        revealSource: true,
                        revealTarget: false,
                        destroyAtEnd: !isLocalGuardOwner); // Only reveal to guard player
                    
                    // Phase 2: Check if guess was correct and show result
                    bool guessCorrect = game.lastGuardGuessType == targetCard.type;
                    
                    // Phase 3: Guard reveals target card
                    if (guessCorrect && isLocalGuardOwner)
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

    private void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }
    
    private void RefreshOpponents()
    {
        for (int i = 0; i < opponentAreas.Length; i++)
        {
            var oppView = opponentAreas[i];
            var oppState = game.players[oppView.GetPlayerId()];

            if (oppView.handView != null)
            {
                if (showOpponentHands)
                    oppView.handView.ShowHand(oppState);
                else
                {
                    //oppView.handView.ShowCardBack(oppState);
                    var revealedIndices = new HashSet<int>(
                        game.GetRevealedHandIndicesForSpyPlayer(localPlayerId, oppState.id)
                    );
                    oppView.handView.ShowCardBack(oppState, revealedIndices);
                }
            }
        }
        if (opponentAreas != null)
            foreach (var v in opponentAreas) v.Refresh();
    }

    private void RefreshPlayer()
    {
        if (playerArea != null && playerArea.handView != null && game != null)
        {
            var localPlayerState = game.players[localPlayerId];
            playerArea.handView.ShowHand(localPlayerState);
        }

        if (playerArea != null)
            playerArea.Refresh();
        
        if (playerManagers != null) // Sync player manager displays
            foreach (var pm in playerManagers) 
                pm.Sync();
    }

    public void RefreshAll()
    {
        RefreshOpponents();
        RefreshPlayer();
    }

    public void SetDisplayName(int playerIndex, string name)
    {
        if (playerIndex == localPlayerId) playerArea.SetName(name);
        else
        {
            int opponentIndex = playerIndex > localPlayerId ? playerIndex - 1 : playerIndex;
            opponentAreas[opponentIndex].SetName(name);
        }
    }
}