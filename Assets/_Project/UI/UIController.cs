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

    [Header("Config")]
    public int localPlayerId = 0; 
    public bool showOpponentHands = false; // set by GameController
    public bool manualControlBots = false; // set by GameController

    public PlayerManager[] playerManagers;

    private GameState game;
    private PlayerView playerArea;
    private OpponentView[] opponentAreas;
    private GuardChoiceView guardChoiceView;
    
    // Events to send card index
    public event Action<int, int> OnPlayCard;        // playerId, cardIndex
    public event Action<int, int> OnSelectTarget;    // playerId, targetId
    public event Action<int, int> OnSelectGuess;     // playerId, guess
    public event Action OnRoundContinueClicked;
    public event Action OnRematchClicked;
    public event Action OnQuitClicked;

    // Getters for views that need to be accessed by GameController and CardEffectAnimationController
    public PlayerView GetPlayerArea() => playerArea;

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

        // helper to get display name from GameContrroller
        string GetDisplayName(int playerId)
        {
            var gc = GameController.Instance;
            if (gc != null &&
                gc.playerNames != null &&
                playerId >= 0 &&
                playerId < gc.playerNames.Count &&
                !string.IsNullOrWhiteSpace(gc.playerNames[playerId]))
            {
                return gc.playerNames[playerId];
            }
            return $"Player {playerId + 1}";
        }

        // Build player area
        playerArea = Instantiate(playerAreaPrefab, currentPlayerContainer);
        playerArea.Bind(game.players[localPlayerId], GetDisplayName(localPlayerId));

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
            oppView.Bind(game.players[i], GetDisplayName(i));
    
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

    public CardView FindCardView(int playerId, CardData card)
    {
        if (card == null) return null;

        if (playerId == localPlayerId)
            return playerArea.handView.FindViewForCard(card);

        if (opponentAreas == null) return null;

        foreach (var opp in opponentAreas)
        {
            if (opp != null && opp.GetPlayerId() == playerId)
                return opp.handView.FindViewForCard(card);
        }

        return null;
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