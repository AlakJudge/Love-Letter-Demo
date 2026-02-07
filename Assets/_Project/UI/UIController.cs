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
    private TurnController turn;
    private RuleValidation rules;
    private EffectResolver resolver;

    private PlayerView playerArea;
    private OpponentView[] opponentAreas;
    private GuardChoiceView guardChoiceView;

    public GameController controller;

    public void Bind(GameState game, TurnController turn, RuleValidation rules, EffectResolver resolver)
    {
        this.game = game;
        this.turn = turn;
        this.rules = rules;
        this.resolver = resolver;

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
                    if (turn.TrySelectGuess(game, guessValue, rules, resolver, out var error))
                    {
                        turn.StartTurn(game);
                        controller?.UpdateUI();
                    }
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

                if (turn.TryPlayCard(game, card, rules, resolver, out var error))
                {
                    if (turn.Phase == TurnPhase.SelectTarget)
                    {
                        // Enable target selection
                        SetTargetingMode(true);
                    }
                    else
                    {
                        turn.StartTurn(game);
                    }
                    controller?.UpdateUI();
                }
            };
        }
        // Hook up local player as target (for debugging)
        if (playerArea != null)
        {
            playerArea.OnTargetSelected += targetId =>
            {
                if (turn.Phase != TurnPhase.SelectTarget) return;

                if (turn.TrySelectTarget(game, targetId, rules, resolver, out var error))
                {
                    SetTargetingMode(false);

                    if (turn.Phase == TurnPhase.SelectGuess)
                    {
                        guardChoiceView?.Show();
                    }
                    else
                    {
                        turn.StartTurn(game);
                    }
                    controller?.UpdateUI();
                }
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
                if (turn.Phase != TurnPhase.SelectTarget) return;

                if (turn.TrySelectTarget(game, targetId, rules, resolver, out var error))
                {
                    SetTargetingMode(false);
                    // Check if we need to show Guard choice
                    if (turn.Phase == TurnPhase.SelectGuess)
                    {
                        guardChoiceView?.Show();
                    }
                    else
                    {
                        turn.StartTurn(game);
                    }
                    controller?.UpdateUI();
                }
            };

            // Hook opponent hand clicks if manual control enabled
            if (oppView.handView != null)
            {
                oppView.handView.OnCardClicked += card =>
                {
                    if (!manualControlBots) return; // ignore if manual control disabled

                    int botPlayerId = oppView.GetPlayerId();                    
                    if (game.CurrentPlayer.id != botPlayerId) return;

                    if (turn.TryPlayCard(game, card, rules, resolver, out var error))
                    {
                        if (turn.Phase == TurnPhase.SelectTarget)
                            SetTargetingMode(true);
                        else
                        {
                            turn.StartTurn(game);
                        }
                        controller?.UpdateUI();
                    }
                };
            }
            opponentAreas[idx++] = oppView;
        }
    }

    public void DisableTargetingMode()
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
        playerArea?.Refresh();
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