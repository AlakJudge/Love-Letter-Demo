using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }
    public GameSetup gameSetup = new GameSetup();
    private enum SlotType
    {
        Empty = 0,
        Human = 1,
        Bot   = 2
    }

    [Header("Game Settings")]
    [Tooltip("Seconds of delay between bot actions (play card, select target, select guess).")]
    public float botDelay = 1;
    [Tooltip("Show opponents' hands in multiplayer (for testing)")]
    public bool showOpponentHands = false; 
    [Tooltip("If true, bots are manually controlled")]
    public bool manualControlBots = false; 
    [Tooltip("The deck cards and their order for this round")]
    public List<CardData> gameDeck = new(); 
    [Tooltip("The cards used to build the deck")]
    public List<CardData> deckTemplate = new(); 
    public List<string> playerNames = new();

    [Header("Animation")]
    [SerializeField] private CardEffectAnimationController cardEffectAnimationController;
    [SerializeField] private CardPlayAnimator cardPlayAnimator;    

    [Header("UI Objects")]
    public UIController ui;
    public TransitionView transitionView;
    public Button rematchButton;
    public Button quitButton;
    public Button restartButton;
    public Button infoButton;
    public Button fastModeButton;
    public Button deckButton;

    private GameState game;
    private TurnController turn;
    private RuleValidation rules;
    private int localPlayerId = 0;

    private bool isAnimatingCardPlay;
    private bool deferredUiRefresh; 
    private bool deferredTurnComplete;

    private PlayerState pendingRoundWinner;
    private PlayerState pendingGameWinner;

    private Coroutine botRoutine;

    // Networking
    private bool isMultiplayer = false;
    private PhotonView photonView;

    private void Awake()
    {
        Instance = this;
        photonView = GetComponent<PhotonView>();
        // If we have a cached GameSetup from the menu, use that instead of the default values set in the inspector
        if (RuntimeGameSetupCache.Current != null)
            gameSetup = RuntimeGameSetupCache.Current;
    }

    void Start()
    {
        InitializeGame();
        SetupUI();
        StartNewRoundNetworked();
    }

    private void InitializeGame()
    {
        List<PlayerState> players;

        if (PhotonNetwork.InRoom)
        {
            isMultiplayer = true;
            players = BuildPlayersFromOnlineLobby();
        }
        else
        {
            isMultiplayer = false;

            players = new List<PlayerState>();
            playerNames.Clear();

            // Check if we have a saved single player layout
            bool hasConfig = PlayerPrefs.HasKey("SP_Slot_0_Type");

            if (hasConfig)
            {
                string humanName = PlayerPrefs.GetString("PlayerName", "Player");
                int nextPlayerIndex = 0;
                localPlayerId = -1;
                
                // Check GameSetup and change number of players if it's manually set there
                if (gameSetup.enableCustomSetup && gameSetup.playerCount > 0 && gameSetup.playerCount <= 4)
                    gameSetup.AddBotsToGameConfig();
                else // Default to going through and checking 4 slots if not set and using the configured player count from lobby
                    gameSetup.playerCount = 4;

                for (int slotIndex = 0; slotIndex < gameSetup.playerCount; slotIndex++)
                {
                    int type = PlayerPrefs.GetInt($"SP_Slot_{slotIndex}_Type", 0); // 0 = Empty
                    if (type == 0)
                        continue;

                    bool isBot = type == 2;
                    string displayName;

                    if (isBot)
                    {
                        displayName = PlayerPrefs.GetString(
                            $"SP_Slot_{slotIndex}_BotName",
                            $"Bot {slotIndex + 1}"
                        );
                    }
                    else
                    {
                        displayName = humanName;
                        localPlayerId = nextPlayerIndex;
                    }

                    var playerState = new PlayerState(nextPlayerIndex, displayName, isBot: isBot);
                    players.Add(playerState);
                    playerNames.Add(displayName);

                    nextPlayerIndex++;
                }

                gameSetup.playerCount = players.Count;

                if (localPlayerId < 0)
                    localPlayerId = 0;
            }
            else
            { 
                // Fallback to original local setup
                players = new List<PlayerState>();
                for (int i = 0; i < gameSetup.playerCount; i++)
                {
                    bool isBot = i != localPlayerId;
                    string playerName = isBot ? $"Bot {i}" : $"Player {i+1}";
                    players.Add(new PlayerState(i, name: playerName, isBot: isBot));
                    playerNames.Add(playerName);
                }
            }
        }
        Debug.Log($"Created {gameSetup.playerCount} local players.");

        // Create GameState
        var deck = new List<CardData>(deckTemplate);
        game = new GameState(players, deck);
        turn = new TurnController();
        rules = new RuleValidation();

        // Wire TurnController log events into TurnLogger
        turn.OnLog += (message, turnNumber) =>
        {
            if (TurnLogger.Instance != null)
                TurnLogger.Instance.Log(message, turnNumber);
            else
                Debug.Log($"[Turn {turnNumber}] {message}");
        };

        // Subscribe to turn events
        turn.OnNeedTargetSelection += () => 
        {
            ui.EnableTargeting();
            if (ui.cancelCardSelectionButton != null)
                ui.cancelCardSelectionButton.gameObject.SetActive(true);
        };
                
        turn.OnNeedGuessSelection  += () => // Only show guard choice view when played by local player
        {
            var current = game.CurrentPlayer;
            if (current != null && current.id == ui.localPlayerId)
                ui.ShowGuardChoice();
        };
        turn.OnCardDrawn += (player, card) => StartCoroutine(AnimateLocalDraw(card));
        turn.OnRoundWin += OnRoundOver;
        turn.OnGameWin += OnGameOver;
        turn.OnTurnComplete += HandleTurnComplete;
        turn.OnCardEffectResolved += HandleCardEffectResolved;
        

        ui.OnRoundContinueClicked += () => StartNewRoundNetworked();

        ui.OnRematchClicked += () => RestartGame();
        ui.OnQuitClicked += () => QuitToMenu();

        // Only show fast mode button in single player
        if (fastModeButton != null)
            fastModeButton.gameObject.SetActive(!isMultiplayer);
    }

    private string TypeKey(int i)    => $"slot{i}_type";
    private string PlayerKey(int i)  => $"slot{i}_player";
    private string BotNameKey(int i) => $"slot{i}_botName";

    private List<PlayerState> BuildPlayersFromOnlineLobby()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null)
        {
            Debug.LogWarning("GameController: no Photon room, falling back to local players.");
            // fallback to 4 local players
            var fallback = new List<PlayerState>();
            for (int i = 0; i < gameSetup.playerCount; i++)
            {
                bool isBot = i != localPlayerId;
                string playerName = isBot ? $"Bot {i}" : $"Player {i+1}";
                // Offline: slot owner "0", bots for all but localPlayerId
                fallback.Add(new PlayerState(i, name: playerName, actorNumber: 0, isBot: isBot));
            }
            return fallback;
        }

        var props = room.CustomProperties;
        var players = new List<PlayerState>();
        playerNames.Clear();
        localPlayerId = -1;

        // Go through 4 slots in order. PlayerState.id == slotIndex
        for (int slotIndex = 0; slotIndex < 4; slotIndex++)
        {
            int type = (int)SlotType.Empty;
            if (props.TryGetValue(TypeKey(slotIndex), out var typeObj))
                type = (int)typeObj;

            if (type == (int)SlotType.Empty) // skip empty slots
                continue;

            PlayerState player;
            string displayName;

            if (type == (int)SlotType.Bot) // Bots
            {
                displayName = props.TryGetValue(BotNameKey(slotIndex), out var nameObj)
                    ? (string)nameObj
                    : $"Bot {slotIndex + 1}";

                player = new PlayerState(slotIndex, displayName, actorNumber: -1, isBot: true);
            }
            else // Humans
            {
                int actorNumber = -1;
                if (props.TryGetValue(PlayerKey(slotIndex), out var actorObj))
                    actorNumber = (int)actorObj;

                Player photonPlayer = null;
                foreach (var p in PhotonNetwork.PlayerList)
                {
                    if (p.ActorNumber == actorNumber)
                    {
                        photonPlayer = p;
                        break;
                    }
                }

                displayName = photonPlayer != null ? photonPlayer.NickName : $"Player {slotIndex + 1}";

                bool isLocal = photonPlayer != null &&
                            photonPlayer.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;

                if (isLocal)
                    localPlayerId = slotIndex;

                player = new PlayerState(slotIndex, displayName, actorNumber: actorNumber, isBot: false);
            }

            players.Add(player);
            playerNames.Add(displayName);
        }

        gameSetup.playerCount = players.Count;

        if (localPlayerId < 0)
            Debug.LogWarning("GameController: localPlayerId not found from lobby slots; defaulting to 0.");

        // Keep UI in sync
        ui.localPlayerId     = localPlayerId;
        ui.showOpponentHands = showOpponentHands;
        ui.manualControlBots = manualControlBots;

        Debug.Log($"Created {players.Count} ONLINE players; localPlayerId={localPlayerId}");

        return players;
    }
    
    // Wiring of game state to UI and input
    private void SetupUI()
    {
        ui.localPlayerId = localPlayerId;
        ui.showOpponentHands = showOpponentHands;
        ui.manualControlBots = manualControlBots;
        ui.Bind(game);

        // Bind animation controller
        if (cardEffectAnimationController != null)
            cardEffectAnimationController.Bind(ui, game, localPlayerId, ui.GetPlayerArea(), () => botDelay);

        // Handle card cancellation - reset phase to ChooseCard
        ui.OnPlayCardCancelled += () =>
        {
            if (turn.Phase == TurnPhase.SelectTarget || turn.Phase == TurnPhase.SelectGuess)
            {
                turn.ResetPhase();
                ui.RefreshAll();
            }
        };

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
            ProcessCommand(cmd);
        };
    }

    // NETWORKING ENTRY POINT
    private void ProcessCommand(PlayerCommand cmd)
    {
        if (!isMultiplayer)
        {
            // Local execution
            ExecuteCommand(cmd);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            // Host executes all commands
            ApplyCommandAsMaster(cmd);
        }
        else
        {
            // Client input: submit to master only
            photonView.RPC("Rpc_SubmitCommand", RpcTarget.MasterClient,
                        (int)cmd.type, cmd.playerId, cmd.cardIndex, cmd.targetPlayerId, cmd.guessValue);
        }
    }

    // Called locally
    public void ExecuteCommand(PlayerCommand cmd)
    {
        if (turn.ExecuteCommand(game, cmd, rules, out string error))
            if (isAnimatingCardPlay)
            {
                deferredUiRefresh = true;
            }
            else
            {
                if (!(turn.Phase == TurnPhase.SelectTarget || turn.Phase == TurnPhase.SelectGuess))
                {
                    ui.RefreshAll();
                }
            }
    }

    [PunRPC]
    private void Rpc_SubmitCommand(int type, int playerId, int cardIndex, int targetId, int guess)
    {
        // This runs only on the master (we always send to MasterClient)
        if (!PhotonNetwork.IsMasterClient)
            return;

        var cmd = new PlayerCommand
        {
            type           = (CommandType)type,
            playerId       = playerId,
            cardIndex      = cardIndex,
            targetPlayerId = targetId,
            guessValue     = guess
        };

        ApplyCommandAsMaster(cmd);
    }

    [PunRPC]
    private void Rpc_ApplyCommand(int type, int playerId, int cardIndex, int targetId, int guess)
    {
        // This runs on non‑masters
        if (PhotonNetwork.IsMasterClient)
            return;

        var cmd = new PlayerCommand
        {
            type           = (CommandType)type,
            playerId       = playerId,
            cardIndex      = cardIndex,
            targetPlayerId = targetId,
            guessValue     = guess
        };

        ExecuteCommand(cmd);
    }

    [PunRPC]
    private void Rpc_StartRound(int seed)
    {
        // Called on ALL clients (including master)
        turn.StartNewRound(game, deckTemplate, seed, gameSetup);
        BeginTurnForCurrentPlayer();
    }

    [PunRPC]
    private void Rpc_TriggerCardAnimation(int playerId, int targetId, int cardTypeInt, int guessValue)
    {
        // All clients play the animation
        var player = game.players[playerId];
        var target = targetId >= 0 ? game.players[targetId] : null;
        var card = player.discardPile.Count > 0 ? player.discardPile[player.discardPile.Count - 1] : null;
        
        if (card == null || (CardType)cardTypeInt != card.type)
        {
            // Fallback: reconstruct CardData from type
            card = deckTemplate.Find(c => c.type == (CardType)cardTypeInt);
        }

        // Sync guard guess state for animation
        if (card != null && card.type == CardType.Guard && target != null && guessValue > 0)
        {
            game.lastGuardSourcePlayerId = player.id;
            game.lastGuardTargetPlayerId = target.id;
            game.lastGuardGuessType = (CardType)guessValue;
            game.lastGuardGuessCorrect = game.lastGuardGuessType == target.hand[0].type;
        }
        
        if (card != null)
            StartCoroutine(HandleCardEffectResolvedRoutine(player, target, card));
    }
    
    private void ApplyCommandAsMaster(PlayerCommand cmd)
    {
        // Master updates its own state
        ExecuteCommand(cmd);

        // Then tell all others to apply the same command
        photonView.RPC("Rpc_ApplyCommand", RpcTarget.Others,
            (int)cmd.type, cmd.playerId, cmd.cardIndex, cmd.targetPlayerId, cmd.guessValue);
    }

    public void StartNewRoundNetworked()
    {
        // If a manual seed is set, use that instead of creating a new one
        int seed;
        if (gameSetup.enableCustomSetup && gameSetup.fixedSeed != 0)
        {
            seed = gameSetup.fixedSeed;
        }
        else
        {
            seed = Random.Range(int.MinValue, int.MaxValue);
        }
        game.seed = seed;

        if (!isMultiplayer)
        {
            // Offline / local game
            turn.StartNewRound(game, deckTemplate, seed, gameSetup);
            ui.UpdateSetupDiscards();
            BeginTurnForCurrentPlayer();
        }
        else
        {
            // Only master decides and broadcasts the seed
            if (!PhotonNetwork.IsMasterClient)
                return;

            photonView.RPC(nameof(Rpc_StartRound), RpcTarget.All, seed); 
        }      
        // Cache game deck to inspector
        foreach (var card in game.deck)
        {
            gameDeck.Add(card);
        } 
    }

    private void BeginTurnForCurrentPlayer()
    {
        // Activate fast mode if only bots are playing and all players have been elimited in current round
        if (game.CurrentPlayer.isBot && game.players.TrueForAll(p => p.isBot || p.isEliminated))
        {
            if (botDelay >= 0.5f)
            {
                ToggleFastMode();
            }
        }

        ui.RefreshAll(); // Ensure all players have cards in hand before the round starts and the first draw is made
        turn.StartTurn(game);
        
        var currentPlayer = game.CurrentPlayer;

        // If it's a bot (and not manually controlled), have it start its turn
        if (IsBot(currentPlayer) && !manualControlBots)
        {
            // Only master should drive bots
            if (isMultiplayer && !PhotonNetwork.IsMasterClient)
                return;
            
            if (botRoutine != null) StopCoroutine(botRoutine);
            botRoutine = StartCoroutine(RunBotTurn());
        }
    }

    private IEnumerator RunBotTurn()
    {
        yield return new WaitForSeconds(botDelay); // Delay between bot starts turn and plays card

        var botCommands = BotTurnController.GetTurnCommands(game, game.CurrentPlayer.id, rules);

        foreach (var cmd in botCommands)
        {
            ProcessCommand(cmd);
            yield return new WaitForSeconds(botDelay); // Delay between bot commands (e.g., play card, then select target, then select guess)
        }

        botRoutine = null;
    }
    private void HandleCardEffectResolved(PlayerState player, PlayerState target, CardData card)
    {
        if (ui == null)
            return;
        
        // In multiplayer, only master triggers animations for all
        if (isMultiplayer && PhotonNetwork.IsMasterClient)
        {
            int targetId = target != null ? target.id : -1;
            int guessValue = (card.type == CardType.Guard) ? (int)game.lastGuardGuessType : 0;

            photonView.RPC("Rpc_TriggerCardAnimation", RpcTarget.All,
                player.id, targetId, (int)card.type, guessValue);
        }
        else if (!isMultiplayer)
        {
            // Offline: run locally
            StartCoroutine(HandleCardEffectResolvedRoutine(player, target, card));
        }
    }
    private IEnumerator HandleCardEffectResolvedRoutine(PlayerState player, PlayerState target, CardData card)
    {
        isAnimatingCardPlay = true;

        try
        {
            // Clone the player and target objects (if exists)
            // This prevents issues with the card effect being resolved before the animation plays
            PlayerState playerClone = new PlayerState(player.id, player.name, player.actorNumber, player.isBot, new List<CardData>(player.hand))
            {
                isEliminated = player.isEliminated
            };
            PlayerState targetClone = null;
            if (target != null)
            {
                targetClone = new PlayerState(target.id, target.name, target.actorNumber, target.isBot, new List<CardData>(target.hand))
                {
                    isEliminated = target.isEliminated
                };
            }

            // Countess rule: skip fly-in if this play is illegal
            bool countessConflict =
                (card.type == CardType.Prince || card.type == CardType.King) &&
                playerClone.hand.Exists(c => c.type == CardType.Countess);

            // Skip fly-in if trying to play princess
            bool playingPrincess = card.type == CardType.Princess;

            if (countessConflict || playingPrincess)
            {
                // optionally still show the Countess warning UI
                yield return cardEffectAnimationController.ShowCardEffect(playerClone, targetClone, card);
            }
            else
            {
                yield return cardEffectAnimationController.AnimateCardPlay(playerClone, card, () 
                    => cardEffectAnimationController.ShowCardEffect(playerClone, targetClone, card));
            }

            // Only send the name of the card if they're the source or the target, otherwise just say they see a card.
            if (card.type == CardType.Spy && target != null)
            {
                var revealedCard = target.hand[0];
                if (Instance.IsLocalOwner(player) || Instance.IsLocalOwner(target))
                {
                    Debug.Log($"'{player.name}' spies on '{target.name}'s hand and sees a {revealedCard.type}");
                    TurnLogger.Instance.Log($"'{player.name}' spies on '{target.name}'s hand and sees a {revealedCard.type}", game.turnNumber);
                }            
                else
                {
                    Debug.Log($"'{player.name}' spies on '{target.name}'s hand and sees a card");
                    TurnLogger.Instance.Log($"'{player.name}' spies on '{target.name}'s hand and sees a card", game.turnNumber);
                }
            }
        }
        finally // Ensure no softlocks occur due to toggling fast mode during animation or other interruptions
        {
            isAnimatingCardPlay = false;
            TryProcessDeferredTurn();
        }
    }

    private void TryProcessDeferredTurn()
    {
        // First, apply any UI refresh that was held back while animating
        if (deferredUiRefresh)
        {
            deferredUiRefresh = false;
            ui.RefreshAll();
        }

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
        ui.ClearCardSelection();
        ui.DisableTargeting();
        ui.HideGuardChoice();
        game.AdvanceToNextPlayer();
        BeginTurnForCurrentPlayer();
    }

    private IEnumerator AnimateLocalDraw(CardData drawnCard)
    {
        if (cardPlayAnimator != null && ui != null && ui.deckCardView != null)
        {
            yield return cardPlayAnimator.DrawCardRoutine(
                ui.deckCardView, 
                drawnCard, 
                ui.GetPlayerContainer(game.CurrentPlayer.id)
                );
        }
        ui.RefreshAll(); // Ensure UI is updated after draw animation
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

    public bool IsLocalOwner(PlayerState p)
    {
        if (p == null) return false;
        if (p.isBot) return false;
        if (!PhotonNetwork.InRoom) return p.id == localPlayerId; // offline
        if (PhotonNetwork.LocalPlayer == null) return false; // safety check
        return p.actorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
    }
    private bool IsBot(PlayerState p) => p.isBot;

    public void ToggleInfoPanel()
    {
        if (ui != null)
            ui.ToggleInfoPanel();
    }

    public void ToggleFastMode()
    {
        bool isFast;
        
        if (botDelay < 0.5f)
        {
            // Restore default delay
            botDelay = 1f;
            fastModeButton.gameObject.GetComponent<Image>().color = Color.white;
            isFast = false;
        }
        else
        { 
            botDelay = 0.1f;
            fastModeButton.gameObject.GetComponent<Image>().color = Color.green;
            isFast = true;
        }
        // Also toggle card play animation speed
        if (cardPlayAnimator != null)
            cardPlayAnimator.ToggleFastMode(isFast);

        // Also toggle transition speed
        if (transitionView != null)
            transitionView.ToggleFastMode(isFast);
    }

    // Ensure the current game setup is cached so it can be accessed by the rematch button
    public static class RuntimeGameSetupCache
    {
        public static GameSetup Current;
    }

    public void RestartGame()
    {
        RuntimeGameSetupCache.Current = gameSetup;

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
    
    public void QuitToMenu()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }
    
}