using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    private enum SlotType
    {
        Empty = 0,
        Human = 1,
        Bot   = 2
    }

    [Header("Setup")]
    public int playerCount = 4;
    public int localPlayerId = 0;
    [Tooltip("Seconds of delay between bot actions (play card, select target, select guess).")]
    public float botDelay = 1;
    public List<CardData> deckTemplate = new(); 
    public List<string> playerNames = new();

    [Header("Animation")]
    [SerializeField] private CardEffectAnimationController cardEffectAnimationController;
    [SerializeField] private CardPlayAnimator cardPlayAnimator;
    
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
    public Button infoButton;
    public Button fastModeButton;

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

    // Networking
    private bool isMultiplayer = false;
    private PhotonView photonView;

    private void Awake()
    {
        Instance = this;
        photonView = GetComponent<PhotonView>();
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

            // Original local setup
            players = new List<PlayerState>();
            for (int i = 0; i < playerCount; i++)
            {
                bool isBot = i != localPlayerId;
                players.Add(new PlayerState(i, actorNumber: 0, isBot: isBot));
            }
            Debug.Log($"Created {playerCount} local players.");
        }

        // Create GameState + deck
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
        turn.OnRoundWin += OnRoundOver;
        turn.OnGameWin += OnGameOver;
        turn.OnTurnComplete += HandleTurnComplete;
        turn.OnCardEffectResolved += HandleCardEffectResolved;

        ui.OnRoundContinueClicked += () => StartNewRoundNetworked();

        ui.OnRematchClicked += () => RestartGame();
        ui.OnQuitClicked += () => QuitToMenu();

        BuildPlayerObjects();
        ui.playerManagers = playerManagers;
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
            for (int i = 0; i < playerCount; i++)
            {
                bool isBot = i != localPlayerId;
                // Offline: slot owner "0", bots for all but localPlayerId
                fallback.Add(new PlayerState(i, actorNumber: 0, isBot: isBot));
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

                player = new PlayerState(slotIndex, actorNumber: -1, isBot: true);
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

                player = new PlayerState(slotIndex, actorNumber: actorNumber, isBot: false);
            }

            players.Add(player);
            playerNames.Add(displayName);
        }

        playerCount = players.Count;

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
            cardEffectAnimationController.Bind(ui, game, localPlayerId, ui.GetPlayerArea());

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
        game.seed = seed;
        turn.StartNewRound(game, deckTemplate, seed);
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
        if (!isMultiplayer)
        {
            // Offline / local game
            int seed = Random.Range(int.MinValue, int.MaxValue);
            game.seed = seed;
            turn.StartNewRound(game, deckTemplate, seed);
            BeginTurnForCurrentPlayer();
            return;
        }

        // Only master decides and broadcasts the seed
        if (!PhotonNetwork.IsMasterClient)
            return;

        int newSeed = Random.Range(int.MinValue, int.MaxValue);
        photonView.RPC(nameof(Rpc_StartRound), RpcTarget.All, newSeed);
    }

    private void BeginTurnForCurrentPlayer()
    {
        turn.StartTurn(game);
        ui.RefreshAll();

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

        // Clone the player and target objects (if exists)
        // This prevents issues with the card effect being resolved before the animation plays
        PlayerState playerClone = new PlayerState(player.id, player.actorNumber, player.isBot, new List<CardData>(player.hand))
        {
            isEliminated = player.isEliminated
        };
        PlayerState targetClone = null;
        if (target != null)
        {
            targetClone = new PlayerState(target.id, target.actorNumber, target.isBot, new List<CardData>(target.hand))
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
        ui.ClearCardSelection();
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

    public void RestartGame()
    {
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