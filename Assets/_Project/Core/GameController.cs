using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{
    [Header("Setup")]
    public int playerCount = 4;
    public int localPlayerId = 0;
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
    private EffectResolver resolver;

    void Start()
    {
        // Hide rematch and quit buttons initially
        if (rematchButton != null) rematchButton.gameObject.SetActive(false);
        if (quitButton != null) quitButton.gameObject.SetActive(false);

        // Build players
        var players = new List<PlayerState>();
        for (int i = 0; i < playerCount; i++) players.Add(new PlayerState(i));
        Debug.Log($"Created {playerCount} players.");

        var deck = new List<CardData>(deckTemplate);
        game = new GameState(players, deck);
        turn = new TurnController();
        rules = new RuleValidation();
        resolver = new EffectResolver();

        // Deal initial hands
        foreach (var p in game.players)
            if (game.deck.Count > 0) p.hand.Add(game.deck.Pop());
        Debug.Log("Dealt initial hands.");
        
        // Bind UI objects
        ui.controller = this;
        ui.localPlayerId = localPlayerId;
        ui.showOpponentHands = showOpponentHands;
        ui.manualControlBots = manualControlBots;
        ui.Bind(game, turn, rules, resolver);

        BuildPlayerObjects();
        
        if (transitionView == null)
            transitionView = FindFirstObjectByType<TransitionView>();

        // Ensure logger exists
        if (TurnLogger.Instance == null)
        {
            var loggerObj = new GameObject("TurnLogger");
            loggerObj.AddComponent<TurnLogger>();
        }

        // Start first round
        StartNewRound();
    }

    public void StartNewRound()
    {
        // Reset player states
        foreach (var player in game.players)
        {
            player.hand.Clear();
            player.discardPile.Clear();
            player.revealedCards.Clear();
            player.isProtected = false;
            player.isEliminated = false;
            // Tokens persist across rounds
        }
        // Reset turn controller state
        turn.pendingCard = null;
        turn.pendingTargetId = null;

        // Rebuild and shuffle deck
        game.deck.Clear();
        var newDeck = new List<CardData>(deckTemplate);
        Shuffle(newDeck);
        foreach (var card in newDeck)
            game.deck.Push(card);

        // Leave one card face down out of the game
        CardData removedCard = game.deck.Pop();
        Debug.Log($"Removed card: {removedCard.type}");

        // Deal initial hands
        foreach (var player in game.players)
        {
            if (game.deck.Count > 0)
                player.hand.Add(game.deck.Pop());
        }

        // Random starting player
        game.currentPlayerIndex = Random.Range(0, playerCount);

        game.turnNumber = 1;

        // Clear log and start logging
        TurnLogger.Instance?.Clear();
        TurnLogger.Instance?.Log($"New round started! Player {game.CurrentPlayer.id + 1} goes first.", 1);

        // Start first turn
        turn.StartTurn(game);
        // Avoid potential targeting issues if last turn in previous round was a targeting effect
        ui.DisableTargetingMode();
        
        ui.RefreshAll();
        SyncPlayerObjects();
    }

    public void OnRoundOver(PlayerState winner)
    {
        Debug.Log("OnRoundOver");
        if (transitionView == null) return;

        transitionView.OnTransitionFinished -= StartNewRound;
        transitionView.OnTransitionFinished += StartNewRound;       

        transitionView.ShowTransition(
            $"Player {winner.id + 1} wins the round!\n\nGet ready for the next round...",
            roundOrGameOver: "RoundOver"
            );
    }

    public void OnGameOver(PlayerState winner)
    {
        Debug.Log("OnGameOver");
        if (transitionView == null) return;

        transitionView.ShowTransition(
            $"Player {winner.id + 1} wins the game with {winner.tokens} tokens!\n\nClick below for a rematch.",
            roundOrGameOver: "GameOver"
            );
        
        // Show rematch and quit buttons
        if (rematchButton != null) rematchButton.gameObject.SetActive(true);
        if (quitButton != null) quitButton.gameObject.SetActive(true);
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

    private void SyncPlayerObjects()
    {
        if (playerManagers == null) return;
        foreach (var pm in playerManagers) pm?.Sync();
    }

    public void UpdateUI()
    {
        ui.RefreshAll();
        SyncPlayerObjects();
    }

    private void Shuffle(List<CardData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
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