using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [Header("Setup")]
    public int playerCount = 4;
    public int localPlayerId = 0;
    [Tooltip("Seconds of delay between bot actions (play card, select target, select guess).")]
    public float botDelay = 1;
    public List<CardData> deckTemplate = new(); 
    public List<string> playerNames = new();

    [Header("Debug")]
    public bool showOpponentHands = false; 
    public bool manualControlBots = false;
    
    [Header("Players")]
    public Transform playersList;             
    public PlayerManager playerManagerPrefab;

    [Header("UI Objects")]
    public UIController ui;
    public TransitionView transitionView;
    public Button rematchButton;
    public Button quitButton;
    public Button restartButton;

    private PlayerManager[] playerManagers;
    private GameState game;
    private TurnController turn;
    private RuleValidation rules;

    private bool isAnimatingCardPlay;
    private bool deferredUiRefresh; 
    private bool deferredTurnComplete;

    private PlayerState pendingRoundWinner;
    private PlayerState pendingGameWinner;

    private Coroutine botRoutine;

    // For future networking
    private bool isMultiplayer = false;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        InitializeGame();
        SetupUI();
        StartNewRound();
    }

    private void InitializeGame()
    {
        // Build players
        var players = new List<PlayerState>();
        for (int i = 0; i < playerCount; i++) 
        {
            var player = new PlayerState(i, i == localPlayerId);
            players.Add(player);
        }
        Debug.Log($"Created {playerCount} players.");

        var deck = new List<CardData>(deckTemplate);
        game = new GameState(players, deck);
        turn = new TurnController();
        rules = new RuleValidation();

        // Subscribe to turn events
        turn.OnNeedTargetSelection += () => ui.EnableTargeting();
        turn.OnNeedGuessSelection  += () => // Only show guard choice view when played by local player
        {
            var current = game.CurrentPlayer;
            if (current != null && current.id == ui.localPlayerId)
                ui.ShowGuardChoice();
        };
        turn.OnRoundWin += OnRoundOver;
        turn.OnGameWin += OnGameOver;
        turn.OnTurnComplete += HandleTurnComplete;
        turn.OnCardEffectResolved += HandleCardEffectResolved;

        ui.OnRoundContinueClicked += () => StartNewRound();

        ui.OnRematchClicked += () => RestartGame();
        ui.OnQuitClicked += () => QuitToMenu();

        BuildPlayerObjects();
        ui.playerManagers = playerManagers;
    }

    // Wiring of game state to UI and input
    private void SetupUI()
    {
        ui.localPlayerId = localPlayerId;
        ui.showOpponentHands = showOpponentHands;
        ui.manualControlBots = manualControlBots;
        ui.Bind(game);

        // Subscribe to UI input and convert to commands
        ui.OnPlayCard += (playerId, cardIndex) => 
        {
            var cmd = new PlayerCommand
            {
                type = CommandType.PlayCard,
                playerId = playerId,
                cardIndex = cardIndex,
                targetPlayerId = -1,
                guessValue = 0
            };
            ProcessCommand(cmd);
        };

        ui.OnSelectTarget += (playerId, targetId) => 
        {
            var cmd = new PlayerCommand
            {
                type = CommandType.SelectTarget,
                playerId = playerId,
                cardIndex = turn.pendingCardIndex,
                targetPlayerId = targetId,
                guessValue = 0
            };
            ProcessCommand(cmd);
        };

        ui.OnSelectGuess += (playerId, guess) => 
        {
            var cmd = new PlayerCommand
            {
                type = CommandType.SelectGuess,
                playerId = playerId,
                cardIndex = turn.pendingCardIndex,
                targetPlayerId = turn.pendingTargetId,
                guessValue = guess
            };
            game.lastGuardSourcePlayerId = playerId;
            game.lastGuardTargetPlayerId = turn.pendingTargetId;
            game.lastGuardGuessType = (CardType)guess;
            Debug.Log($"Player {playerId + 1} guessed Player {turn.pendingTargetId + 1} has a {(CardType)guess}");
            game.lastGuardGuessCorrect = game.lastGuardGuessType == game.players[turn.pendingTargetId].hand[0].type;
            ProcessCommand(cmd);
        };
    }

    // NETWORKING ENTRY POINT
    private void ProcessCommand(PlayerCommand cmd)
    {
        if (isMultiplayer)
        {
            // TODO: Send command to Photon server
            return;
        }

        // Local execution
        ExecuteCommand(cmd);
    }

    // Called locally OR when receiving network command
    public void ExecuteCommand(PlayerCommand cmd)
    {
        if (turn.ExecuteCommand(game, cmd, rules, out string error))
            if (isAnimatingCardPlay)
            {
                deferredUiRefresh = true;
            }
            else
            {
                ui.RefreshAll();
            }
    }

    private void BeginTurnForCurrentPlayer()
    {
        turn.StartTurn(game);
        ui.RefreshAll();

        var currentPlayer = game.CurrentPlayer;

        // If it's a bot (and not manually controlled), have it start its turn
        if (!currentPlayer.isLocalPlayer && !manualControlBots)
        {
            if (botRoutine != null) StopCoroutine(botRoutine);
            botRoutine = StartCoroutine(RunBotTurn());
        }
    }

    private IEnumerator RunBotTurn()
    {
        yield return new WaitForSeconds(botDelay); // Delay between bot starts turn and plays card

        var botCommands = BotController.GetTurnCommands(game, game.CurrentPlayer.id, rules);

        foreach (var cmd in botCommands)
        {
            ExecuteCommand(cmd);
            yield return new WaitForSeconds(botDelay); // Delay between bot commands (e.g., play card, then select target, then select guess)
        }

        botRoutine = null;
    }
    private void HandleCardEffectResolved(PlayerState player, PlayerState target, CardData card)
    {
        if (ui == null)
            return;
        
        StartCoroutine(HandleCardEffectResolvedRoutine(player, target, card));
    }
    private IEnumerator HandleCardEffectResolvedRoutine(PlayerState player, PlayerState target, CardData card)
    {
        isAnimatingCardPlay = true;

        // Clone the player and target objects (if exists)
        // This prevents issues with the card effect being resolved before the animation plays
        PlayerState playerClone = new PlayerState(player.id, player.isLocalPlayer, new List<CardData>(player.hand))
        {
            isEliminated = player.isEliminated
        };
        PlayerState targetClone = null;
        if (target != null)
        {
            targetClone = new PlayerState(target.id, target.isLocalPlayer, new List<CardData>(target.hand))
            {
                isEliminated = target.isEliminated
            };
        }

        // Countess rule: skip fly-in if this play is illegal
        bool countessConflict =
            (card.type == CardType.Prince || card.type == CardType.King) &&
            playerClone.hand.Exists(c => c.type == CardType.Countess);

        if (countessConflict)
        {
            // optionally still show the Countess warning UI
            yield return ui.ShowCardEffect(playerClone, targetClone, card);
        }
        else
        {
            yield return ui.AnimateCardPlay(playerClone, card, () => ui.ShowCardEffect(playerClone, targetClone, card));
        }
        isAnimatingCardPlay = false;
        TryProcessDeferredTurn();
    }

    private void TryProcessDeferredTurn()
    {
        // First, apply any UI refresh that was held back while animating
        if (deferredUiRefresh)
        {
            deferredUiRefresh = false;
            ui.RefreshAll();
        }

        // 
        if (!isAnimatingCardPlay)
        {
            if (pendingGameWinner != null)
            {
                var winner = pendingGameWinner;
                pendingGameWinner = null;
                ui.HandleGameWin(winner);
                return; // don't start another turn after game over
            }
            
            if (pendingRoundWinner != null)
            {
                var winner = pendingRoundWinner;
                pendingRoundWinner = null;
                ui.HandleRoundWin(winner);
                return; // don't start another turn after round over
            }
        }
        // Then, if a turn complete was deferred, process it now
        if (deferredTurnComplete && !isAnimatingCardPlay)
        {
            deferredTurnComplete = false;
            ProcessTurnComplete();
        }
    }

    private void HandleTurnComplete()
    {
        // If we're currently animating a card play, defer the turn complete processing until after the animation finishes
        if (isAnimatingCardPlay)
        {
            deferredTurnComplete = true;
            return;
        }
        ProcessTurnComplete();
    }

    private void ProcessTurnComplete()
    {
        ui.DisableTargeting();
        ui.HideGuardChoice();
        game.AdvanceToNextPlayer();
        BeginTurnForCurrentPlayer();
    }

    private void OnRoundOver(PlayerState winner)
    {
        if (isAnimatingCardPlay)
        {
            pendingRoundWinner = winner;
        }
        else
        {
            ui.HandleRoundWin(winner);
        }
    }

    private void OnGameOver(PlayerState winner)
    {
        if (isAnimatingCardPlay)
        {
            pendingGameWinner = winner;
        }
        else
        {
            ui.HandleGameWin(winner);
        }
    }

    public void StartNewRound()
    {
        turn.StartNewRound(game, deckTemplate);
        
        // Start first turn
        BeginTurnForCurrentPlayer();
    }

    private void BuildPlayerObjects()
    {
        if (playersList == null)
        {
            var list = new GameObject("PlayersList");
            playersList = list.transform;
        }

        // Clear old
        for (int i = playersList.childCount - 1; i >= 0; i--)
            Destroy(playersList.GetChild(i).gameObject);

        playerManagers = new PlayerManager[game.players.Count];

        for (int i = 0; i < game.players.Count; i++)
        {
            var obj = Instantiate(playerManagerPrefab, playersList);
            obj.name = $"Player_{i + 1}";
            var name = (i < playerNames.Count && !string.IsNullOrWhiteSpace(playerNames[i]))
                ? playerNames[i] : $"Player {i + 1}";
            obj.Bind(game.players[i], name);
            playerManagers[i] = obj;
        }
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
    
    public void QuitToMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }
    
}