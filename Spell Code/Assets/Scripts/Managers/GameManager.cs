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
using static RollbackManager;
using UnityEngine.InputSystem.Composites;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameObject MainMenuScreen;

    public GameObject playerPrefab;
    public PlayerController[] players = new PlayerController[4];
    public int playerCount = 0;
    [NonSerialized]
    public ushort ramNeededToWinRound = 1;

    public SpriteRenderer shopImage;


    [NonSerialized]
    /// <summary>
    /// This matrix defines how much damage each player has done to a given player when said player dies, notably used for RAM payout.
    /// </summary>
    public byte[,] damageMatrix = new byte[,]
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
    public List<StageDataSO> stages;
    [SerializeField] private List<StageDataSO> gameStages = new List<StageDataSO>();
    public StageDataSO lobbySO;
    // public StageDataSO currentStage;
    public int currentStageIndex = 0;
    public SceneUiManager sceneManager;

    public List<GameObject> tempMapGOs = new List<GameObject>();
    public GameObject lobbyMapGO;
    public string currentStage;

    [HideInInspector]
    public ShopManager shopManager;
    public OnboardManager onboardManager;


    public GameObject floppyDisplayPrefab;

    public GO_Door goDoorPrefab;
    public OnlineHostDoor onlineHostDoor;

    public bool roundOver;
    public bool gameOver;

    public bool prevSceneWasShop;
    public bool isTransitioning = false;

    public SpellCode_Gate[] gates = new SpellCode_Gate[4];
    private readonly Dictionary<Vector2, SpellCode_Gate> gateLookup = new();
    private const float GatePositionKeyPrecision = 1000f;

    //game timers
    public float roundEndTimer = 0f;
    public int roundEndTransitionTime = 5;
    private int roundEndFrameCounter = 0;
    private bool roundEndUIShown = false;
    private int lastRoundWinnerPID = -1;
    private bool roundTransitionPending = false;
    private bool onlineRoundAdvanceApplied = false;
    private bool pendingOpponentShopTransition = false;
    public TextMeshProUGUI playerWinText;
    public TextMeshProUGUI roundEndedText;

    //main menu stuff (we will likely remove all of this later, its just a rehash of shop manager stuff)
    public bool playersChosenSpell;
    public GameObject[] floppyObjects;

    [SerializeField]
    private List<string> p1_choices;
    [SerializeField]
    private List<string> p2_choices;
    [SerializeField]
    private List<string> p3_choices;
    [SerializeField]
    private List<string> p4_choices;

    public List<GameObject> gambas;

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
    //private ulong cachedLocalInput = 5; // Stores input gathered in Update()
    //private bool codePrevFrame = false;
    //private bool jumpPrevFrame = false;
    //private bool codeCurrentFrame = false;
    //private bool jumpCurrentFrame = false;

    // New variables for Online Match State
    public int frameNumber { get; private set; } = 0; // Master frame counter
    public bool isOnlineMatchActive = false;
    private ulong localPlayerInput = 0; // Stores local input for the current frame
    private ulong[] syncedInput = new ulong[2] { 0, 0 }; // Inputs for both players this frame
    public int localPlayerIndex = 0; // Set this before starting online match
    public int remotePlayerIndex = 1; // Set this before starting online match
    private int timeoutFrames = 0; // Timeout counter
    public int randomSeed = 0;
    public int randomCallCount = 0;
    private uint rngState = 0;
    private uint stageRngState;

    [Header("Debug")]
    public bool logDesyncTrace = false;
    public int logDesyncEveryNFrames = 1;

    // Online lobby state tracking
    public bool localPlayerReadyForGameplay = false;
    public bool remotePlayerReadyForGameplay = false;
    private bool localSceneTransitionReady = false;
    private bool remoteSceneTransitionReady = false;
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

    public void ExecuteOrder66()
    {
        GameObject dontDestroyProbe = new GameObject("Order66_DontDestroyProbe");
        DontDestroyOnLoad(dontDestroyProbe);

        Scene dontDestroyScene = dontDestroyProbe.scene;
        GameObject[] persistentRoots = dontDestroyScene.GetRootGameObjects();

        for (int i = 0; i < persistentRoots.Length; i++)
        {
            if (persistentRoots[i] != dontDestroyProbe)
            {
                Destroy(persistentRoots[i]);
            }
        }

        Destroy(dontDestroyProbe);
        Instance = null;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isOnlineMatchActive = false;
        isWaitingForOpponent = false;
        opponentIsReady = false;
        isTransitioning = false;
        localSceneTransitionReady = false;
        remoteSceneTransitionReady = false;
        frameNumber = 0;

        isRunning = true;
        isSaved = false;

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

#if UNITY_EDITOR
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
#endif
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
        }
    }

    private ulong GatherInputForOnline()
    {
        if (StressTestController.Instance != null && StressTestController.Instance.UseDeterministicInput)
        {
            return StressTestController.Instance.GetDeterministicInput(frameNumber);
        }

        PlayerController localPlayer = players[localPlayerIndex];
        if (localPlayer != null && localPlayer.inputs.IsActive)
        {
            return (ulong)localPlayer.inputs.UpdateInputs();
        }
        return 5; // neutral
        //return GatherRawInput(); // fallback to raw input gathering if player controller or inputs are not available
    }

    //private ulong GatherRawInput()
    //{
    //    // Direction
    //    bool up = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow);
    //    bool down = UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow);
    //    bool left = UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow);
    //    bool right = UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow);

    //    // Buttons - sample current state
    //    bool codeNow = UnityEngine.Input.GetKey(KeyCode.R);
    //    bool jumpNow = UnityEngine.Input.GetKey(KeyCode.T);

    //    // Detect state transitions
    //    ButtonState codeState = GetButtonStateHelper(codePrevFrame, codeNow);
    //    ButtonState jumpState = GetButtonStateHelper(jumpPrevFrame, jumpNow);

    //    // Update for next frame - do this AFTER getting states
    //    codePrevFrame = codeNow;
    //    jumpPrevFrame = jumpNow;

    //    ButtonState[] buttons = new ButtonState[2] { codeState, jumpState };
    //    bool[] dirs = new bool[4] { up, down, left, right };

    //    return (ulong)InputConverter.ConvertToLong(buttons, dirs);
    //}

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
        onboardManager = null;

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

        onboardManager = FindFirstObjectByType<OnboardManager>();
        if (onboardManager != null)
        {
            onboardManager.ResetOnboarding();
        }

        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i] != null)
            {
                gates[i].isOpen = false;
                gates[i].SetOpen(false);
            }
        }

        foreach (GameObject gambaGO in GetValidGambaObjects())
        {
            if (gambaGO == null) continue;
            GambaMachine gamba = gambaGO.GetComponent<GambaMachine>();
            if (gamba != null)
            {
                gamba.ResetLobbyState();
            }
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
        localSceneTransitionReady = false;
        remoteSceneTransitionReady = false;

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

                players[i].inputs.AssignInputDevice(null);
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
        // Keep the configured/default input delay until synchronized settings arrive
        // instead of forcing an immediate zero-delay startup.
        RollbackManager.Instance.Init(opponentId.Value);

        if (MatchMessageManager.Instance != null)
        {
            if (StressTestController.Instance != null && StressTestController.Instance.enableStressTest)
            {
                StressTestController.Instance.ResetForNewMatch();
            }
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

        if (!isOnlineMatchActive || !isWaitingForOpponent) return;

        opponentIsReady = true;
        if (localPlayerIndex == 0) // Host generates and sends seed
        {
            MatchMessageManager.Instance.SendRollbackSettings();
            int agreedSeed = UnityEngine.Random.Range(0, 100000);
            InitializeWithSeed(agreedSeed);
            MatchMessageManager.Instance.SendSeed(agreedSeed);
            StartLobbySimulation();
        }
    }

    public void StartLobbySimulation()
    {
        // Double-check we're in the right state
        if (!isWaitingForOpponent)
        {
            //Debug.LogWarning("StartLobbySimulation called but not waiting - aborting");
            return;
        }

        lastPacketReceivedTime = UnityEngine.Time.unscaledTime;
        lobbyWaitStartTime = UnityEngine.Time.unscaledTime;

        isWaitingForOpponent = false;

        // Send match start confirmation
        if (MatchMessageManager.Instance != null)
        {
            MatchMessageManager.Instance.SendMatchStartConfirm();
        }

        ProjectileManager.Instance.InitializeAllProjectiles();
        frameNumber = 0;
        isRunning = true;

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

    public void OnOpponentSceneTransitionReady()
    {
        remoteSceneTransitionReady = true;
        CheckSceneTransitionReady();
    }

    // Check if both players are ready to transition
    private void CheckBothPlayersReadyForGameplay()
    {
        if (localPlayerReadyForGameplay && remotePlayerReadyForGameplay)
        {
            //Debug.Log("Both players ready - transitioning to Gameplay");
            isTransitioning = true;
            localSceneTransitionReady = false;
            remoteSceneTransitionReady = false;
            LoadRandomGameplayStage();
        }
    }

    private void CheckSceneTransitionReady()
    {
        if (!isTransitioning)
        {
            return;
        }

        if (localSceneTransitionReady && remoteSceneTransitionReady)
        {
            isTransitioning = false;
            localSceneTransitionReady = false;
            remoteSceneTransitionReady = false;
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
            localSceneTransitionReady = false;
            remoteSceneTransitionReady = false;

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

    private List<GameObject> GetValidGambaObjects(bool refreshIfNeeded = false)
    {
        if (gambas == null)
        {
            gambas = new List<GameObject>();
        }

        if (refreshIfNeeded && (gambas.Count == 0 || gambas.Any(gambaGO => gambaGO == null)))
        {
            RefreshSceneObjectReferences();
        }

        gambas = gambas.Where(gambaGO => gambaGO != null).ToList();
        return gambas;
    }

    public void UpdateSceneLogic(ulong[] inputs)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool isOnline = isOnlineMatchActive;
        bool isRealFrame = RollbackManager.Instance == null || !RollbackManager.Instance.isRollbackFrame;

        if (activeScene.name == "MainMenu")
        {
            goDoorPrefab?.CheckOpenDoor();

            if (onboardManager == null)
                onboardManager = FindFirstObjectByType<OnboardManager>();
            if (onboardManager != null)
                onboardManager.OnboardUpdate(inputs);

            if (isOnline)
            {
                shopImage.enabled = false;
                SimulateOnlineFloppies(inputs, isRealFrame);
            }
        }
        else if (activeScene.name == "Shop")
        {
            for (int i = 0; i < playerCount; i++)
            {
                players[i].roundRam = 0; // reset round RAM to prevent carryover from lobby
            }
            goDoorPrefab.CheckOpenDoor();

            bool isRollback = RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame;
            foreach (GameObject gambaGO in GetValidGambaObjects(refreshIfNeeded: true))
            {
                if (gambaGO == null) continue;
                GambaMachine gamba = gambaGO.GetComponent<GambaMachine>();
                if (gamba != null) gamba.SimulateOnline(gamba.ownerPID - 1, isRollback);
            }

            if (!isRollback && goDoorPrefab.CheckAllPlayersReady())
            {
                if (isOnlineMatchActive)
                {
                    if (!localPlayerReadyForGameplay)
                    {
                        SendLobbyReadyForGameplay();
                    }
                }
                else
                {
                    LoadRandomGameplayStage();
                }
            }
            if (isOnline)
            {
                shopImage.enabled = true;
                SimulateOnlineFloppies(inputs, isRealFrame);
            }
        }
        else if (activeScene.name == "Gameplay")
        {
            CheckDeathsAndRoundEnd(GetActivePlayerControllers());

            if (isOnlineMatchActive && pendingOpponentShopTransition && roundOver && !isTransitioning)
            {
                pendingOpponentShopTransition = false;
                AdvanceRoundCountOnce();
                BeginOnlineShopTransition();
                return;
            }

            if (isOnline)
            {
                shopImage.enabled = false;
            }
        }
    }

    private void SimulateOnlineFloppies(ulong[] inputs, bool isRealFrame)
    {
        if (!isRealFrame) return;
        if (floppyObjects == null || floppyObjects.Length == 0)
        {
            FindAllFloppyDisks();
        }
        if (floppyObjects == null) return;

        for (int i = 0; i < floppyObjects.Length; i++)
        {
            GameObject floppy = floppyObjects[i];
            if (floppy == null) continue;
            SpellCode_FloppyDisk disk = floppy.GetComponent<SpellCode_FloppyDisk>();
            if (disk != null)
            {
                disk.SimulateOnline(inputs, isRealFrame);
            }
        }
    }

    /// <summary>
    /// Executes one frame of the online match simulation using RollbackManager.
    /// </summary>
    private void RunOnlineFrame()
    {
        RollbackManager rbManager = RollbackManager.Instance;
        if (rbManager == null) return;

        if (!rbManager.isRollbackFrame)
        {
            int currentFrame = frameNumber;
            int stateIndex = currentFrame % RollbackManager.InputArraySize;
            if (rbManager.states[stateIndex].frame != currentFrame || rbManager.states[stateIndex].state == null)
            {
                rbManager.SaveState();
            }
        }

        timeoutFrames = 0;
        rbManager.RollbackEvent();

        if (!rbManager.AllowUpdate())
        {
            return;
        }

        localPlayerInput = GatherInputForOnline();
        //codePrevFrame = codeCurrentFrame;
        //jumpPrevFrame = jumpCurrentFrame;

        frameNumber++;
        rbManager.SendLocalInput(localPlayerInput);
        syncedInput = rbManager.SynchronizeInput();

        Scene activeScene = SceneManager.GetActiveScene();

        UpdateGameState(syncedInput);

        UpdateSceneLogic(syncedInput);

        // ONLINE LOBBY LOGIC (MainMenu scene)
        if (activeScene.name == "MainMenu")
        {
            // Handle spell selection for online players (only local and remote)
            //HandleOnlineSpellSelection();

            //if (onboardManager == null)
            //    onboardManager = FindFirstObjectByType<OnboardManager>(); // only finds active objects

            //if (onboardManager != null && !rbManager.isRollbackFrame)
            //    onboardManager.OnboardUpdate(syncedInput);

            // Drive gamba machines through synced simulation (must run during rollback for RNG consistency)
            foreach (GameObject gambaGO in GetValidGambaObjects(refreshIfNeeded: true))
            {
                if (gambaGO == null) continue;
                GambaMachine gamba = gambaGO.GetComponent<GambaMachine>();
                if (gamba != null) gamba.SimulateOnline(gamba.ownerPID - 1, rbManager.isRollbackFrame);
            }

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
            TickRoundEndTransition(!rbManager.isRollbackFrame);
        }
        //else if (activeScene.name == "Shop")
        //{
        //    if (!rbManager.isRollbackFrame)
        //    {
        //        foreach (GameObject gambaGO in gambas)
        //        {
        //            GambaMachine gamba = gambaGO.GetComponent<GambaMachine>();
        //            if (gamba != null) gamba.SimulateOnline(gamba.ownerPID - 1);
        //        }
        //    }
        //}

        if (!rbManager.isRollbackFrame && !rbManager.DelayBased)
        {
            rbManager.SaveState();
        }
    }

    private int RoundEndTransitionFrameThreshold => Mathf.Max(1, Mathf.RoundToInt(roundEndTransitionTime * 60f));

    private void TickRoundEndTransition(bool isRealFrame)
    {
        if (!roundOver)
        {
            roundEndFrameCounter = 0;
            roundTransitionPending = false;
            return;
        }

        HandleRoundEndUI(isRealFrame);

        roundEndFrameCounter++;
        if (roundEndFrameCounter >= RoundEndTransitionFrameThreshold)
        {
            roundEndFrameCounter = 0;
            roundTransitionPending = true;
        }

        if (isRealFrame && roundTransitionPending)
        {
            roundTransitionPending = false;
            PerformRoundTransition();
        }
    }

    private void PerformRoundTransition()
    {
        ClearStages();

        if (gameOver)
        {
            playerWinText.enabled = false;
            AdvanceRoundCountOnce();
            GameEnd();
            roundOver = false;
            roundEndUIShown = false;
            lastRoundWinnerPID = -1;
            roundTransitionPending = false;
            return;
        }

        playerWinText.enabled = false;
        AdvanceRoundCountOnce();

        bool hasMaxSpells = playerCount > 0
            && players[0] != null
            && players[0].spellList != null
            && players[0].spellList.Count >= 6;

        if (hasMaxSpells)
        {
            if (isOnlineMatchActive)
            {
                localPlayerReadyForGameplay = false;
                remotePlayerReadyForGameplay = false;
            }
            LoadRandomGameplayStage();
            ResetPlayers();
            roundOver = false;
            roundEndUIShown = false;
            lastRoundWinnerPID = -1;
            roundTransitionPending = false;
            return;
        }

        RoundEnd();
        ResetPlayers();
        roundOver = false;
        roundEndUIShown = false;
        lastRoundWinnerPID = -1;
        roundTransitionPending = false;
    }

    private void HandleRoundEndUI(bool isRealFrame)
    {
        if (!isRealFrame || !roundOver || roundEndUIShown || lastRoundWinnerPID <= 0)
        {
            return;
        }

        roundEndUIShown = true;

        string message;
        if (gameOver)
        {
            message = "Game Over : Player " + lastRoundWinnerPID + " wins the match! Congratulations!!!";
            if (roundEndedText != null)
            {
                roundEndedText.text = message;
            }
            if (tempUI != null)
            {
                StartCoroutine(tempUI.DisplayTransitionScreen(4f, message));
            }
        }
        else
        {
            message = "Round Ended : Player " + lastRoundWinnerPID + " wins the match! Beginning Shop Phase...";
            if (roundEndedText != null)
            {
                roundEndedText.text = message;
            }
            if (tempUI != null)
            {
                StartCoroutine(tempUI.DisplayTransitionScreen(4f, message));
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
                    sceneManager.MainMenu();
                    //RestartGame();
                    return;
                }
            }
        }
        ///shop specific update
        if (activeScene.name == "Shop")
        {
            for (int i = 0; i < playerCount; i++)
            {
                players[i].roundRam = 0; // reset round RAM to prevent carryover from lobby
            }
            shopImage.enabled = true;
            goDoorPrefab.CheckOpenDoor();

            if (goDoorPrefab.CheckAllPlayersReady())
            {
                LoadRandomGameplayStage();
            }
        }
        else
        {
            shopImage.enabled = false;
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

            if (!isOnlineMatchActive && onlineHostDoor != null)
            {
                onlineHostDoor.CheckOpenDoor();
                onlineHostDoor.CheckHostTrigger();
            }

            if (players[0] != null)
            {
                SetMenuActive(false);
            }
        }

        else if (activeScene.name == "Gameplay")
        {
            if (!roundOver) { dataManager.roundTimer++; }

            if (CheckDeathsAndRoundEnd(GetActivePlayerControllers()))
            {
                HandleRoundEndUI(true);

                //stop repeating all sounds
                SFX_Manager.Instance.StopRepeatingAllSounds();



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
                        Debug.Log(roundEndTimer);
                        roundEndTimer = 0;
                        roundEndUIShown = false;
                        lastRoundWinnerPID = -1;
                    }
                    else if (players[0].spellList.Count >= 6)
                    {
                        playerWinText.enabled = false;
                        dataManager.totalRoundsPlayed += 1;
                        LoadRandomGameplayStage();
                        ResetPlayers();
                        Debug.Log(roundEndTimer);
                        roundEndTimer = 0;
                        roundOver = false;
                        roundEndUIShown = false;
                        lastRoundWinnerPID = -1;
                    }
                    else
                    {
                        playerWinText.enabled = false;
                        dataManager.totalRoundsPlayed += 1;
                        RoundEnd();
                        ResetPlayers();
                        Debug.Log(roundEndTimer);
                        roundEndTimer = 0;
                        roundOver = false;
                        roundEndUIShown = false;
                        lastRoundWinnerPID = -1;
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

    public bool IsGateOpenAtPosition(float x, float y)
    {
        if (TryGetGateAtPosition(x, y, out var gate))
        {
            return gate.isOpen;
        }
        return false;
    }

    public bool TryGetGateAtPosition(float x, float y, out SpellCode_Gate gate)
    {
        Vector2 key = GetGateKey(x, y);
        if (gateLookup.TryGetValue(key, out gate))
        {
            return true;
        }

        return TryLocateGateNearKey(key, out gate);
    }

    public Vector2 GetGateKey(float x, float y)
    {
        return NormalizeGatePosition(new Vector2(x, y));
    }

    private Vector2 NormalizeGatePosition(Vector2 raw)
    {
        float roundedX = Mathf.Round(raw.x * GatePositionKeyPrecision) / GatePositionKeyPrecision;
        float roundedY = Mathf.Round(raw.y * GatePositionKeyPrecision) / GatePositionKeyPrecision;
        return new Vector2(roundedX, roundedY);
    }

    private bool TryLocateGateNearKey(Vector2 key, out SpellCode_Gate gate)
    {
        float tolerance = 1f / GatePositionKeyPrecision;
        foreach (SpellCode_Gate candidate in gates)
        {
            if (candidate == null) continue;

            Vector2 candidateKey = NormalizeGatePosition(candidate.transform.position);
            if (Vector2.Distance(candidateKey, key) <= tolerance)
            {
                gate = candidate;
                gateLookup[candidateKey] = candidate;
                return true;
            }
        }

        gate = null;
        return false;
    }

    public void UpdatePlayerBounties(bool applyVisuals = true)
    {
        ushort averageRoundRam = 0;
        int averageRoundWins = 0;
        for (int i = 0; i < playerCount; i++)
        {
            averageRoundRam += players[i].roundRam;
            averageRoundWins += players[i].roundsWon;
        }
        averageRoundRam = (ushort)(averageRoundRam / playerCount);
        averageRoundWins = averageRoundWins / playerCount;


        for (int i = 0; i < playerCount; i++)
        {
            players[i].ramBounty = (short)(((players[i].roundRam - averageRoundRam)/2) + (100*(players[i].roundsWon - averageRoundWins)));
        }

        if (!applyVisuals)
        {
            return;
        }

        //give the player with the highest bounty the bounty aura VFX
        int playerWithHighestBountyIndex = 0;
        for (int i = 0; i < playerCount; i++)
        {
            if (players[i].ramBounty > players[playerWithHighestBountyIndex].ramBounty)
            {
                playerWithHighestBountyIndex = i;
            }
            else
            {
                //remove the bounty VFX from this player
                VFX_Manager.Instance.StopVisualEffect(VisualEffects.BOUNTY_AURA, i + 1);
            }
        }
        //Debug.Log("Highest bounty player = " + players[playerWithHighestBountyIndex].pID);

        //give the bounty VFX to the player with the highest bounty
        //VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BOUNTY_AURA, players[playerWithHighestBountyIndex].position, playerWithHighestBountyIndex + 1, true, players[playerWithHighestBountyIndex].gameObject.transform, players[playerWithHighestBountyIndex].ramBounty);
        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BOUNTY_AURA, players[playerWithHighestBountyIndex].position + FixedVec2.FromFloat(0f, 102f), playerWithHighestBountyIndex + 1, true, players[playerWithHighestBountyIndex].gameObject.transform);
    }

    public bool CheckDeathsAndRoundEnd(PlayerController[] playerControllers)
    {

        if(roundOver) { return true; }

        bool isRollback = RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame;

        foreach (PlayerController player in playerControllers)
        {
            //check for player deaths
            if(!player.isAlive)
            {

                //go through each player and award them ram based on the percentage of the other player's health they took (damage matrix)
                foreach (PlayerController p in playerControllers)
                {
                    int damagePercent = damageMatrix[player.pID - 1, p.pID - 1];
                    int bountyCut = Math.Max(-PlayerController.baseRamLifeWorth, (damagePercent * player.ramBounty) / 100);
                    int totalRamEarned = (damagePercent * PlayerController.baseRamLifeWorth) / 100 + bountyCut;
                    int CollectedGold = Mathf.Clamp((int)totalRamEarned,0,ramNeededToWinRound-1-p.roundRam);
                    p.roundRam += (ushort)CollectedGold;
                    p.totalRam += (ushort)CollectedGold;
                    p.SpawnToast($"+{totalRamEarned} RAM", Color.yellow);

                    damageMatrix[player.pID - 1, p.pID - 1] = 0; //reset damage matrix for next death
                }

                UpdatePlayerBounties(!isRollback);

                // Clear lingering projectiles from the dead player so both clients respawn
                // into the same clean state instead of carrying old shots across deaths.
                ProjectileManager.Instance.DeleteAllPlayerProjectiles(player.pID);

                // Respawn position is deterministic state and must be recomputed during rollback too.
                FixedVec2 spawnPos = GetRandomSpawnVec2();
                player.SpawnPlayer(spawnPos);
            }
        }

        //then check winner conditions (most ram at the end of the round)
        foreach (PlayerController player in playerControllers)
        {
            if (player.roundRam >= ramNeededToWinRound)
            {
                // Determine winner deterministically here
                if (!roundOver)
                {
                    ushort highestRam = 0;
                    PlayerController winner = null;
                    for (int i = 0; i < playerCount; i++)
                    {
                        if (players[i].roundRam >= ramNeededToWinRound && players[i].roundRam > highestRam)
                        {
                            winner = players[i];
                            highestRam = players[i].roundRam;
                        }
                    }

                    if (winner != null)
                    {
                        winner.roundsWon += 1;
                        roundOver = true;
                        lastRoundWinnerPID = winner.pID;
                        roundEndUIShown = false;

                        for (int i = 0; i < playerCount; i++)
                        {
                            //players[i].roundRam = 0;
                            if (!isRollback)
                            {
                                players[i].playerNum.enabled = false;
                                players[i].inputDisplay.enabled = false;
                            }
                            if (players[i].roundsWon >= 3) { gameOver = true; }
                        }
                        if (!isRollback)
                        {
                            playerWinText.enabled = true;
                        }
                    }
                }
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
                players[i].inputDisplay.enabled = true;
                players[i].playerNum.enabled = true;
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

    public void InitializeWithSeed(int seed)
    {
        randomSeed = seed;
        randomCallCount = 0;
        rngState = (uint)seed;
        stageRngState = (uint)(seed ^ 0x9E3779B9);
        Debug.Log($"[SEED] Initialized RNG with seed: {seed}");
    }

    public int GetNextRandom(int minValue, int maxValue)
    {
        // Simple LCG - fully deterministic, reconstructible from state alone
        rngState = rngState * 1664525u + 1013904223u;
        randomCallCount++;
        int range = maxValue - minValue;
        if (range <= 0) return minValue;
        return minValue + (int)(rngState % (uint)range);
    }

    private int GetNextStageRandom(int minValue, int maxValue)
    {
        // Deterministic LCG, same constants as GetNextRandom but separate state
        stageRngState = stageRngState * 1664525u + 1013904223u;
        int range = maxValue - minValue;
        if (range <= 0) return minValue;
        return minValue + (int)(stageRngState % (uint)range);
    }

    public FixedVec2 GetRandomSpawnVec2()
    {
        Vector2[] spawnPointList = GetSpawnPositions();
        Vector2 spawnPoint = spawnPointList[GetNextRandom(0, spawnPointList.Length)]; // Use wrapper
        Debug.Log("SpawnPoint chosen: " + spawnPoint);
        return new FixedVec2(Fixed.FromFloat(spawnPoint.x), Fixed.FromFloat(spawnPoint.y));
    }


    //A round is 1 match + spell acquisition phase
    public void RoundEnd()
    {
        if (!isSaved)
        {
            dataManager.SaveMatch();
            isSaved = true;
            dataManager.roundTimer = 0;
        }
        //ProjectileManager.Instance.DeleteAllProjectiles();
        //isRunning = false;

        if (isOnlineMatchActive)
        {
            if (localPlayerIndex == 0 && MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.SendShopTransitionSignal();
            }
            BeginOnlineShopTransition();
            return;
        }
        sceneManager.LoadScene("Shop");
        SetStage(-1);
         //play a new shop song
         //BGM_Manager.Instance.StartAndPlaySong();
    }

    private void BeginOnlineShopTransition()
    {
        if (isTransitioning && SceneManager.GetActiveScene().name == "Shop")
        {
            return;
        }

        isTransitioning = true;
        localSceneTransitionReady = false;
        localPlayerReadyForGameplay = false;
        remotePlayerReadyForGameplay = false;
        sceneManager.LoadScene("Shop");
        SetStage(-1);
    }

    public void OnOpponentShopTransition()
    {
        if (!isOnlineMatchActive)
        {
            return;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (activeSceneName == "Shop")
        {
            return;
        }

        if (activeSceneName != "Gameplay")
        {
            pendingOpponentShopTransition = true;
            return;
        }

        if (!roundOver && !isTransitioning)
        {
            pendingOpponentShopTransition = true;
            return;
        }

        pendingOpponentShopTransition = false;
        AdvanceRoundCountOnce();
        BeginOnlineShopTransition();
    }

    private void AdvanceRoundCountOnce()
    {
        if (dataManager == null)
        {
            dataManager = DataManager.Instance;
        }

        if (dataManager == null)
        {
            return;
        }

        if (isOnlineMatchActive)
        {
            if (onlineRoundAdvanceApplied)
            {
                return;
            }

            onlineRoundAdvanceApplied = true;
        }

        dataManager.totalRoundsPlayed += 1;
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
            localSceneTransitionReady = false;
        }
        else
        {
            isRunning = false;
        }
        sceneManager.LoadScene("End");

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
            currentStage = lobbyMapGO.name;
            return;
        }
        for (int i = 0; i < tempMapGOs.Count; i++)
        {
            if (i == stageIndex)
            {
                tempMapGOs[i].SetActive(true);
                currentStage = tempMapGOs[i].name;
            }
        }
    }

    public void LoadRandomGameplayStage()
    {
        if (isOnlineMatchActive)
        {
            if (localPlayerIndex == 0)
            {
                SelectAndBroadcastStage();
            }
            return;
        }

        // Disable PlayerInputManager BEFORE loading scene to prevent duplicate player registration
        if (playerInputManager != null)
        {
            playerInputManager.DisableJoining();
            playerInputManager.enabled = false;
            //Debug.Log("Disabled PlayerInputManager before scene load");
        }

        //if gameStages is empty,...
        if (gameStages.Count <= 0)
        {
            //fill it back up
            RandomizeGameStages();
        }

        int _gameStageIndex = GetNextRandom(0, gameStages.Count);
        int _newStageIndex = stages.FindIndex(x => x == gameStages[_gameStageIndex]);

        //remove the stage associated with newStageIndex so it does not repeat for the rest of the game
        gameStages.Remove(stages[_newStageIndex]);

        SetStage(_newStageIndex);
        if (isOnlineMatchActive)
        {
            isTransitioning = true;
            localSceneTransitionReady = false;
        }

        sceneManager.LoadScene("Gameplay");
        // DON'T call ResetPlayers() here - do it in OnSceneLoaded
    }

    private void SelectAndBroadcastStage()
    {
        if (gameStages.Count <= 0)
        {
            gameStages = new List<StageDataSO>(stages);
        }

        int gameStageIndex = GetNextStageRandom(0, gameStages.Count);
        int newStageIndex = stages.FindIndex(x => x == gameStages[gameStageIndex]);
        ApplyOnlineStageSelection(newStageIndex);

        if (MatchMessageManager.Instance != null)
        {
            MatchMessageManager.Instance.SendStageSelect(newStageIndex);
        }
    }

    public void ApplyOnlineStageSelection(int stageIndex)
    {
        if (playerInputManager != null)
        {
            playerInputManager.DisableJoining();
            playerInputManager.enabled = false;
        }

        if (gameStages.Count <= 0)
        {
            gameStages = new List<StageDataSO>(stages);
        }

        if (stageIndex >= 0 && stageIndex < stages.Count)
        {
            gameStages.Remove(stages[stageIndex]);
        }

        SetStage(stageIndex);
        isTransitioning = true;
        localSceneTransitionReady = false;
        sceneManager.LoadScene("Gameplay");
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //Debug.Log($"Scene loaded: {scene.name}");

        RefreshSceneObjectReferences();

        damageMatrix = new byte[4, 4]; //reset damage matrix on each scene load

        int roundsPlayed = 0;
        if (dataManager == null)
        {
            dataManager = DataManager.Instance;
        }
        if (dataManager != null)
        {
            roundsPlayed = dataManager.totalRoundsPlayed;
            
        }
        else
        {
            roundsPlayed = 1;
        }

        ramNeededToWinRound = (ushort)(300 + 100 * roundsPlayed);

        if (scene.name != "MainMenu")
        {
            if (onboardManager != null)
            {
                Destroy(onboardManager.gameObject);
                onboardManager = null;
            }
        }

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
            onlineRoundAdvanceApplied = false;
            pendingOpponentShopTransition = false;
            localPlayerReadyForGameplay = false;
            remotePlayerReadyForGameplay = false;
            localSceneTransitionReady = false;
            frameNumber = 0;
            localPlayerInput = 5;
            syncedInput = new ulong[2] { 5, 5 };
            timeoutFrames = 0;

            if (MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.ResetFrameSyncForSceneTransition();
            }

            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.ClearVars();
            }

            if (currentStageIndex < 0)
            {
                SetStage(1);
            }

            ResetPlayers();
            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.SaveState();
            }

            localSceneTransitionReady = true;
            if (MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.SendSceneTransitionReadySignal();
            }
            CheckSceneTransitionReady();
        }

        // Handle shop scene loading for online
        if (isOnlineMatchActive && scene.name == "Shop" && isTransitioning)
        {
            //Debug.Log("Shop Scene Loaded - Resuming Online Match in Shop");
            pendingOpponentShopTransition = false;
            localPlayerReadyForGameplay = false;
            remotePlayerReadyForGameplay = false;
            localSceneTransitionReady = false;
            frameNumber = 0;
            localPlayerInput = 5;
            syncedInput = new ulong[2] { 5, 5 };
            timeoutFrames = 0;

            if (MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.ResetFrameSyncForSceneTransition();
            }

            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.ClearVars();
            }

            InitializeOnlineShopSceneState();
            ResetPlayers();
            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.SaveState();
            }
            localSceneTransitionReady = true;
            if (MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.SendSceneTransitionReadySignal();
            }
            CheckSceneTransitionReady();
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

    private void InitializeOnlineShopSceneState()
    {
        foreach (GameObject gambaGO in GetValidGambaObjects(refreshIfNeeded: true))
        {
            if (gambaGO == null) continue;
            GambaMachine gamba = gambaGO.GetComponent<GambaMachine>();
            if (gamba == null) continue;

            gamba.resetTimer = 0;
            bool hasActiveOwner = gamba.ownerPID > 0 && gamba.ownerPID <= playerCount && players[gamba.ownerPID - 1] != null;
            int roundsPlayed = dataManager != null ? dataManager.totalRoundsPlayed : 0;
            bool ownerCanUseShop = hasActiveOwner
                && players[gamba.ownerPID - 1].spellList != null
                && players[gamba.ownerPID - 1].spellList.Count < roundsPlayed + 1;

            gamba.ownerPlayer = hasActiveOwner ? players[gamba.ownerPID - 1] : null;
            gamba.activatedCount = ownerCanUseShop ? 0 : 3;
            if (gamba.gambaAnimator != null)
            {
                gamba.gambaAnimator.SetBool("isActive", ownerCanUseShop);
            }
        }

        foreach (SpellCode_Gate gate in gates)
        {
            if (gate == null) continue;
            gate.isOpen = false;
            gate.SetOpen(false);
        }
    }

    private void RefreshSceneObjectReferences()
    {
        GambaMachine[] sceneGambas = FindObjectsByType<GambaMachine>(FindObjectsSortMode.None);
        gambas = sceneGambas?
            .Where(gamba => gamba != null && gamba.gameObject != null)
            .OrderBy(gamba => gamba.ownerPID)
            .Select(gamba => gamba.gameObject)
            .ToList()
            ?? new List<GameObject>();

        SpellCode_Gate[] sceneGates = FindObjectsByType<SpellCode_Gate>(FindObjectsSortMode.None);
        gates = sceneGates?
            .Where(gate => gate != null)
            .OrderBy(gate => gate.name, StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<SpellCode_Gate>();
    }

    public void SetMenuActive(bool isActive)
    {
        if (MainMenuScreen != null)
        {
            MainMenuScreen.SetActive(isActive);
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
        floppyObjects = GameObject.FindGameObjectsWithTag("FloppyDisk")
            .OrderBy(go =>
            {
                SpellCode_FloppyDisk disk = go != null ? go.GetComponent<SpellCode_FloppyDisk>() : null;
                return disk != null ? disk.ownerPID : int.MaxValue;
            })
            .ThenBy(go => go != null ? go.transform.position.x : float.MaxValue)
            .ThenBy(go => go != null ? go.transform.position.y : float.MaxValue)
            .ThenBy(go =>
            {
                SpellCode_FloppyDisk disk = go != null ? go.GetComponent<SpellCode_FloppyDisk>() : null;
                return disk != null ? disk.diskName : string.Empty;
            })
            .ToArray();
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
                bw.Write(roundEndFrameCounter);
                bw.Write(currentStageIndex);

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
                bw.Write(rngState);
                bw.Write(stageRngState);

                // Serialize round state
                bw.Write(ramNeededToWinRound);
                bw.Write(roundEndUIShown);
                bw.Write(lastRoundWinnerPID);
                bw.Write(dataManager != null ? dataManager.totalRoundsPlayed : 0);

                Scene activeScene = SceneManager.GetActiveScene();
                bool includeLobbyShopState = activeScene.name != "Gameplay";
                bw.Write(includeLobbyShopState);

                if (includeLobbyShopState)
                {
                    // Serialize remaining game stages as indices into master stages list
                    bw.Write(gameStages.Count);
                    foreach (StageDataSO stage in gameStages)
                    {
                        bw.Write(stages.IndexOf(stage));
                    }

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
                }

                List<BaseProjectile> activeProjectiles = ProjectileManager.Instance.projectilePrefabs
                    .Where(projectile => projectile != null && projectile.gameObject.activeSelf)
                    .ToList();
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

                bw.Write(includeLobbyShopState);
                if (includeLobbyShopState)
                {
                    bw.Write(gates.Length);
                    foreach (var gate in gates)
                    {
                        if (gate != null) gate.Serialize(bw);
                    }

                    List<GameObject> validGambas = GetValidGambaObjects(refreshIfNeeded: true);
                    bw.Write(validGambas.Count);
                    foreach (GameObject gambaGO in validGambas)
                    {
                        if (gambaGO == null)
                        {
                            bw.Write(0);
                            bw.Write((byte)0);
                            bw.Write(0);
                            bw.Write(false);
                            continue;
                        }
                        GambaMachine gamba = gambaGO.GetComponent<GambaMachine>();
                        // Write defaults if somehow null, so byte count stays consistent
                        bw.Write(gamba != null ? gamba.activatedCount : 0);
                        bw.Write(gamba != null ? gamba.resetTimer : (byte)0);
                        bw.Write(gamba != null ? gamba.GetStartingSpellPos() : 0);
                        bool isActive = gamba != null && gamba.gambaAnimator != null && gamba.gambaAnimator.GetBool("isActive");
                        bw.Write(isActive);
                    }
                }

                return memoryStream.ToArray();
            }
        }
    }

    public byte[] SerializeHashState()
    {
        using (MemoryStream memoryStream = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(memoryStream))
        {
            bw.Write(playerCount);
            for (int i = 0; i < playerCount; i++)
            {
                if (players[i] != null)
                {
                    players[i].SerializeGameplayHash(bw);
                }
            }

            bw.Write(roundOver);
            bw.Write(gameOver);
            bw.Write(roundEndFrameCounter);
            bw.Write(currentStageIndex);

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    bw.Write(damageMatrix[i, j]);
                }
            }

            bw.Write(randomSeed);
            bw.Write(randomCallCount);
            bw.Write(rngState);
            bw.Write(stageRngState);
            bw.Write(ramNeededToWinRound);
            bw.Write(roundEndUIShown);
            bw.Write(lastRoundWinnerPID);

            List<BaseProjectile> activeProjectiles = ProjectileManager.Instance.projectilePrefabs
                .Where(projectile => projectile != null && projectile.gameObject.activeSelf)
                .ToList();
            bw.Write(activeProjectiles.Count);
            foreach (BaseProjectile projectile in activeProjectiles)
            {
                int prefabIndex = ProjectileManager.Instance.projectilePrefabs.IndexOf(projectile);
                bw.Write(prefabIndex);
                projectile.Serialize(bw);
            }

            return memoryStream.ToArray();
        }
    }

    public byte[] SerializeSharedGameplayHashState()
    {
        using (MemoryStream memoryStream = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(memoryStream))
        {
            bw.Write(roundOver);
            bw.Write(gameOver);
            bw.Write(currentStageIndex);

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    bw.Write(damageMatrix[i, j]);
                }
            }

            bw.Write(rngState);
            bw.Write(ramNeededToWinRound);

            return memoryStream.ToArray();
        }
    }

    public byte[] SerializeProjectileHashState()
    {
        using (MemoryStream memoryStream = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(memoryStream))
        {
            List<BaseProjectile> activeProjectiles = ProjectileManager.Instance.projectilePrefabs
                .Where(projectile => projectile != null && projectile.gameObject.activeSelf)
                .ToList();
            bw.Write(activeProjectiles.Count);
            foreach (BaseProjectile projectile in activeProjectiles)
            {
                int prefabIndex = ProjectileManager.Instance.projectilePrefabs.IndexOf(projectile);
                bw.Write(prefabIndex);
                projectile.Serialize(bw);
            }

            return memoryStream.ToArray();
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
                roundEndFrameCounter = br.ReadInt32();
                int savedStageIndex = br.ReadInt32();
                if (savedStageIndex != currentStageIndex)
                {
                    SetStage(savedStageIndex);
                }

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
                randomCallCount = br.ReadInt32();
                rngState = br.ReadUInt32(); // Restore exact RNG state directly
                stageRngState = br.ReadUInt32();

                // Deserialize round state
                ramNeededToWinRound = br.ReadUInt16();
                roundEndUIShown = br.ReadBoolean();
                lastRoundWinnerPID = br.ReadInt32();
                int savedTotalRoundsPlayed = br.ReadInt32();
                if (dataManager == null)
                {
                    dataManager = DataManager.Instance;
                }
                if (dataManager != null)
                {
                    dataManager.totalRoundsPlayed = savedTotalRoundsPlayed;
                }

                bool includeLobbyShopState = br.ReadBoolean();
                if (includeLobbyShopState)
                {
                    // Deserialize remaining game stages
                    int savedStageCount = br.ReadInt32();
                    gameStages.Clear();
                    for (int i = 0; i < savedStageCount; i++)
                    {
                        int stageIdx = br.ReadInt32();
                        if (stageIdx >= 0 && stageIdx < stages.Count)
                        {
                            gameStages.Add(stages[stageIdx]);
                        }
                    }

                    p1_shopIndex = br.ReadInt32();
                    p2_shopIndex = br.ReadInt32();
                    p1_lastCycleFrame = br.ReadInt32();
                    p2_lastCycleFrame = br.ReadInt32();

                    // Deserialize shop spell choices
                    List<string> savedP1Choices = DeserializeStringList(br);
                    List<string> savedP2Choices = DeserializeStringList(br);

                    for (int i = 0; i < playerCount; i++)
                    {
                        players[i].chosenSpell = br.ReadBoolean();
                    }
                }

                // Projectile State 
                int savedProjectileCount = br.ReadInt32();
                List<BaseProjectile> masterList = ProjectileManager.Instance.projectilePrefabs;
                List<BaseProjectile> currentlyActive = masterList
                    .Where(projectile => projectile != null && projectile.gameObject.activeSelf)
                    .ToList();
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

                ProjectileManager.Instance.SynchronizeActiveProjectiles();

                bool hasLobbyShopTail = br.ReadBoolean();
                if (hasLobbyShopTail)
                {
                    int gateCount = br.ReadInt32();
                    for (int i = 0; i < gateCount; i++)
                    {
                        if (i < gates.Length && gates[i] != null)
                        {
                            gates[i].Deserialize(br);
                        }
                    }

                    int gambaCount = br.ReadInt32();
                    for (int i = 0; i < gambaCount; i++)
                    {
                        int activatedCount = br.ReadInt32();
                        byte resetTimer = br.ReadByte();
                        int startingSpellPos = br.ReadInt32();
                        bool isActive = br.ReadBoolean();
                        if (i < gambas.Count)
                        {
                            GambaMachine gamba = gambas[i].GetComponent<GambaMachine>();
                            if (gamba != null)
                            {
                                gamba.activatedCount = activatedCount;
                                gamba.resetTimer = resetTimer;
                                gamba.SetStartingSpellPos(startingSpellPos);
                                if (gamba.gambaAnimator != null)
                                {
                                    gamba.gambaAnimator.SetBool("isActive", isActive);
                                }
                            }
                        }
                    }
                }

                // Resolve References
                // Call ResolveReferences on players if they need it (unlikely for player->spell)
                // Call ResolveReferences on all *active* projectiles
                foreach (BaseProjectile projectile in ProjectileManager.Instance.projectilePrefabs.Where(p => p != null && p.gameObject.activeSelf))
                {
                    projectile.ResolveReferences();
                }
                for (int i = 0; i < playerCount; i++)
                {
                    if (players[i] != null)
                        players[i].ResolveReferences();
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

    /// <summary>
    /// Allocate space for and randomize the array of stages that a game can choose from. No duplicate stages are allowed in this array
    /// </summary>
    private void RandomizeGameStages()
    {
        //copy all stages from stages into gameStages
        gameStages = new List<StageDataSO>(stages);

        //Debug.Log("Before culling: gameStages.Count = " + gameStages.Count);

        //delete random stages from gameStages until gameStages.Length equals 9
        while (gameStages.Count > 9)
        {
            gameStages.RemoveAt(GetNextStageRandom(0, gameStages.Count));
        }

        //Debug.Log("After culling: gameStages.Count = " + gameStages.Count);
    }
}
