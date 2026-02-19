using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem.Users;
using BestoNet.Types;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using UnityEngine.Windows;
using System;
using UnityEngine.InputSystem.Composites;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameObject MainMenuScreen;

    public GameObject playerPrefab;
    public PlayerController[] players = new PlayerController[4];
    public int playerCount = 0;
    [NonSerialized]
    public ushort ramNeededToWinRound = 600;


    [NonSerialized]
    /// <summary>
    /// This matrix defines how much damage each player has done to a given player when said player dies, notably used for RAM payout.
    /// </summary>
    public byte[,] damageMatrix = new byte[,] //@jayesh, lemme know if this needs to be serialized
    {
        { 0, 0, 0, 0 }, // player 1 dies
        { 0, 0, 0, 0 }, // player 2 dies
        { 0, 0, 0, 0 }, // player 3 dies
        { 0, 0, 0, 0 }  // player 4 dies
    };

    public bool isRunning;
    public bool isSaved;

    public System.Random seededRandom;

    private DataManager dataManager;
    public TempSpellDisplay[] tempSpellDisplays = new TempSpellDisplay[4];
    public TempUIScript tempUI;
    public StageDataSO[] stages;
    public StageDataSO lobbySO;
    // public StageDataSO currentStage;
    public int currentStageIndex = 0;
    public SceneUiManager sceneManager;

    public List<GameObject> tempMapGOs = new List<GameObject>();
    public GameObject lobbyMapGO;

    [HideInInspector]
    public ShopManager shopManager;
    public OnboardManager onboardManager;

    public GO_Door goDoorPrefab;

    public bool roundOver;
    public bool gameOver;

    public bool prevSceneWasShop;
    public bool isTransitioning = false;

    public SpellCode_Gate[] gates = new SpellCode_Gate[4];

    //game timers
    public float roundEndTimer = 0f;
    public int roundEndTransitionTime = 2;
    public TextMeshProUGUI playerWinText;

    //main menu stuff (we will likely remove all of this later, its just a rehash of shop manager stuff)
    public bool playersChosenSpell;
    public Image p1_spellCard;
    public Image p2_spellCard;
    public Image p3_spellCard;
    public Image p4_spellCard;
    public GameObject[] floppyObjects;

    [SerializeField]
    private List<string> p1_choices;
    [SerializeField]
    private List<string> p2_choices;
    [SerializeField]
    private List<string> p3_choices;
    [SerializeField]
    private List<string> p4_choices;

    private int p1_index = 0;
    private int p2_index = 0;
    private int p3_index = 0;
    private int p4_index = 0;

    [Header("Online UI")]
    public GameObject onlineMenuUI;
    public KeyCode toggleOnlineMenuKey = KeyCode.F5;

    [Header("Online Match State")]
    public bool isWaitingForOpponent = false;
    public bool opponentIsReady = false;
    private float lobbyWaitStartTime = 0f;
    private float LOBBY_TIMEOUT = 30f;
    // Network health tracking (uses real time, not frames)
    private float lastPacketReceivedTime = 0f;
    private const float NETWORK_TIMEOUT = 10f;

    [Header("Input Management")]
    public PlayerInputManager playerInputManager;

    public string lastSceneName;

    // Add these fields to GameManager class
    private ulong cachedLocalInput = 5; // Stores input gathered in Update()
    private bool codePrevFrame = false;
    private bool jumpPrevFrame = false;
    private bool codeCurrentFrame = false;
    private bool jumpCurrentFrame = false;

    // New variables for Online Match State
    public int frameNumber { get; private set; } = 0; // Master frame counter
    public bool isOnlineMatchActive = false;
    private ulong localPlayerInput = 0; // Stores local input for the current frame
    private ulong[] syncedInput = new ulong[2] { 0, 0 }; // Inputs for both players this frame
    public int localPlayerIndex = 0; // Set this before starting online match
    public int remotePlayerIndex = 1; // Set this before starting online match
    private int timeoutFrames = 0; // Timeout counter
    private int randomSeed = 0;
    private int randomCallCount = 0;

    // Online lobby state tracking
    public bool localPlayerReadyForGameplay = false;
    public bool remotePlayerReadyForGameplay = false;
    [HideInInspector]
    public int p1_shopIndex = 0;
    [HideInInspector]
    public int p2_shopIndex = 0;

    private int p1_lastCycleFrame = -999;
    private int p2_lastCycleFrame = -999;
    private const int CYCLE_COOLDOWN_FRAMES = 15; // Prevent cycling for 15 frames (~0.25 seconds)

    private void Awake()
    {
        // if an instance already exists and it's not this one, destroy this duplicate
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            // otherwise, set this as the instance
            Instance = this;
            // optional: prevent the gameobject from being destroyed when loading new scenes
            DontDestroyOnLoad(gameObject);
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isOnlineMatchActive = false;
        isWaitingForOpponent = false;
        opponentIsReady = false;
        isTransitioning = false;
        frameNumber = 0;

        isRunning = true;
        isSaved = false;

        p1_spellCard.enabled = false;
        p2_spellCard.enabled = false;
        p3_spellCard.enabled = false;
        p4_spellCard.enabled = false;

        playerWinText.enabled = false;
        playerInputManager = GetComponent<PlayerInputManager>();
        dataManager = DataManager.Instance;

        //goDoorPrefab = GetComponentInChildren<GO_Door>();
        if (onlineMenuUI != null)
        {
            onlineMenuUI.SetActive(false);
        }

        seededRandom = new System.Random(UnityEngine.Random.Range(0, 10000));


        SetStage(-1);
        //StartCoroutine(End());

        //play a new main menu song
        //BGM_Manager.Instance.StartAndPlaySong();
    }

    // Update is called once per frame
    void Update()
    {
        //if (isOnlineMatchActive)
        //{
        //    cachedLocalInput = GatherInputForOnline();
        //}

        // Don't touch PlayerInputManager during online matches
        if (!isOnlineMatchActive)
        {
            gameObject.GetComponent<PlayerInputManager>().enabled = (SceneManager.GetActiveScene().name == "MainMenu");
        }
        else
        {
            // Keep it disabled during online matches
            if (playerInputManager != null && playerInputManager.enabled)
            {
                playerInputManager.enabled = false;
            }
        }


        //if ` is pressed, toggle box rendering
        if (UnityEngine.Input.GetKeyDown(KeyCode.BackQuote))
        {
            BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
        }

        //if = is pressed, player 1 win
        if (UnityEngine.Input.GetKeyDown(KeyCode.Equals))
        {
            players[0].roundRam = 600;
        }

        //remove player test key ","
        if (UnityEngine.Input.GetKeyDown(KeyCode.Comma)) { Destroy(players[0].gameObject); players[0] = null; playerCount--; }//players[0].inputs.InputDevice }

        if (!isOnlineMatchActive)
        {
            if (UnityEngine.Input.GetKeyDown(toggleOnlineMenuKey))
            {
                if (onlineMenuUI != null)
                {
                    // Toggle the online menu's visibility
                    bool isOnlineMenuVisible = !onlineMenuUI.activeSelf;
                    onlineMenuUI.SetActive(isOnlineMenuVisible);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        //if (prevSceneWasShop)
        //{
        //    ResetPlayers();
        //    prevSceneWasShop = false;
        //}

        if (isTransitioning) return;

        // ONLINE LOBBY WAIT STATE
        if (isOnlineMatchActive && isWaitingForOpponent)
        {
            // Check for lobby timeout
            float waitTime = UnityEngine.Time.unscaledTime - lobbyWaitStartTime;
            if (waitTime > LOBBY_TIMEOUT)
            {
                //Debug.LogError("Lobby timeout - opponent didn't join in time");
                StopMatch("Opponent failed to connect");
                // Return to menu or show error UI
                return;
            }
            return; // Don't run simulation yet
        }

        if (isOnlineMatchActive && isRunning)
        {
            if (!CheckNetworkHealth())
            {
                StopMatch("Network timeout - connection lost");
                return;
            }
        }

        if (isOnlineMatchActive)
        {
            // Execute the online frame logic using RollbackManager
            RunOnlineFrame();
        }
        else
        {
            // Execute the simple offline frame logic
            RunFrame();
        }

        // RENDER/UPDATE UI ONLY ON NON-ROLLBACK FRAMES
        if (!isOnlineMatchActive || (RollbackManager.Instance != null && !RollbackManager.Instance.isRollbackFrame))
        {
            AnimationManager.Instance.RenderGameState();

            // Refresh lobby UI alongside animation rendering
            if (isOnlineMatchActive)
            {
                RefreshLobbyUI();
            }
        }
    }

    private ulong GatherInputForOnline()
    {
        //if (players[localPlayerIndex] != null && players[localPlayerIndex].inputs.IsActive)
        //{
        //    var upVal = players[localPlayerIndex].inputs.UpAction?.ReadValue<float>() ?? 0f;
        //    var downVal = players[localPlayerIndex].inputs.DownAction?.ReadValue<float>() ?? 0f;
        //    var leftVal = players[localPlayerIndex].inputs.LeftAction?.ReadValue<float>() ?? 0f;
        //    var rightVal = players[localPlayerIndex].inputs.RightAction?.ReadValue<float>() ?? 0f;

        //    return players[localPlayerIndex].GetInputs();
        //    //if (upVal > 0.1f || downVal > 0.1f || leftVal > 0.1f || rightVal > 0.1f)
        //    //{
        //    //    return players[localPlayerIndex].GetInputs();
        //    //}
        //}
        return GatherRawInput();
    }

    private ulong GatherRawInput()
    {
        // Direction
        bool up = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow);
        bool down = UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow);
        bool left = UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow);
        bool right = UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow);

        // Buttons - sample current state
        bool codeNow = UnityEngine.Input.GetKey(KeyCode.R);
        bool jumpNow = UnityEngine.Input.GetKey(KeyCode.T);

        // Detect state transitions
        ButtonState codeState = GetButtonStateHelper(codePrevFrame, codeNow);
        ButtonState jumpState = GetButtonStateHelper(jumpPrevFrame, jumpNow);

        // Update for next frame - do this AFTER getting states
        codePrevFrame = codeNow;
        jumpPrevFrame = jumpNow;

        ButtonState[] buttons = new ButtonState[2] { codeState, jumpState };
        bool[] dirs = new bool[4] { up, down, left, right };

        return (ulong)InputConverter.ConvertToLong(buttons, dirs);
    }

    private ButtonState GetButtonStateHelper(bool previous, bool current)
    {
        if (!previous && !current)
            return ButtonState.None;
        else if (current && !previous)
            return ButtonState.Pressed;
        else if (current && previous)
            return ButtonState.Held;
        else
            return ButtonState.Released;
    }

    // Match Control Methods


    /// <summary>
    /// Initializes and starts an online match. Requires RollbackManager.
    /// </summary>
    /// <param name="localIndex">Player index (0 or 1) for this client.</param>
    /// <param name="remoteIndex">Player index (0 or 1) for the opponent.</param>
    public void StartOnlineMatch(int localIndex, int remoteIndex, Steamworks.SteamId opponentId)
    {
        //Debug.Log("Starting Online Match...");
        if (RollbackManager.Instance == null)
        {
            //Debug.LogError("Cannot start online match: RollbackManager not found!");
            return;
        }
        if (!opponentId.IsValid)
        {
            //Debug.LogError("Cannot start online match: Invalid Opponent SteamId provided!");
            return;
        }

        // Hide online menu immediately
        if (onlineMenuUI != null)
        {
            onlineMenuUI.SetActive(false);
            //Debug.Log("Online menu UI hidden");
        }

        isOnlineMatchActive = false;
        isWaitingForOpponent = false;
        opponentIsReady = false;
        isRunning = false;
        isTransitioning = false;
        localPlayerReadyForGameplay = false;
        remotePlayerReadyForGameplay = false;

        // Disable PlayerInputManager
        if (playerInputManager != null)
        {
            playerInputManager.DisableJoining();
            playerInputManager.enabled = false;
            //Debug.Log("PlayerInputManager disabled");
        }

        lobbyWaitStartTime = UnityEngine.Time.unscaledTime;
        lastPacketReceivedTime = 0f;

        localPlayerIndex = localIndex;
        remotePlayerIndex = remoteIndex;
        ResetMatchState();

        ClearPlayerObjects();
        this.playerCount = 2;

        if (playerPrefab == null)
        {
            //Debug.LogError("Player Prefab is not assigned in GameManager Inspector!");
            return;
        }

        // Create players but don't start simulation
        for (int i = 0; i < 2; i++)
        {
            GameObject p = Instantiate(playerPrefab);
            players[i] = p.GetComponent<PlayerController>();
            AnimationManager.Instance.InitializePlayerVisuals(players[i], i);

            if (players[i].playerNum != null)
            {
                players[i].playerNum.text = "P" + (i + 1);
            }

            var pInput = p.GetComponent<UnityEngine.InputSystem.PlayerInput>();

            if (i == remotePlayerIndex)
            {
                if (pInput != null)
                {
                    pInput.DeactivateInput();
                    pInput.enabled = false;
                }
                players[i].CheckForInputs(false);
            }
            else if (i == localIndex)
            {
                if (pInput != null)
                {
                    pInput.ActivateInput();
                    if (pInput.user.valid && UnityEngine.InputSystem.Keyboard.current != null)
                    {
                        UnityEngine.InputSystem.Users.InputUser.PerformPairingWithDevice(
                            UnityEngine.InputSystem.Keyboard.current,
                            pInput.user
                        );
                    }
                }
                players[i].CheckForInputs(true);
            }
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                players[i].InitCharacter();
            }
        }

        // Initialize managers
        RollbackManager.Instance.Init(opponentId.Value);

        if (MatchMessageManager.Instance != null)
        {
            MatchMessageManager.Instance.StartMatch(opponentId);
            // Send ready signal to opponent
            MatchMessageManager.Instance.SendReadySignal();
        }
        else
        {
            //Debug.LogError("MatchMessageManager not found during StartOnlineMatch!");
        }

        // Set up online state but DON'T start simulation yet
        isOnlineMatchActive = true;
        isWaitingForOpponent = true; // Enter lobby wait state

        ProjectileManager.Instance.InitializeAllProjectiles();

        SetStage(-1); // Lobby stage
        ResetPlayers();

        isRunning = true;

        //Debug.Log($"Entered Online Lobby - Waiting for opponent... LocalPlayer={localPlayerIndex}");
    }

    public void OnPacketReceived()
    {
        lastPacketReceivedTime = UnityEngine.Time.unscaledTime;
    }

    private bool CheckNetworkHealth()
    {
        // Don't check during lobby phase
        if (isWaitingForOpponent)
            return true;

        // If we haven't received ANY packets yet, give it more time
        if (lastPacketReceivedTime == 0f)
        {
            // Give 15 seconds for initial connection
            if (UnityEngine.Time.unscaledTime - lobbyWaitStartTime > 15f)
            {
                //Debug.LogError("Network timeout - no packets received after 15 seconds");
                return false;
            }
            return true;
        }

        // Check time since last packet
        float timeSinceLastPacket = UnityEngine.Time.unscaledTime - lastPacketReceivedTime;

        if (timeSinceLastPacket > NETWORK_TIMEOUT)
        {
            //Debug.LogError($"Network timeout - no packets for {timeSinceLastPacket:F1} seconds");
            return false;
        }

        // Warn if connection is getting laggy
        if (timeSinceLastPacket > 3f && Mathf.FloorToInt(timeSinceLastPacket) % 1 == 0)
        {
            //Debug.LogWarning($"Network lag - no packets for {timeSinceLastPacket:F1} seconds");
        }

        return true;
    }

    public void OnOpponentReady()
    {
        //Debug.Log("Received opponent ready signal");

        if (!isOnlineMatchActive)
        {
            //Debug.LogWarning("Received ready signal but not in online match state - ignoring");
            return;
        }

        if (!isWaitingForOpponent)
        {
            //Debug.LogWarning("Received ready signal but already started - ignoring");
            return;
        }

        opponentIsReady = true;
        StartLobbySimulation();
    }

    private void StartLobbySimulation()
    {
        if (onlineMenuUI != null)
        {
            onlineMenuUI.SetActive(false);
        }

        if (onboardManager != null)
        {
            onboardManager.gameObject.SetActive(false);
        }

        // Double-check we're in the right state
        if (!isWaitingForOpponent)
        {
            //Debug.LogWarning("StartLobbySimulation called but not waiting - aborting");
            return;
        }

        isWaitingForOpponent = false;
        lastPacketReceivedTime = UnityEngine.Time.unscaledTime;

        // Send match start confirmation
        if (MatchMessageManager.Instance != null)
        {
            MatchMessageManager.Instance.SendMatchStartConfirm();
        }

        ProjectileManager.Instance.InitializeAllProjectiles();
        frameNumber = 0;
        isRunning = true;

        int testValue = GetNextRandom(0,100);
        Debug.Log($"[SYNC CHECK] seededRandom test value: {testValue} | randomCallCount: {randomCallCount}");
    }

    // Send lobby ready signal
    public void SendLobbyReadyForGameplay()
    {
        if (!isOnlineMatchActive || MatchMessageManager.Instance == null)
            return;

        localPlayerReadyForGameplay = true;
        //Debug.Log("Local player ready for gameplay transition - sending signal");

        // Send via MatchMessageManager
        MatchMessageManager.Instance.SendLobbyReadySignal();

        CheckBothPlayersReadyForGameplay();
    }

    // Receive lobby ready signal
    public void OnOpponentReadyForGameplay()
    {
        //Debug.Log("Opponent is ready for gameplay transition");
        remotePlayerReadyForGameplay = true;
        CheckBothPlayersReadyForGameplay();
    }

    // Check if both players are ready to transition
    private void CheckBothPlayersReadyForGameplay()
    {
        if (localPlayerReadyForGameplay && remotePlayerReadyForGameplay)
        {
            //Debug.Log("Both players ready - transitioning to Gameplay");
            isTransitioning = true;
            LoadRandomGameplayStage();
        }
    }

    /// <summary>
    /// Stops the currently running match (local or online).
    /// </summary>
    /// <param name="reason">Reason for stopping.</param>
    public void StopMatch(string reason = "Match Ended")
    {
        //Debug.Log($"Stopping Match: {reason}");

        isRunning = false;

        if (isOnlineMatchActive)
        {
            //Debug.Log("Cleaning up online match state...");

            // Clean up rollback manager
            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.Disconnect();
            }

            // Clean up match message manager
            if (MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.StopMatch();
            }

            // Clear online flags
            isOnlineMatchActive = false;
            isWaitingForOpponent = false;
            opponentIsReady = false;
            isTransitioning = false;
            localPlayerReadyForGameplay = false;
            remotePlayerReadyForGameplay = false;

            // Reset frame counter
            frameNumber = 0;

            // Clear online player objects
            ClearPlayerObjects();

            // Re-enable PlayerInputManager for offline play
            if (playerInputManager != null)
            {
                playerInputManager.enabled = true;
                playerInputManager.EnableJoining();
                //Debug.Log("PlayerInputManager re-enabled for offline play");
            }
        }

        // General cleanup
        ProjectileManager.Instance.DeleteAllProjectiles();

        //Debug.Log("Match stopped and state reset");
    }

    private void ClearPlayerObjects()
    {
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                var inputComp = players[i].GetComponent<UnityEngine.InputSystem.PlayerInput>();
                if (inputComp != null)
                {
                    inputComp.DeactivateInput();
                    inputComp.enabled = false;
                }
                Destroy(players[i].gameObject);
                players[i] = null;
            }
        }
        playerCount = 0;
    }

    /// <summary>
    /// Resets common match state variables.
    /// </summary>
    private void ResetMatchState()
    {
        frameNumber = 0;
        localPlayerInput = 0;
        syncedInput = new ulong[2] { 0, 0 };
        timeoutFrames = 0;
    }

    // Call this to sync UI with game state (only called on non-rollback frames from FixedUpdate)
    public void RefreshLobbyUI()
    {
        if (!isOnlineMatchActive) return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "MainMenu") return;

        // Sync spell selection UI for both players
        for (int i = 0; i < 2; i++)
        {
            if (players[i] == null) continue;

            List<string> choices = i == 0 ? p1_choices : p2_choices;
            int currentIndex = i == 0 ? p1_index : p2_index;
            Image spellCard = i == 0 ? p1_spellCard : p2_spellCard;

            if (spellCard == null) continue;

            // If player spawned but hasn't chosen yet - show current selection
            if (players[i].isSpawned && !players[i].chosenStartingSpell)
            {
                if (choices != null && choices.Count > 0 && currentIndex >= 0 && currentIndex < choices.Count)
                {
                    // Update sprite to match current synced index
                    spellCard.sprite = SpellDictionary.Instance.spellDict[choices[currentIndex]].shopSprite;
                    spellCard.enabled = true;
                }
            }
            // If player has chosen - hide the card
            else if (players[i].chosenStartingSpell)
            {
                spellCard.enabled = false;
            }
            // If player not spawned yet - ensure card is disabled
            else
            {
                spellCard.enabled = false;
            }
        }
    }

    public void UpdateSceneLogic(ulong[] inputs)
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.name == "Shop")
        {
            if (shopManager == null)
            {
                shopManager = FindAnyObjectByType<ShopManager>();
            }
            if (shopManager != null)
            {
                shopManager.ShopUpdate(inputs);
            }
        }
        else if (activeScene.name == "MainMenu")
        {
            HandleOnlineSpellSelection();
            goDoorPrefab?.CheckOpenDoor();
        }
    }

    /// <summary>
    /// Executes one frame of the online match simulation using RollbackManager.
    /// </summary>
    private void RunOnlineFrame()
    {
        RollbackManager rbManager = RollbackManager.Instance;
        if (rbManager == null) return;


        if (frameNumber <= rbManager.InputDelay)
        {
            rbManager.SaveState();
        }

        localPlayerInput = GatherInputForOnline();
        //codePrevFrame = codeCurrentFrame;
        //jumpPrevFrame = jumpCurrentFrame;

        timeoutFrames = 0;
        rbManager.RollbackEvent();

        frameNumber++;
        rbManager.SendLocalInput(localPlayerInput);
        syncedInput = rbManager.SynchronizeInput();

        if (!rbManager.AllowUpdate())
        {
            frameNumber--;
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.name == "Shop")
        {
            // Only run non-rollback shop logic if needed, or ensure ShopUpdate is deterministic
            // Assuming ShopUpdate relies on inputs and needs to be deterministic:
            if (shopManager == null)
            {
                shopManager = FindAnyObjectByType<ShopManager>();
            }

            if (shopManager != null)
            {
                // Need to pass the SYNCHRONIZED inputs to the shop so both players buy/select the same things
                // Assuming ShopUpdate accepts ulong[] or casting is handled
                ulong[] shopInputs = new ulong[playerCount];
                for (int i = 0; i < playerCount; i++) shopInputs[i] = syncedInput[i];

                shopManager.ShopUpdate(shopInputs); // Using Synced Inputs
            }
        }
        else
        {
            shopManager = null;
        }

        UpdateGameState(syncedInput);

        UpdateSceneLogic(syncedInput);

        // ONLINE LOBBY LOGIC (MainMenu scene)
        if (activeScene.name == "MainMenu")
        {
            // Handle spell selection for online players (only local and remote)
            HandleOnlineSpellSelection();

            // Check gates and door - same logic as offline
            goDoorPrefab.CheckOpenDoor();

            if (goDoorPrefab.CheckAllPlayersReady())
            {
                // In online mode, signal readiness instead of immediately transitioning
                if (!localPlayerReadyForGameplay)
                {
                    SendLobbyReadyForGameplay();
                }
            }
        }
        else if (activeScene.name == "Gameplay")
        {
            // Only check end conditions if NOT rolling back
            if (!rbManager.isRollbackFrame)
            {
                if (CheckDeathsAndRoundEnd(GetActivePlayerControllers()))
                {
                    if (!roundOver)
                    {
                        // Determine winner
                        ushort highestRam = 0;
                        PlayerController winner = null;
                        for (int i = 0; i < playerCount; i++)
                        {
                            if (players[i].roundRam >= ramNeededToWinRound)
                            {
                                if (players[i].roundRam > highestRam)
                                {
                                    winner = players[i];
                                    highestRam = players[i].roundRam;
                                }
                            }
                        }

                        if (winner != null)
                        {
                            winner.roundsWon += 1;
                            roundOver = true;

                            // Show win text (optional - might look weird in online with lag)
                            // playerWinText.enabled = true;
                            // playerWinText.text = "Player " + (winner.pID) + " wins the match!";

                            // Reset roundRam for all players
                            for (int i = 0; i < playerCount; i++)
                            {
                                players[i].roundRam = 0;
                                players[i].playerNum.enabled = false;
                                players[i].inputDisplay.enabled = false;
                                if (players[i].roundsWon >= 3) { gameOver = true; }
                            }
                        }
                    }

                    // Handle round end timer (deterministic)
                    if (roundEndTransitionTime >= roundEndTimer)
                    {
                        roundEndTimer += Time.fixedDeltaTime; // Use fixedDeltaTime for determinism
                    }

                    // Transition after timer
                    if (roundEndTransitionTime <= roundEndTimer)
                    {
                        ClearStages();

                        if (gameOver)
                        {
                            // playerWinText.enabled = false;
                            dataManager.totalRoundsPlayed += 1;
                            GameEnd();
                            roundEndTimer = 0;
                        }
                        else if (players[0].spellList.Count >= 6)
                        {
                            // Max spells reached - skip shop, go to next gameplay
                            // playerWinText.enabled = false;
                            dataManager.totalRoundsPlayed += 1;

                            // Need to reset ready flags and set transitioning
                            localPlayerReadyForGameplay = false;
                            remotePlayerReadyForGameplay = false;
                            isTransitioning = true;

                            LoadRandomGameplayStage();
                            foreach (PlayerController player in players)
                            {
                                if (player != null)
                                    player.inputDisplay.enabled = true;
                            }
                            roundEndTimer = 0;
                            roundOver = false;
                        }
                        else
                        {
                            // Normal round end - go to shop
                            // playerWinText.enabled = false;
                            dataManager.totalRoundsPlayed += 1;
                            RoundEnd();
                            roundEndTimer = 0;
                            roundOver = false;
                        }
                    }
                }
            }
        }

        if (!rbManager.isRollbackFrame && !rbManager.DelayBased)
        {
            rbManager.SaveState();
        }

        // ALWAYS refresh UI on non-rollback frames to sync with game state
        // RefreshLobbyUI();
    }

    // Handle spell selection for online players (GAME STATE ONLY)
    public void HandleOnlineSpellSelection()
    {
        for (int i = 0; i < 2; i++)
        {
            if (players[i] == null) continue;

            if (!players[i].isSpawned)
            {
                GenerateStartingSpells(i);
                players[i].isSpawned = true;
            }

            if (!players[i].chosenStartingSpell && players[i].isSpawned)
            {
                List<string> choices = i == 0 ? p1_choices : p2_choices;
                int currentIndex = i == 0 ? p1_index : p2_index;

                int lastCycleFrame = i == 0 ? p1_lastCycleFrame : p2_lastCycleFrame;

                InputSnapshot snapshot = InputConverter.ConvertFromLong(syncedInput[i]);

                // Cycle spells - ONLY if button is PRESSED and cooldown has passed
                if (snapshot.ButtonStates[0] == ButtonState.Pressed &&
                    (frameNumber - lastCycleFrame) >= CYCLE_COOLDOWN_FRAMES)
                {
                    //Debug.Log($"[SYNCED] p{i + 1} pressed cycle spell (current index: {currentIndex})");

                    currentIndex = (currentIndex + 1) % choices.Count;

                    if (i == 0)
                    {
                        p1_index = currentIndex;
                        p1_lastCycleFrame = frameNumber;
                    }
                    else
                    {
                        p2_index = currentIndex;
                        p2_lastCycleFrame = frameNumber;
                    }

                    //Debug.Log($"[SYNCED] p{i + 1} new index: {currentIndex}, spell: {choices[currentIndex]}");
                }

                // Choose spell
                if (snapshot.ButtonStates[1] == ButtonState.Pressed)
                {
                    //Debug.Log($"[SYNCED] p{i + 1} chose spell: {choices[currentIndex]}");

                    players[i].startingSpell = choices[currentIndex];
                    players[i].chosenStartingSpell = true;

                    if (!players[i].startingSpellAdded)
                    {
                        players[i].AddSpellToSpellList(choices[currentIndex]);
                        players[i].startingSpellAdded = true;
                        //Debug.Log($"[SYNCED] Added starting spell to player {i}: {choices[currentIndex]}");
                    }
                }
            }
        }
    }

    public void ForceSetFrame(int newFrame)
    {
        this.frameNumber = newFrame;
    }


    /// <summary>
    /// Runs a single frame of the game.
    /// </summary>
    protected void RunFrame()
    {
        //if (!isRunning)
        //    return;
        if (playerInputManager != null)
        {
            playerInputManager.enabled = true;
        }

        ulong[] inputs = new ulong[playerCount];
        for (int i = 0; i < inputs.Length; ++i)
        {
            inputs[i] = players[i].GetInputs();
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "MainMenu") { playerInputManager.DisableJoining(); }
        else { playerInputManager.EnableJoining(); }

        if (activeScene.name == "End")
        {
            for (int i = 0; i < inputs.Length; ++i)
            {
                InputSnapshot inputSnap = InputConverter.ConvertFromLong(inputs[i]);
                if ((inputSnap.ButtonStates[0] is ButtonState.Pressed or ButtonState.Held)
                    || (inputSnap.ButtonStates[1] is ButtonState.Pressed or ButtonState.Held))
                {
                    sceneManager.Restart();
                    //RestartGame();
                    return;
                }
            }
        }
        ///shop specific update
        if (activeScene.name == "Shop")
        {
            if (shopManager == null)
            {
                shopManager = FindAnyObjectByType<ShopManager>();
            }
            shopManager.ShopUpdate(inputs);
        }
        else
        {
            shopManager = null;
        }

        ///onboard manager specific update
        if (activeScene.name == "MainMenu")
        {
            if (onboardManager == null)
            {
                onboardManager = FindAnyObjectByType<OnboardManager>();
            }
            onboardManager.OnboardUpdate(inputs);
        }
        else
        {
            onboardManager = null;
        }


        //if the game is not running, skip the update (everything after this uses player controller updates)
        if (!isRunning)
            return;



        UpdateGameState(inputs);

        if (activeScene.name == "MainMenu")
        {
            //if (lastSceneName == "End")
            //{
            //for (int i = 0; i < gates.Length; i++)
            //{
            //    gates[i].SetOpen(true);
            //}

            //if (onlineMenuUI != null)
            //{
            //    onlineMenuUI.SetActive(false);
            //}
            //}

            goDoorPrefab.CheckOpenDoor();

            if (goDoorPrefab.CheckAllPlayersReady())
            {
                LoadRandomGameplayStage();
            }

            if (players[0] != null)
            {
                SetMenuActive(false);
            }
        }

        else if (activeScene.name == "Gameplay")
        {
            if (CheckDeathsAndRoundEnd(GetActivePlayerControllers()))
            {
                
                if (!roundOver)
                {
                    ushort highestRam = 0;
                    PlayerController winner = null;
                    for (int i = 0; i < playerCount; i++)
                    {
                        if (players[i].roundRam >= ramNeededToWinRound)
                        {
                            if (players[i].roundRam > highestRam)
                            {
                                winner = players[i];
                                highestRam = players[i].roundRam;
                            }
                        }
                    }

                    winner.roundsWon += 1;
                    roundOver = true;
                    playerWinText.enabled = true;
                    playerWinText.text = "Player " + (winner.pID) + " wins the match!";

                    for (int i = 0; i < playerCount; i++)
                    {
                        players[i].roundRam = 0;
                        players[i].playerNum.enabled = false;
                        players[i].inputDisplay.enabled = false;
                        if (players[i].roundsWon >= 3) { gameOver = true; }
                    }
                }

                

                if (roundEndTransitionTime >= roundEndTimer)
                {
                    roundEndTimer += Time.deltaTime;
                }

                //Game end logic here
                if (roundEndTransitionTime <= roundEndTimer)
                {
                    ClearStages();
                    if (gameOver)
                    {
                        playerWinText.enabled = false;
                        dataManager.totalRoundsPlayed += 1;
                        GameEnd();
                        //Debug.Log(roundEndTimer);
                        roundEndTimer = 0;
                    }
                    else if (players[0].spellList.Count >= 6)
                    {
                        playerWinText.enabled = false;
                        dataManager.totalRoundsPlayed += 1;
                        LoadRandomGameplayStage();
                        foreach (PlayerController player in players) { player.inputDisplay.enabled = true; }
                        //Debug.Log(roundEndTimer);
                        roundEndTimer = 0;
                    }
                    else
                    {
                        playerWinText.enabled = false;
                        dataManager.totalRoundsPlayed += 1;
                        RoundEnd();
                        //Debug.Log(roundEndTimer);
                        roundEndTimer = 0;
                        roundOver = false;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Updates the game state based on the provided inputs.
    /// </summary>
    /// <param name="inputs">Array of inputs for each player.</param>
    public void UpdateGameState(ulong[] inputs)
    {
        ProjectileManager.Instance.UpdateProjectiles();
        HitboxManager.Instance.ProcessCollisions();

        //update each player update values
        for (int i = 0; i < playerCount; i++)
        {
            players[i].PlayerUpdate((ulong)inputs[i]);
        }

        for (int i = 0; i < playerCount; i++)
        {
            if (players[i].isAlive)
            {
                players[i].ProcEffectUpdate();
            }
        }
    }

    //gets called everytime a new player enters, recreates player array
    public void GetPlayerControllers(PlayerInput playerInput)
    {
        if (isOnlineMatchActive)
        {
            //Debug.Log("GetPlayerControllers called but online match active - ignoring");
            return;
        }

        // Check if this player is already registered
        PlayerController existingPlayer = playerInput.GetComponent<PlayerController>();
        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] == existingPlayer)
            {
                //Debug.LogWarning($"Player {existingPlayer.name} already registered at index {i} - ignoring duplicate registration");
                return; // Already registered, don't add again!
            }
        }

        //Debug.Log($"[GetPlayerControllers] Adding new player. Current playerCount={playerCount}");

        players[playerCount] = existingPlayer;
        players[playerCount].inputs.AssignInputDevice(playerInput.devices[0]);
        AnimationManager.Instance.InitializePlayerVisuals(players[playerCount], playerCount);

        // INCREMENT FIRST
        playerCount++;

        // Update ALL player numbers
        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] != null && players[i].playerNum != null)
            {
                players[i].playerNum.text = "P" + (i + 1);
            }
        }

        //Debug.Log($"[GetPlayerControllers] Player added. New playerCount={playerCount}");
    }

    public void UpdatePlayerBounties()
    {
        ushort averageTotalRam = 0;
        for (int i = 0; i < playerCount; i++)
        {
            averageTotalRam += players[i].totalRam;
        }
        averageTotalRam = (ushort)((float)averageTotalRam / (float)playerCount);

        for (int i = 0; i < playerCount; i++)
        {
            players[i].ramBounty = (short)((float)(players[i].totalRam - averageTotalRam)/2);
        }
    }

    public bool CheckDeathsAndRoundEnd(PlayerController[] playerControllers)
    {

        if(roundOver) { return true; }

        foreach (PlayerController player in playerControllers)
        {
            //check for player deaths
            if(!player.isAlive)
            {

                //go through each player and award them ram based on the percentage of the other player's health they took (damage matrix)
                foreach (PlayerController p in playerControllers)
                {
                    ushort bountyCut = (ushort)(((float)damageMatrix[player.pID - 1, p.pID - 1]/100) * (float)player.ramBounty);
                    float totalRamEarned = (damageMatrix[player.pID - 1, p.pID - 1]/100f) * PlayerController.baseRamLifeWorth + bountyCut;
                    p.roundRam += (ushort)totalRamEarned;
                    p.totalRam += (ushort)totalRamEarned;

                    damageMatrix[player.pID - 1, p.pID - 1] = 0; //reset damage matrix for next death
                }

                UpdatePlayerBounties();

                //respawn the dead player
                player.SpawnPlayer(GetRandomSpawnVec2());
            }
        }

        //then check winner conditions (most ram at the end of the round)
        foreach (PlayerController player in playerControllers)
        {
            if (player.roundRam >= ramNeededToWinRound)
            {
                return true;
            }
        }


        return false;
    }

    //reset players after each round
    public void ResetPlayers()
    {
        FixedVec2[] spawnPos = GetSpawnPositions()
            .Select(v => FixedVec2.FromFloat(v.x, v.y))
            .ToArray();
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                players[i].basicsFired = 0;
                players[i].spellsFired = 0;
                players[i].spellsHit = 0;
                players[i].times = new List<Fixed>();
                players[i].isAlive = true;
                players[i].SpawnPlayer(spawnPos[i]);
            }
        }

        isSaved = false;
    }

    /// <summary>
    /// Restart gamestate when "play" or "rematch" is pressed
    /// </summary>
    public void RestartGame()
    {
        gameOver = false;
        Vector2[] spawnPositions = GetSpawnPositions();
        // Convert spawn positions to FixedVec2
        FixedVec2[] fixedSpawnPositions = spawnPositions
            .Select(v => FixedVec2.FromFloat(v.x, v.y))
            .ToArray();
        //reset each player to their starting values
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                //this is different from ResetPlayers()
                players[i].ResetPlayer();
                players[i].SpawnPlayer(fixedSpawnPositions[i]);
                players[i].inputDisplay.enabled = true;
                players[i].playerNum.enabled = true;
            }
        }
    }

    /// <summary>
    /// Restarts the game from the lobby, not just a rematch
    /// </summary>
    public void RestartLobby()
    {
        gameOver = false;
        playerCount = 0;

        SetMenuActive(true);

        if (onlineMenuUI != null)
        {
            onlineMenuUI.SetActive(false);
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                Destroy(players[i].gameObject);
                players[i] = null;
            }
        }
    }

    public Vector2[] GetSpawnPositions()
    {
        if (currentStageIndex < 0)
        {
            return new Vector2[] {
                lobbySO.playerSpawnTransform[0],
                lobbySO.playerSpawnTransform[1],
                lobbySO.playerSpawnTransform[2],
                lobbySO.playerSpawnTransform[3]};
        }
        else
        {
            return new Vector2[] {
                stages[currentStageIndex].playerSpawnTransform[0],
                stages[currentStageIndex].playerSpawnTransform[1],
                stages[currentStageIndex].playerSpawnTransform[2],
                stages[currentStageIndex].playerSpawnTransform[3]};
        }
    }

    public int GetNextRandom(int minValue, int maxValue)
    {
        randomCallCount++;
        return seededRandom.Next(maxValue);
    }

    public FixedVec2 GetRandomSpawnVec2()
    {
        Vector2[] spawnPointList = GetSpawnPositions();
        Vector2 spawnPoint = spawnPointList[GetNextRandom(0, spawnPointList.Length)]; // Use wrapper
        return new FixedVec2(Fixed.FromFloat(spawnPoint.x), Fixed.FromFloat(spawnPoint.y));
    }


    //A round is 1 match + spell acquisition phase
    public void RoundEnd()
    {
        if (!isSaved)
        {
            dataManager.SaveMatch();
            isSaved = true;
        }
        ProjectileManager.Instance.DeleteAllProjectiles();
        isRunning = false;

        if (isOnlineMatchActive)
        {
            isTransitioning = true;
            // Reset ready flags for next shop phase
            localPlayerReadyForGameplay = false;
            remotePlayerReadyForGameplay = false;
        }
        SceneManager.LoadScene("Shop");

         //play a new shop song
         //BGM_Manager.Instance.StartAndPlaySong();
    }

    /// <summary>
    /// called when a game ends (game is a series of matches/rounds)
    /// </summary>
    public void GameEnd()
    {
        if (!isSaved)
        {
            dataManager.SaveMatch();
            isSaved = true;
        }

        //reset all ram values for players so they don't carry over to the end screen or next match
        for (int i = 0; i < playerCount; i++)
        {
            players[i].totalRam = 0;
            players[i].roundRam = 0;

        }

        gameOver = false;
        roundOver = false;

        dataManager.SaveToFile();
        ProjectileManager.Instance.DeleteAllProjectiles();
        if (isOnlineMatchActive)
        {
            isTransitioning = true;
        }
        else
        {
            isRunning = false;
        }
        SceneManager.LoadScene("End");

        //play a new end song
        //BGM_Manager.Instance.StartAndPlaySong();
    }

    public PlayerController[] GetActivePlayerControllers()
    {
        PlayerController[] activePlayers = new PlayerController[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            activePlayers[i] = players[i];
        }
        return activePlayers;
    }

    public void SetStage(int stageIndex)
    {
        currentStageIndex = stageIndex;

        ClearStages();
        //enable the temp map gameobject corresponding to the stage index, disable others
        if (currentStageIndex == -1)
        {
            //foreach (SpellCode_Gate gate in gates) { gate.isOpen = false; }
            lobbyMapGO.SetActive(true);
            return;
        }
        for (int i = 0; i < tempMapGOs.Count; i++)
        {
            if (i == stageIndex)
            {
                tempMapGOs[i].SetActive(true);
            }
        }
    }

    public void LoadRandomGameplayStage()
    {
        // Disable PlayerInputManager BEFORE loading scene to prevent duplicate player registration
        if (playerInputManager != null)
        {
            playerInputManager.DisableJoining();
            playerInputManager.enabled = false;
            //Debug.Log("Disabled PlayerInputManager before scene load");
        }

        int newStageIndex;
        if (isOnlineMatchActive)
        {
            newStageIndex = 1;
        }
        else
        {
            do
            {
                newStageIndex = seededRandom.Next(0, stages.Length);
            } while (currentStageIndex == newStageIndex);
        }

        SetStage(newStageIndex);
        if (isOnlineMatchActive)
        {
            isTransitioning = true;
        }

        SceneManager.LoadScene("Gameplay");
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //Debug.Log($"Scene loaded: {scene.name}");

        damageMatrix = new byte[4, 4]; //reset damage matrix on each scene load

        // For OFFLINE gameplay
        if (!isOnlineMatchActive && scene.name == "Gameplay")
        {
            //Debug.Log("Gameplay loaded (offline) - resetting players");

            // Keep PlayerInputManager disabled to prevent duplicate joins
            if (playerInputManager != null)
            {
                playerInputManager.enabled = false;
            }

            ResetPlayers();
            HitboxManager.Instance.GetActiveCamera();
            FindAllFloppyDisks();
        }

        // For ONLINE gameplay
        if (isOnlineMatchActive && scene.name == "Gameplay" && isTransitioning)
        {
            //Debug.Log("Gameplay Scene Loaded - Resuming Online Match");
            isTransitioning = false;
            localPlayerReadyForGameplay = false;
            remotePlayerReadyForGameplay = false;

            if (currentStageIndex != 0 && currentStageIndex != 1)
            {
                SetStage(1);
            }

            ResetPlayers();

            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.SaveState();
            }
        }

        // Handle shop scene loading for online
        if (isOnlineMatchActive && scene.name == "Shop" && isTransitioning)
        {
            //Debug.Log("Shop Scene Loaded - Resuming Online Match in Shop");
            isTransitioning = false;
            // Ready flags are already reset in RoundEnd()
        }
    }

    public void ClearStages()
    {
        for (int i = 0; i < tempMapGOs.Count; i++)
        {
            tempMapGOs[i].SetActive(false);
        }
        lobbyMapGO.SetActive(false);
    }

    public void SetMenuActive(bool isActive)
    {
        if (MainMenuScreen != null)
        {
            MainMenuScreen.SetActive(isActive);
        }
    }

    public void GenerateStartingSpells(int index)
    {
        if (index == 0)
        {
            p1_choices = new List<string>();
            p1_choices.Add("SkillshotSlash");
            p1_choices.Add("MightOfZeus");
            p1_choices.Add("AmonSlash");
            p1_choices.Add("CoinToss");
        }
        if (index == 1)
        {
            p2_choices = new List<string>();
            p2_choices.Add("SkillshotSlash");
            p2_choices.Add("MightOfZeus");
            p2_choices.Add("AmonSlash");
            p2_choices.Add("CoinToss");
        }
        if (index == 2)
        {
            p3_choices = new List<string>();
            p3_choices.Add("SkillshotSlash");
            p3_choices.Add("MightOfZeus");
            p3_choices.Add("AmonSlash");
            p3_choices.Add("CoinToss");
        }
        if (index == 3)
        {
            p4_choices = new List<string>();
            p4_choices.Add("SkillshotSlash");
            p4_choices.Add("MightOfZeus");
            p4_choices.Add("AmonSlash");
            p4_choices.Add("CoinToss");
        }
    }

    //resets the raw stats for each player back to 0 or their base state
    public void ResetPlayerStats()
    {
        for (int i = 0; i < playerCount; i++)
        {
            players[i].basicsFired = 0;
            players[i].spellsFired = 0;
            players[i].spellsHit = 0;
            players[i].times = new List<Fixed>();
        }
    }

    public void FindAllFloppyDisks()
    {
        floppyObjects = GameObject.FindGameObjectsWithTag("FloppyDisk");
    }

    // ---------------------------------------------------------Central State Serialization Methods-----------------------------------------

    /// <summary>
    /// Serializes the entire deterministic game state managed by GameManager.
    /// Includes players and active projectiles.
    /// </summary>
    /// <returns>A byte array representing the game state snapshot.</returns>
    public byte[] SerializeManagedState()
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (BinaryWriter bw = new BinaryWriter(memoryStream))
            {

                // Player State
                bw.Write(playerCount); // Save number of active players
                for (int i = 0; i < playerCount; i++)
                {
                    if (players[i] != null)
                    {
                        players[i].Serialize(bw); // Call player's serialize method
                    }
                    else
                    {
                        // Handle potential null player slot if necessary, though playerCount should be accurate
                        //Debug.LogError($"Attempted to serialize null player at index {i}");
                    }
                }

                bw.Write(roundOver);
                bw.Write(gameOver);
                bw.Write(roundEndTimer);

                // Serialize damage matrix
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        bw.Write(damageMatrix[i, j]);
                    }
                }

                // Serialize random state for deterministic respawns
                bw.Write(randomSeed);
                bw.Write(randomCallCount);

                // Serialize spell selection indices for lobby state
                bw.Write(p1_index);
                bw.Write(p2_index);
                bw.Write(p3_index);
                bw.Write(p4_index);

                bw.Write(p1_shopIndex);
                bw.Write(p2_shopIndex);

                bw.Write(p1_lastCycleFrame);
                bw.Write(p2_lastCycleFrame);

                // Serialize shop spell choices themselves
                if (shopManager != null)
                {
                    SerializeStringList(bw, shopManager.GetP1Choices());
                    SerializeStringList(bw, shopManager.GetP2Choices());
                }
                else
                {
                    // No shop active, write empty lists
                    bw.Write(0); // p1_choices count
                    bw.Write(0); // p2_choices count
                }

                // Also serialize if players have chosen their shop spell
                for (int i = 0; i < playerCount; i++)
                {
                    bw.Write(players[i].chosenSpell);
                }

                List<BaseProjectile> activeProjectiles = ProjectileManager.Instance.activeProjectiles;
                bw.Write(activeProjectiles.Count);

                foreach (BaseProjectile projectile in activeProjectiles)
                {
                    // Save an identifier to find this projectile instance later during Deserialize
                    // Using its index in the *master* prefab list is generally reliable if that list never changes order after init.
                    int prefabIndex = ProjectileManager.Instance.projectilePrefabs.IndexOf(projectile);
                    if (prefabIndex == -1)
                    {
                        //Debug.LogError($"Active projectile {projectile.projName} (Owner: {projectile.owner?.characterName}) not found in master prefab list during Serialize!");
                        bw.Write(-1);
                    }
                    else
                    {
                        bw.Write(prefabIndex);
                        projectile.Serialize(bw);
                    }
                }

                bw.Write(gates.Length);
                foreach (var gate in gates)
                {
                    if (gate != null) gate.Serialize(bw);
                }

                return memoryStream.ToArray();
            }
        }
    }

    /// <summary>
    /// Deserializes and applies a game state snapshot.
    /// Restores players and manages projectile activation/state.
    /// </summary>
    /// <param name="stateData">The byte array snapshot to load.</param>
    public void DeserializeManagedState(byte[] stateData)
    {
        using (MemoryStream memoryStream = new MemoryStream(stateData))
        {
            using (BinaryReader br = new BinaryReader(memoryStream))
            {
                int savedPlayerCount = br.ReadInt32();
                if (savedPlayerCount != playerCount)
                {
                    //Debug.LogWarning($"Player count mismatch during Deserialize! Saved: {savedPlayerCount}, Current: {playerCount}.");
                }
                for (int i = 0; i < playerCount; i++) // Use current (or updated) playerCount
                {
                    if (players[i] != null)
                    {
                        players[i].Deserialize(br);
                    }
                    else
                    {
                        //Debug.LogError($"Attempting to deserialize state into null player at index {i}.");
                    }
                }

                roundOver = br.ReadBoolean();
                gameOver = br.ReadBoolean();
                roundEndTimer = br.ReadSingle();

                // Deserialize damage matrix
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        damageMatrix[i, j] = br.ReadByte();
                    }
                }

                // Deserialize random state
                randomSeed = br.ReadInt32();
                int savedCallCount = br.ReadInt32();

                // Reconstruct random to exact same state as when it was saved
                seededRandom = new System.Random(randomSeed);
                for (int i = 0; i < savedCallCount; i++)
                {
                    seededRandom.Next();
                }
                randomCallCount = savedCallCount;

                // Deserialize spell selection indices
                p1_index = br.ReadInt32();
                p2_index = br.ReadInt32();
                p3_index = br.ReadInt32();
                p4_index = br.ReadInt32();
                p1_shopIndex = br.ReadInt32();
                p2_shopIndex = br.ReadInt32();
                p1_lastCycleFrame = br.ReadInt32();
                p2_lastCycleFrame = br.ReadInt32();

                // Deserialize shop spell choices
                List<string> savedP1Choices = DeserializeStringList(br);
                List<string> savedP2Choices = DeserializeStringList(br);

                // If shop manager exists, verify or restore choices
                if (shopManager != null)
                {
                    shopManager.SetP1Choices(savedP1Choices);
                    shopManager.SetP2Choices(savedP2Choices);
                }

                for (int i = 0; i < playerCount; i++)
                {
                    players[i].chosenSpell = br.ReadBoolean();
                }

                // Projectile State 
                int savedProjectileCount = br.ReadInt32();
                List<BaseProjectile> masterList = ProjectileManager.Instance.projectilePrefabs;
                List<BaseProjectile> currentlyActive = ProjectileManager.Instance.activeProjectiles.ToList(); // Copy to allow modification
                List<BaseProjectile> shouldBeActive = new List<BaseProjectile>(); // Track projectiles loaded from state

                // Read data and identify which projectiles should be active
                Dictionary<int, byte[]> projectileStateData = new Dictionary<int, byte[]>(); // Store raw state data temporarily
                List<int> activePrefabIndices = new List<int>();

                for (int i = 0; i < savedProjectileCount; i++)
                {
                    int prefabIndex = br.ReadInt32();
                    if (prefabIndex == -1 || prefabIndex >= masterList.Count)
                    {
                        //Debug.LogError($"Invalid prefab index ({prefabIndex}) read during projectile Deserialize. Skipping projectile state.");
                        // Need robust skipping logic here if SpellData.Deserialize can vary in length
                        continue; // Skip this entry
                    }
                    activePrefabIndices.Add(prefabIndex);

                    // Read the projectile's state into a temporary buffer
                    // This requires knowing the exact size of a serialized projectile, OR read until end marker (complex)
                    // A simpler (but less efficient) approach: Serialize includes size, or use fixed size
                    // Assuming BaseProjectile.Deserialize reads exactly its data:
                    // Need to temporarily store the BinaryReader position or read into temp memory.

                    // Re-seek or re-read approach (Less efficient but simpler to write now):
                    long currentPos = br.BaseStream.Position;
                    // Dummy deserialize to advance stream (inefficient - better to calculate size)
                    if (prefabIndex >= 0 && prefabIndex < masterList.Count && masterList[prefabIndex] != null)
                    {
                        masterList[prefabIndex].Deserialize(br);
                    }
                    else
                    {
                        // Cannot determine size to skip - this approach has issues.
                        //Debug.LogError("Cannot skip unknown projectile data.");
                        // Alternative: Calculate exact size of serialized projectile data.
                    }
                    long nextPos = br.BaseStream.Position;
                    long dataSize = nextPos - currentPos;
                    br.BaseStream.Position = currentPos; // Rewind
                    byte[] projData = br.ReadBytes((int)dataSize); // Read the exact bytes
                    projectileStateData[prefabIndex] = projData; // Store bytes keyed by prefab index
                }


                // Synchronize active state
                // Deactivate projectiles that are currently active but shouldn't be
                foreach (BaseProjectile activeProj in currentlyActive)
                {
                    int currentPrefabIndex = masterList.IndexOf(activeProj);
                    if (!activePrefabIndices.Contains(currentPrefabIndex))
                    {
                        // This projectile shouldn't be active, deactivate it
                        ProjectileManager.Instance.DeleteProjectile(activeProj); // Use manager's method to handle pool state
                    }
                }

                // Activate projectiles that should be active but aren't
                foreach (int prefabIndex in activePrefabIndices)
                {
                    BaseProjectile projectileInstance = masterList[prefabIndex];
                    if (!projectileInstance.gameObject.activeSelf)
                    {
                        // Activate from pool (Reset values first)
                        projectileInstance.ResetValues();
                        projectileInstance.gameObject.SetActive(true);
                        // Add to active list if DeleteProjectile doesn't handle it
                        if (!ProjectileManager.Instance.activeProjectiles.Contains(projectileInstance))
                        {
                            ProjectileManager.Instance.activeProjectiles.Add(projectileInstance);
                        }
                    }
                    shouldBeActive.Add(projectileInstance); // Add to the list of projectiles to load state for
                }


                // Load state into the now-correctly-active projectiles
                foreach (BaseProjectile projectileToLoad in shouldBeActive)
                {
                    int prefabIndex = masterList.IndexOf(projectileToLoad);
                    if (projectileStateData.TryGetValue(prefabIndex, out byte[] projData))
                    {
                        using (MemoryStream projStream = new MemoryStream(projData))
                        {
                            using (BinaryReader projReader = new BinaryReader(projStream))
                            {
                                projectileToLoad.Deserialize(projReader);
                            }
                        }
                    }
                    else
                    {
                        //Debug.LogError($"State data for prefab index {prefabIndex} not found during load pass.");
                    }
                }

                int gateCount = br.ReadInt32();
                for (int i = 0; i < gateCount; i++)
                {
                    if (i < gates.Length && gates[i] != null)
                    {
                        gates[i].Deserialize(br);
                    }
                }

                // Resolve References
                // Call ResolveReferences on players if they need it (unlikely for player->spell)
                // Call ResolveReferences on all *active* projectiles
                foreach (BaseProjectile projectile in ProjectileManager.Instance.activeProjectiles) // Iterate over the now correct list
                {
                    projectile.ResolveReferences();
                }
            }
        }
    }

    // Helper methods for string list serialization
    private void SerializeStringList(BinaryWriter bw, List<string> list)
    {
        bw.Write(list?.Count ?? 0);
        if (list != null)
        {
            foreach (string s in list)
            {
                bw.Write(s ?? "");
            }
        }
    }

    private List<string> DeserializeStringList(BinaryReader br)
    {
        int count = br.ReadInt32();
        List<string> list = new List<string>();
        for (int i = 0; i < count; i++)
        {
            list.Add(br.ReadString());
        }
        return list;
    }
}