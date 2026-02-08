using System;
using UnityEngine;

public class UIController : MonoBehaviour
{
    [Header("Containers")]
    public Transform currentPlayerContainer;
    public Transform opponentsContainer;
    public GameObject guardChoiceContainer;

    [Header("Prefabs")]
    public PlayerView playerAreaPrefab;
    public OpponentView opponentAreaPrefab;

    [Header("Config")]
    public int localPlayerId = 0; 
    public bool showOpponentHands = false; // set by GameController
    public bool manualControlBots = false; // set by GameController

    private GameState game;
    private PlayerView playerArea;
    private OpponentView[] opponentAreas;
    private GuardChoiceView guardChoiceView;

    // Events to send card index
    public event Action<int, int> OnPlayCard;        // playerId, cardIndex
    public event Action<int, int> OnSelectTarget;    // playerId, targetId
    public event Action<int, int> OnSelectGuess;     // playerId, guess

    public void Bind(GameState game)
    {
        this.game = game;

       // Hide guard choice initially
        if (guardChoiceContainer != null)
        {
            guardChoiceView = guardChoiceContainer.GetComponent<GuardChoiceView>();
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

        // Hook card clicks to play command
        if (playerArea.handView != null)
        {
            playerArea.handView.OnCardClicked += card =>
            {
                if (game.CurrentPlayer.id != localPlayerId && !manualControlBots) return;

                int cardIndex = game.CurrentPlayer.hand.IndexOf(card);
                if (cardIndex >= 0)
                {
                    OnPlayCard.Invoke(game.CurrentPlayer.id, cardIndex);
                }
            };
        }
        // Hook up local player as target (for debugging)
        if (playerArea != null)
        {
            playerArea.OnTargetSelected += targetId =>
            {
                OnSelectTarget.Invoke(game.CurrentPlayer.id, targetId);
            };
        }

        // OPPONENTS
        int oppCount = game.players.Count - 1;
        opponentAreas = new OpponentView[oppCount];

        int idx = 0;
        for (int i = 0; i < game.players.Count; i++)
        {
            if (i == localPlayerId) continue;
            var oppView = Instantiate(opponentAreaPrefab, opponentsContainer);
            oppView.Bind(game.players[i], $"Player {i + 1}", showOpponentHands);
    
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
                        OnPlayCard.Invoke(botPlayerId, cardIndex);
                    }
                };
            }
            opponentAreas[idx++] = oppView;
        }
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

    private void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    public void RefreshAll()
    {
        playerArea.Refresh();
        if (opponentAreas != null)
            foreach (var v in opponentAreas) v.Refresh();
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