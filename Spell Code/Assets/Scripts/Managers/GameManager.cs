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

    public static Dictionary<string, Color> colors = new Dictionary<string, Color>
    {
        { "red", HexToColor("#ff424f") },
        { "green", HexToColor("#6cb328") },
        { "blue", HexToColor("#409def") },
        { "yellow", HexToColor("#fbc800") },
        { "white", HexToColor("#ffffff") },
        { "purple", HexToColor("#b44cef") },
        { "pink", HexToColor("#ec8cff") },
        { "gold", HexToColor("#dd8c00") },
        { "grey", HexToColor("#998d86") },
        { "black", HexToColor("#000000") }
    };

    private static Color HexToColor(string hexCode)
    {
        ColorUtility.TryParseHtmlString(hexCode, out Color color);
        return color;
    }

    public GameObject MainMenuScreen;

    public GameObject playerPrefab;
    public PlayerController[] players = new PlayerController[4];
    public List<PlayerController> playerNPCs = new List<PlayerController>();
    public int playerCount = 0;
    [NonSerialized]
    public ushort ramNeededToWinRound = 1;
    public static ushort baseRamNeeddedtowin = 400;

    public SpriteRenderer shopImage;

    [NonSerialized]
    public PlayerController bigWinner = null;
    public bool endInputEnabled = false;
    [NonSerialized]
    public int endWinnerPid = -1;
    [NonSerialized]
    public Texture2D endWinnerPalette = null;

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
    public TempSpellDisplay[] spellDisplays = new TempSpellDisplay[4];
    public TempUIScript tempUI;
    public List<StageDataSO> stages;
    [SerializeField] private List<StageDataSO> gameStages = new List<StageDataSO>();
    public StageDataSO lobbySO;
    public StageDataSO TutorialSO;
    public StageDataSO trainingGroundsSO;
    public StageDataSO soloLobbySO;
    // public StageDataSO currentStage;
    public int currentStageIndex = 0;
    public SceneUiManager sceneManager;

    public List<GameObject> tempMapGOs = new List<GameObject>();
    public GameObject lobbyMapGO;
    public GameObject tutorialMapGO;
    public GameObject trainingGroundsGO;
    public GameObject soloLobbyGO;
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

    public GameObject buttons;

    [Header("Online UI")]
    public GameObject networkInfo;
    public TextMeshProUGUI pingText;
    public TextMeshProUGUI rollbackFramesText;
    private const float NETWORK_INFO_DISPLAY_REFRESH_SECONDS = 2f;
    private float nextNetworkInfoDisplayRefreshTime = 0f;

    [Header("Online Match State")]
    public bool isWaitingForOpponent = false;
    public bool opponentIsReady = false;
    private float lobbyWaitStartTime = 0f;
    private float LOBBY_TIMEOUT = 30f;
    // Network health tracking (uses real time, not frames)
    private float lastPacketReceivedTime = 0f;
    private const float NETWORK_TIMEOUT = 10f;
    private const float TRANSITION_NETWORK_GRACE_SECONDS = 10f;

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
    private OnlineMatchRoster activeOnlineRoster;
    private readonly Dictionary<int, Steamworks.SteamId> onlineSlotToPeer = new Dictionary<int, Steamworks.SteamId>();
    private readonly Dictionary<Steamworks.SteamId, int> onlinePeerToSlot = new Dictionary<Steamworks.SteamId, int>();
    private readonly HashSet<int> onlineDisconnectedSlots = new HashSet<int>();
    private readonly HashSet<int> readyPeerSlots = new HashSet<int>();
    private readonly HashSet<int> gameplayReadyPeerSlots = new HashSet<int>();
    private readonly HashSet<int> sceneReadyPeerSlots = new HashSet<int>();
    private readonly Dictionary<int, GameplayReadyContext> pendingGameplayReadyBySlot = new Dictionary<int, GameplayReadyContext>();
    private readonly Dictionary<int, int> pendingGameplayReadyTransitionBySlot = new Dictionary<int, int>();
    private readonly Dictionary<int, (int transitionId, byte sceneType, int sceneSignature)> pendingSceneReadyBySlot = new Dictionary<int, (int transitionId, byte sceneType, int sceneSignature)>();
    private int timeoutFrames = 0; // Timeout counter
    public int randomSeed = 0;
    public int randomCallCount = 0;
    private uint rngState = 0;
    private uint stageRngState;


    // Host-side counterpart of ApplyOnlineGameplayRngState
    private bool hasPendingHostGameplayRngRestore = false;
    private uint pendingHostGameplayRngRestoreState = 0;
    private int pendingHostGameplayRngRestoreCallCount = -1;

    public uint CurrentRngState => rngState;
    public uint CurrentStageRngState => stageRngState;
    public int CurrentTotalRoundsPlayed
    {
        get
        {
            if (dataManager == null)
            {
                dataManager = DataManager.Instance;
            }

            return dataManager != null ? dataManager.totalRoundsPlayed : 0;
        }
    }

    [Header("Debug")]
    public bool logDesyncTrace = false;
    public int logDesyncEveryNFrames = 1;
    // When true, GameManager emits [SimDiag] lines that show: which FixedUpdate early-return
    // path is hit (rate-limited so it doesn't spam), and a heartbeat every 60 sim frames from
    // RunOnlineFrame showing current frame, wall-clock time, and frames-per-second cadence.
    // Use this when a peer's sim appears to drift without any of the existing hold/rollback
    // logs explaining it. Off in production.
    public bool logSimDiagnostics = false;
    private float lastSimSkipLogTime = -1f;
    private string lastSimSkipReason = null;
    private int lastSimHeartbeatFrame = -1;
    private float lastSimHeartbeatTime = -1f;

    // Online lobby state tracking
    public bool localPlayerReadyForGameplay = false;
    public bool remotePlayerReadyForGameplay = false;
    private enum GameplayReadyContext
    {
        None,
        Lobby,
        Shop
    }
    private GameplayReadyContext localGameplayReadyContext = GameplayReadyContext.None;
    private GameplayReadyContext remoteGameplayReadyContext = GameplayReadyContext.None;
    private GameplayReadyContext pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
    private int onlineTransitionSequence = 0;
    private int activeOnlineTransitionId = 0;
    private int lastAppliedGameplayStageTransitionId = 0;
    private int localGameplayReadyTransitionId = 0;
    private int remoteGameplayReadyTransitionId = 0;
    private int pendingRemoteGameplayReadyTransitionId = 0;
    private bool hasPendingStageSelect = false;
    private int pendingStageSelectTransitionId = 0;
    private byte pendingStageSelectSceneType = 0;
    private int pendingStageSelectSceneSignature = 0;
    private int pendingStageSelectIndex = -1;
    private uint pendingStageSelectRngState = 0;
    private int pendingStageSelectTotalRoundsPlayed = -1;
    private uint pendingStageSelectGameplayRngState = 0;
    private int pendingStageSelectRandomCallCount = -1;
    private bool localSceneTransitionReady = false;
    private bool remoteSceneTransitionReady = false;
    private bool hasPendingRemoteSceneReady = false;
    private int pendingRemoteSceneReadyTransitionId = 0;
    private byte pendingRemoteSceneReadyType = 0;
    private int pendingRemoteSceneReadySignature = 0;
    private int pendingOpponentShopTransitionId = 0;
    [HideInInspector]
    public int p1_shopIndex = 0;
    [HideInInspector]
    public int p2_shopIndex = 0;
    [HideInInspector]
    public int p3_shopIndex = 0;
    [HideInInspector]
    public int p4_shopIndex = 0;

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
            Application.runInBackground = true;
            // optional: prevent the gameobject from being destroyed when loading new scenes
            DontDestroyOnLoad(gameObject);

        }
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void SetResolution()
    {
        Vector2Int displaySize = GetActiveDisplaySize();
        if (displaySize.x <= 0 || displaySize.y <= 0)
        {
            return;
        }

        const float targetAspect = 16f / 9f;
        float displayAspect = (float)displaySize.x / displaySize.y;

        int targetWidth;
        int targetHeight;

        if (displayAspect >= targetAspect)
        {
            targetHeight = displaySize.y;
            targetWidth = Mathf.RoundToInt(targetHeight * targetAspect);
        }
        else
        {
            targetWidth = displaySize.x;
            targetHeight = Mathf.RoundToInt(targetWidth / targetAspect);
        }

        targetWidth = Mathf.Max(1, targetWidth);
        targetHeight = Mathf.Max(1, targetHeight);

        Screen.SetResolution(targetWidth, targetHeight, Screen.fullScreenMode);
    }

    private Vector2Int GetActiveDisplaySize()
    {
        DisplayInfo displayInfo = Screen.mainWindowDisplayInfo;
        if (displayInfo.width > 0 && displayInfo.height > 0)
        {
            return new Vector2Int(displayInfo.width, displayInfo.height);
        }

        Resolution currentResolution = Screen.currentResolution;
        if (currentResolution.width > 0 && currentResolution.height > 0)
        {
            return new Vector2Int(currentResolution.width, currentResolution.height);
        }

        if (Display.main != null && Display.main.systemWidth > 0 && Display.main.systemHeight > 0)
        {
            return new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
        }

        return new Vector2Int(Screen.width, Screen.height);
    }

    public void ExecuteOrder66(string scene)
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
        SceneManager.LoadScene(scene);
        //Camera.main.GetComponentInChildren<Image>().enabled = false;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ApplySettings();
        }
        else
        {
            SetResolution();
        }

        isOnlineMatchActive = false;
        isWaitingForOpponent = false;
        opponentIsReady = false;
        isTransitioning = false;
        localSceneTransitionReady = false;
        remoteSceneTransitionReady = false;
        hasPendingRemoteSceneReady = false;
        pendingRemoteSceneReadyType = 0;
        pendingRemoteSceneReadySignature = 0;
        frameNumber = 0;

        isRunning = true;
        isSaved = false;

        playerWinText.enabled = false;
        playerInputManager = GetComponent<PlayerInputManager>();
        dataManager = DataManager.Instance;

        //goDoorPrefab = GetComponentInChildren<GO_Door>();

        int offlineSeed = UnityEngine.Random.Range(1, int.MaxValue);
        seededRandom = new System.Random(offlineSeed);
        InitializeWithSeed(offlineSeed);


        // A fresh GameManager wakes either at app launch (SoloLobby) or after an ExecuteOrder66
        // into MainMenu/SoloLobby. MainMenu must get the lobby stage immediately: the stage index
        // rides every input packet (GetNetworkSceneSignature), and a deferred online host/join
        // waits in MainMenu before StartOnlineMatch would otherwise correct it — with -4 the
        // player would be standing in solo-lobby geometry inside MainMenu until then.
        SetStage(SceneManager.GetActiveScene().name == "MainMenu" ? -1 : -4);

        SetNetworkInfoVisible(isOnlineMatchActive);
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
            string offlineSceneName = SceneManager.GetActiveScene().name;
            gameObject.GetComponent<PlayerInputManager>().enabled = offlineSceneName == "MainMenu" || 
                                                                    (offlineSceneName == "SoloLobby" && playerCount == 0);
            SetNetworkInfoVisible(false);
        }
        else
        {
            // Keep it disabled during online matches
            if (playerInputManager != null && playerInputManager.enabled)
            {
                playerInputManager.enabled = false;
            }

            SetNetworkInfoVisible(true);
            UpdateNetworkInfoDisplay();
        }


        //if ` is pressed, toggle box rendering
        if (UnityEngine.Input.GetKeyDown(KeyCode.BackQuote))
        {
            BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
        }

#if UNITY_EDITOR
        //if = is pressed, player 1 win
        if (UnityEngine.Input.GetKeyDown(KeyCode.Equals))
        {
            players[0].roundRam = 600;
        }

        if (UnityEngine.Input.GetKeyDown(KeyCode.RightBracket))
        {
            loadSoloLobby();
        }

        if (UnityEngine.Input.GetKeyDown(KeyCode.LeftBracket))
        {
            players[0].ClearSpellList();
        }

        //remove player test key ","
        if (UnityEngine.Input.GetKeyDown(KeyCode.Comma)) { Destroy(players[0].gameObject); players[0] = null; playerCount--; }//players[0].inputs.InputDevice }
#endif
    }
    public void loadMainMenu()
    {
        sceneManager.LoadScene("MainMenu");
        SetStage(-1);
        ResetPlayers();
        players[0].ClearSpellList();
    }

    public void LoadTutorial()
    {
        
        sceneManager.LoadScene("Tutorial");
        SetStage(-2);
        ResetPlayers();
        players[0].ClearSpellList();
    }

    public void loadTrainingGrounds()
    {
        sceneManager.LoadScene("TrainingGrounds");
        SetStage(-3);
        ResetPlayers();
        players[0].ClearSpellList();
    }

    public void loadSoloLobby()
    {
        sceneManager.LoadScene("SoloLobby");
        SetStage(-4);
        ResetPlayers();
        players[0].ClearSpellList();
    }

    private void FixedUpdate()
    {
        //if (prevSceneWasShop)
        //{
        //    ResetPlayers();
        //    prevSceneWasShop = false;
        //}

        if (isTransitioning)
        {
            LogSimSkip("isTransitioning");
            return;
        }
        Scene activeScene = SceneManager.GetActiveScene();

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
            LogSimSkip("isWaitingForOpponent");
            return; // Don't run simulation yet
        }

        if (isOnlineMatchActive && !IsOnlineSimulationScene(activeScene))
        {
            LogSimSkip($"wrong scene '{activeScene.name}'");
            return;
        }

        if (isOnlineMatchActive && isRunning)
        {
            if (!CheckNetworkHealth(out string networkFailureReason))
            {
                StopMatch(networkFailureReason);
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

    private bool IsOnlineSimulationScene(Scene scene)
    {
        return scene.name == "MainMenu" || scene.name == "Gameplay" || scene.name == "Shop";
    }

    private ulong GatherInputForOnline()
    {
        PlayerController localPlayer = localPlayerIndex >= 0 && localPlayerIndex < players.Length
            ? players[localPlayerIndex]
            : null;

        if (StressTestController.Instance != null && StressTestController.Instance.UseDeterministicInput)
        {
            ulong stressInput = StressTestController.Instance.GetDeterministicInput(frameNumber);
            return PlayerController.PackOnlineControlOptions(stressInput, localPlayer);
        }

        if (localPlayer != null && localPlayer.inputs.IsActive)
        {
            if (localPlayer.IsLocalOnlinePauseMenuOpen())
            {
                // Only NEW frames (current + InputDelay onward) go neutral while paused. The
                // already-buffered frames were sent to peers and must play out unchanged —
                // rewriting them (the old NeutralizePendingLocalInputs) desyncs at high ping
                // because peers have already verified those frames and drop the correction.
                return PlayerController.PackOnlineControlOptions(5UL, localPlayer);
            }

            ulong input = (ulong)localPlayer.inputs.UpdateInputs();
            return PlayerController.PackOnlineControlOptions(input, localPlayer);
        }
        return PlayerController.PackOnlineControlOptions(5UL, localPlayer); // neutral
        //return GatherRawInput(); // fallback to raw input gathering if player controller or inputs are not available
    }

    private InputDevice[] GetOnlineSharedInputDevices()
    {
        return InputSystem.devices
            .Where(InputDeviceManager.IsValidInput)
            .Distinct()
            .ToArray();
    }

    private void ConfigureOnlineLocalPlayerInput(PlayerInput playerInput, InputPlayerBindings bindings)
    {
        InputDevice[] sharedDevices = GetOnlineSharedInputDevices();

        if (playerInput != null)
        {
            playerInput.ActivateInput();
            playerInput.actions.bindingMask = null;

            if (playerInput.currentActionMap != null)
            {
                playerInput.currentActionMap.bindingMask = null;
            }

            if (playerInput.user.valid)
            {
                foreach (InputDevice device in sharedDevices)
                {
                    InputUser.PerformPairingWithDevice(device, playerInput.user);
                }
            }
        }

        bindings?.AllowAllBindingGroups();
        bindings?.ConfigureInputDevices(sharedDevices);
    }

    private void MarkOnlineRemotePlayerInputInactive(PlayerController player)
    {
        player?.inputs?.SetActiveWithoutChangingActions(false);
    }

    private void EnsureOnlineLocalPlayerInputActive()
    {
        if (localPlayerIndex < 0 || localPlayerIndex >= players.Length)
        {
            return;
        }

        PlayerController localPlayer = players[localPlayerIndex];
        if (localPlayer == null)
        {
            return;
        }

        PlayerInput playerInput = localPlayer.GetComponent<PlayerInput>();
        localPlayer.inputs.AssignInputDevice(null);
        ConfigureOnlineLocalPlayerInput(playerInput, localPlayer.inputs);
        SettingsManager.Instance?.TryApplyControlOptionsForPlayer(localPlayer);
        localPlayer.CheckForInputs(true, false);
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

    private void ResolveNetworkInfoReferences()
    {
        if (networkInfo == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == "NetworkInfo")
                {
                    networkInfo = child.gameObject;
                    break;
                }
            }
        }

        if (networkInfo == null)
        {
            return;
        }

        TextMeshProUGUI[] texts = networkInfo.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI text in texts)
        {
            if (text.name == "PingText")
            {
                pingText = text;
            }
            else if (text.name == "RollbackFramesText")
            {
                rollbackFramesText = text;
            }
        }
    }

    private void SetNetworkInfoVisible(bool isVisible)
    {
        ResolveNetworkInfoReferences();

        if (networkInfo != null && networkInfo.activeSelf != isVisible)
        {
            networkInfo.SetActive(isVisible);
        }

        if (!isVisible)
        {
            nextNetworkInfoDisplayRefreshTime = 0f;
        }
    }

    private void UpdateNetworkInfoDisplay()
    {
        if (UnityEngine.Time.unscaledTime < nextNetworkInfoDisplayRefreshTime)
        {
            return;
        }

        nextNetworkInfoDisplayRefreshTime = UnityEngine.Time.unscaledTime + NETWORK_INFO_DISPLAY_REFRESH_SECONDS;

        if (pingText != null && MatchMessageManager.Instance != null)
        {
            pingText.SetText($"RTT: {MatchMessageManager.Instance.Ping}");
        }

        if (rollbackFramesText != null && RollbackManager.Instance != null)
        {
            rollbackFramesText.SetText($"Rollback Frames: {RollbackManager.Instance.RollbackFrames}");
        }
    }

    // Match Control Methods


    /// <summary>
    /// Initializes and starts an online match. Requires RollbackManager.
    /// </summary>
    // Closes the local pause menu if it is open and guarantees real-time playback. Called when an
    // online match starts so a pre-match pause (Time.timeScale=0) cannot freeze the
    // FixedUpdate-driven online simulation. timeScale and the pause UI are purely local and
    // cosmetic, so this has zero effect on the deterministic simulation or its hashes.
    private void ForceResumeLocalPauseMenuForOnline()
    {
        if (tempUI != null)
        {
            Pause pauseMenu = tempUI.gameObject.GetComponent<Pause>();
            if (pauseMenu != null && pauseMenu.paused)
            {
                pauseMenu.Resume();
            }
        }

        // Hard guarantee regardless of menu state: an active online match always runs at real time.
        Time.timeScale = 1f;
    }

    public void StartOnlineMatch(OnlineMatchRoster roster)
    {
        if (roster == null || roster.PlayerCount < 2)
        {
            return;
        }

        onboardManager = null;
        if (RollbackManager.Instance == null)
        {
            return;
        }

        // An online match must never start with the local sim frozen. The pause menu sets
        // Time.timeScale=0 while in menus, and Unity halts FixedUpdate -- and therefore
        // RunOnlineFrame -- entirely at timeScale=0. If the player had the pause menu open when an
        // invite arrived, the match would begin with the sim dead: this client can't advance, send
        // inputs, or run its bootstrap, so it reads as the slowest peer and drags every client
        // until the player happens to touch the menu again (the "fixes after the snapshot"
        // symptom). This runs from the network receive path in Update, which is not gated by
        // timeScale, so it reliably fires even while the client is frozen.
        ForceResumeLocalPauseMenuForOnline();

        RollbackManager.Instance.InputDelay = Mathf.Max(RollbackManager.Instance.InputDelay, 3);
        onlineDisconnectedSlots.Clear();
        ResetOnlineRosterState();
        activeOnlineRoster = roster;
        syncedInput = new ulong[Mathf.Max(2, roster.PlayerCount)];
        localPlayerIndex = roster.LocalPlayerSlot;
        remotePlayerIndex = -1;

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer == null)
            {
                continue;
            }

            onlineSlotToPeer[peer.PlayerSlot] = peer.SteamId;
            onlinePeerToSlot[peer.SteamId] = peer.PlayerSlot;
            if (peer.PlayerSlot != localPlayerIndex && remotePlayerIndex < 0)
            {
                remotePlayerIndex = peer.PlayerSlot;
            }
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

        isOnlineMatchActive = false;
        isWaitingForOpponent = false;
        opponentIsReady = false;
        isRunning = false;
        isTransitioning = false;
        localPlayerReadyForGameplay = false;
        remotePlayerReadyForGameplay = false;
        localGameplayReadyContext = GameplayReadyContext.None;
        remoteGameplayReadyContext = GameplayReadyContext.None;
        pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
        hasPendingStageSelect = false;
        pendingStageSelectSceneType = 0;
        pendingStageSelectSceneSignature = 0;
        pendingStageSelectIndex = -1;
        pendingStageSelectRngState = 0;
        pendingStageSelectTotalRoundsPlayed = -1;
        localSceneTransitionReady = false;
        remoteSceneTransitionReady = false;
        hasPendingRemoteSceneReady = false;
        ResetOnlineTransitionTracking();
        pendingRemoteSceneReadyType = 0;
        pendingRemoteSceneReadySignature = 0;
        readyPeerSlots.Clear();
        gameplayReadyPeerSlots.Clear();
        sceneReadyPeerSlots.Clear();

        if (playerInputManager != null)
        {
            playerInputManager.DisableJoining();
            playerInputManager.enabled = false;
        }

        lobbyWaitStartTime = UnityEngine.Time.unscaledTime;
        lastPacketReceivedTime = 0f;
        ResetMatchState();
        ClearPlayerObjects();
        playerCount = roster.PlayerCount;

        if (playerPrefab == null)
        {
            return;
        }

        for (int i = 0; i < playerCount; i++)
        {
            GameObject p = InstantiateOnlinePlayerObject();
            players[i] = p.GetComponent<PlayerController>();
            AnimationManager.Instance.InitializePlayerVisuals(players[i], i);

            if (players[i].playerNum != null)
            {
                players[i].playerNum.text = "P" + (i + 1);
            }

            PlayerInput pInput = p.GetComponent<PlayerInput>();
            if (i == localPlayerIndex)
            {
                players[i].inputs.AssignInputDevice(null);
                ConfigureOnlineLocalPlayerInput(pInput, players[i].inputs);
                SettingsManager.Instance?.TryApplyControlOptionsForPlayer(players[i]);
                players[i].CheckForInputs(true, false);
            }
            else
            {
                MarkOnlineRemotePlayerInputInactive(players[i]);
            }
        }

        EnsureOnlineLocalPlayerInputActive();

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                players[i].InitCharacter();
            }
        }

        RollbackManager.Instance.Init(roster);
        if (StressTestController.Instance != null && StressTestController.Instance.enableStressTest)
        {
            StressTestController.Instance.ResetForNewMatch();
        }

        MatchMessageManager.Instance?.StartMatch(roster);
        MatchMessageManager.Instance?.SendReadySignal();

        isOnlineMatchActive = true;
        isWaitingForOpponent = true;
        SetNetworkInfoVisible(true);
        ProjectileManager.Instance.InitializeAllProjectiles();
        SetStage(-1);
        ResetPlayers();
        isRunning = true;
    }

    public bool TryRefreshOnlineLobbyRoster(OnlineMatchRoster roster)
    {
        if (!CanStartOrRefreshOnlineLobby(roster) || playerPrefab == null)
        {
            return false;
        }

        ApplyOnlineRoster(roster);

        bool createdPlayer = false;
        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer == null || peer.PlayerSlot < 0 || peer.PlayerSlot >= players.Length)
            {
                return false;
            }

            if (players[peer.PlayerSlot] == null)
            {
                CreateOnlinePlayerForSlot(peer.PlayerSlot, peer.PlayerSlot == localPlayerIndex);
                createdPlayer = true;
            }
        }

        EnsureOnlineLocalPlayerInputActive();

        playerCount = roster.PlayerCount;
        syncedInput = new ulong[Mathf.Max(2, playerCount)];
        if (createdPlayer && ProjectileManager.Instance != null)
        {
            ProjectileManager.Instance.InitializeAllProjectiles();
        }
        PruneOnlineReadyForGameplayState(roster);

        MatchMessageManager.Instance?.UpdateRoster(roster);
        RollbackManager.Instance?.UpdateRoster(roster);
        RollbackManager.Instance?.SaveState();
        ApplyPendingGameplayReadyIfAvailable();
        return true;
    }

    public void TrySendOnlineLobbySnapshotToPeer(Steamworks.SteamId peerId)
    {
        if (!isOnlineMatchActive || !IsOnlineHostAuthority() || MatchMessageManager.Instance == null)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            return;
        }

        MatchMessageManager.Instance.SendLobbyRosterSnapshot(peerId, activeOnlineRoster, frameNumber, SerializeManagedState());
    }

    public void TrySendOnlineLobbyRosterUpdateToExistingPeers(OnlineMatchRoster roster, List<Steamworks.SteamId> excludedPeers)
    {
        if (!isOnlineMatchActive || !IsOnlineHostAuthority() || MatchMessageManager.Instance == null || roster?.Peers == null)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            return;
        }

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer == null
                || peer.PlayerSlot == localPlayerIndex
                || IsSteamIdInList(peer.SteamId, excludedPeers))
            {
                continue;
            }

            MatchMessageManager.Instance.SendLobbyRosterUpdate(peer.SteamId, roster);
        }
    }

    public bool ApplyOnlineLobbyRosterUpdate(OnlineMatchRoster roster)
    {
        if (roster == null || roster.LocalPlayerSlot < 0 || SceneManager.GetActiveScene().name != "MainMenu")
        {
            return false;
        }

        if (!isOnlineMatchActive)
        {
            return false;
        }

        if (DoesActiveOnlineRosterMatch(roster))
        {
            return true;
        }

        bool applied = TryRefreshOnlineLobbyRoster(roster);
        if (applied)
        {
            Debug.Log($"[OnlineLobby] Applied lobby roster update. Players={roster.PlayerCount}");
        }
        return applied;
    }

    public bool ApplyOnlineLobbyRosterSnapshot(OnlineMatchRoster roster, int snapshotFrame, byte[] stateData, bool forceApply = false, byte snapshotSceneType = 0, int snapshotSceneSignature = 0)
    {
        if (roster == null || stateData == null || stateData.Length == 0)
        {
            return false;
        }

        if (roster.LocalPlayerSlot < 0)
        {
            return false;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (!forceApply && activeSceneName != "MainMenu")
        {
            return false;
        }

        if (forceApply)
        {
            byte currentSceneType = GetNetworkSceneTypeCode();
            if (currentSceneType == 0 || activeSceneName == "End")
            {
                return false;
            }

            if (snapshotSceneType != 0 && snapshotSceneType != currentSceneType)
            {
                return false;
            }

            if (currentSceneType == 1 && snapshotSceneSignature != 0 && snapshotSceneSignature != GetNetworkSceneSignature())
            {
                return false;
            }
        }

        bool rosterSnapshotAlreadyActive = isOnlineMatchActive && DoesActiveOnlineRosterMatch(roster);
        bool canRefreshPendingBootstrapSnapshot = rosterSnapshotAlreadyActive
            && RollbackManager.Instance != null
            && RollbackManager.Instance.IsWaitingForInitialRemoteInputStreams()
            && snapshotFrame > frameNumber;

        if (rosterSnapshotAlreadyActive && !canRefreshPendingBootstrapSnapshot && !forceApply)
        {
            Debug.Log($"[OnlineLobby] Ignored duplicate lobby roster snapshot. Players={roster.PlayerCount} Frame={snapshotFrame}");
            return true;
        }

        bool bootstrappedFromSnapshot = false;
        if (!isOnlineMatchActive)
        {
            StartOnlineMatch(roster);
            bootstrappedFromSnapshot = isOnlineMatchActive;
            if (!bootstrappedFromSnapshot)
            {
                return false;
            }
        }

        bool rosterAlreadyApplied = DoesActiveOnlineRosterMatch(roster);
        if (!TryRefreshOnlineLobbyRoster(roster) && !rosterAlreadyApplied)
        {
            return false;
        }

        int previousFrame = frameNumber;
        DeserializeManagedState(stateData);
        ForceSetFrame(snapshotFrame);
        isWaitingForOpponent = false;
        isRunning = true;
        lastPacketReceivedTime = UnityEngine.Time.unscaledTime;
        lobbyWaitStartTime = UnityEngine.Time.unscaledTime;
        RollbackManager.Instance?.UpdateRoster(roster);
        RollbackManager.Instance?.ResetRollbackBaseline(snapshotFrame);
        if (bootstrappedFromSnapshot)
        {
            RollbackManager.Instance?.MarkAllRemoteSlotsPendingUntilInput();
        }
        else if (canRefreshPendingBootstrapSnapshot)
        {
            RollbackManager.Instance?.RebaseActiveRemoteStreamsForLobbySnapshot(previousFrame, snapshotFrame);
        }
        else if (forceApply && activeSceneName == "MainMenu")
        {
            RollbackManager.Instance?.StabilizeLobbySnapshotPacing(snapshotFrame);
        }
        RollbackManager.Instance?.SaveState();
        if (bootstrappedFromSnapshot)
        {
            Debug.Log($"[OnlineLobby] Bootstrapped online lobby from host snapshot. Players={roster.PlayerCount} Frame={snapshotFrame}");
        }
        else if (forceApply)
        {
            Debug.Log($"[OnlineLobby] Applied authoritative lobby state snapshot. Players={roster.PlayerCount} Frame={snapshotFrame}");
        }
        else if (canRefreshPendingBootstrapSnapshot)
        {
            Debug.Log($"[OnlineLobby] Refreshed pending lobby bootstrap snapshot. Players={roster.PlayerCount} Frame={snapshotFrame}");
        }
        else
        {
            Debug.Log($"[OnlineLobby] Applied lobby roster snapshot. Players={roster.PlayerCount} Frame={snapshotFrame}");
        }
        return true;
    }

    private void SendAuthoritativeOnlineLobbySnapshot()
    {
        if (!isOnlineMatchActive
            || !IsOnlineHostAuthority()
            || activeOnlineRoster == null
            || MatchMessageManager.Instance == null
            || !IsOnlineSimulationScene(SceneManager.GetActiveScene()))
        {
            return;
        }

        SendAuthoritativeOnlineLobbySnapshotData(SerializeManagedState());
    }

    // Sends already-serialized authoritative state to every remote peer. Split out from the method
    // above so the authoritative-broadcast path can serialize ONCE and reuse the same bytes for both
    // the network send and the host's own self-apply (see BroadcastAuthoritativeOnlineStateSnapshot).
    private void SendAuthoritativeOnlineLobbySnapshotData(byte[] stateData)
    {
        if (stateData == null || activeOnlineRoster == null || MatchMessageManager.Instance == null)
        {
            return;
        }

        for (int i = 0; i < activeOnlineRoster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = activeOnlineRoster.Peers[i];
            if (peer == null || peer.PlayerSlot == localPlayerIndex || !peer.SteamId.IsValid)
            {
                continue;
            }

            MatchMessageManager.Instance.SendLobbyRosterSnapshot(peer.SteamId, activeOnlineRoster, frameNumber, stateData, forceApply: true);
        }
    }

    public void BroadcastAuthoritativeOnlineStateSnapshot(string reason = "")
    {
        if (!isOnlineMatchActive
            || !IsOnlineHostAuthority()
            || activeOnlineRoster == null
            || MatchMessageManager.Instance == null
            || !IsOnlineSimulationScene(SceneManager.GetActiveScene()))
        {
            return;
        }

        int snapshotFrame = frameNumber;
        byte[] stateData = SerializeManagedState();
        SendAuthoritativeOnlineLobbySnapshotData(stateData);

        // Host self-apply (round-trip)
        DeserializeManagedState(stateData);
        ForceSetFrame(snapshotFrame);
        RollbackManager.Instance?.ResetRollbackBaseline(snapshotFrame);
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            RollbackManager.Instance?.StabilizeLobbySnapshotPacing(snapshotFrame);
        }
        RollbackManager.Instance?.SaveState();

        if (!string.IsNullOrEmpty(reason))
        {
            Debug.Log($"[OnlineState] Broadcast authoritative snapshot after {reason}. Frame={snapshotFrame}");
        }
    }

    private bool IsSteamIdInList(Steamworks.SteamId steamId, List<Steamworks.SteamId> steamIds)
    {
        if (!steamId.IsValid || steamIds == null)
        {
            return false;
        }

        for (int i = 0; i < steamIds.Count; i++)
        {
            if (steamIds[i].IsValid && steamIds[i].Value == steamId.Value)
            {
                return true;
            }
        }

        return false;
    }

    public void OnOnlineLobbySnapshotAcknowledged(Steamworks.SteamId peerId)
    {
        SteamLobbyManager.Instance?.OnLobbySnapshotAcknowledged(peerId);
    }

    public void OnPacketReceived()
    {
        lastPacketReceivedTime = UnityEngine.Time.unscaledTime;
    }

    private void RefreshNetworkActivityGrace()
    {
        lastPacketReceivedTime = UnityEngine.Time.unscaledTime;

        if (RollbackManager.Instance != null)
        {
            RollbackManager.Instance.ResetTimeoutGrace(TRANSITION_NETWORK_GRACE_SECONDS);
        }
    }

    private bool CheckNetworkHealth(out string failureReason)
    {
        failureReason = "Network timeout - connection lost";

        if (MatchMessageManager.Instance != null)
        {
            MatchMessageManager.Instance.PumpNetwork();
        }

        // Don't check during lobby phase
        if (isWaitingForOpponent || isTransitioning)
            return true;

        if (IsRosterBasedOnlineMatch() && MatchMessageManager.Instance != null)
        {
            if (!MatchMessageManager.Instance.HasAllPeersResponsive(NETWORK_TIMEOUT, out int stalePeerSlot))
            {
                failureReason = stalePeerSlot >= 0
                    ? $"Network timeout - peer P{stalePeerSlot + 1} stopped responding"
                    : "Network timeout - connection lost";
                return false;
            }
        }

        // If we haven't received ANY packets yet, give it more time
        if (lastPacketReceivedTime == 0f)
        {
            // Give 15 seconds for initial connection
            if (UnityEngine.Time.unscaledTime - lobbyWaitStartTime > 15f)
            {
                //Debug.LogError("Network timeout - no packets received after 15 seconds");
                failureReason = "Network timeout - no packets received after match start";
                return false;
            }
            return true;
        }

        // Check time since last packet
        float timeSinceLastPacket = UnityEngine.Time.unscaledTime - lastPacketReceivedTime;

        if (timeSinceLastPacket > NETWORK_TIMEOUT)
        {
            //Debug.LogError($"Network timeout - no packets for {timeSinceLastPacket:F1} seconds");
            failureReason = "Network timeout - connection lost";
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
        if (IsOnlineHostAuthority()) // Host generates and sends seed
        {
            MatchMessageManager.Instance.SendRollbackSettings();
            int agreedSeed = UnityEngine.Random.Range(0, 100000);
            InitializeWithSeed(agreedSeed);
            MatchMessageManager.Instance.SendSeed(agreedSeed);
            StartLobbySimulation();
        }
    }

    public void OnPeerReady(int playerSlot)
    {
        if (!isOnlineMatchActive || !isWaitingForOpponent)
        {
            return;
        }

        if (!IsPlayerSlotConnected(playerSlot))
        {
            return;
        }

        readyPeerSlots.Add(playerSlot);
        opponentIsReady = readyPeerSlots.Count > 0;

        if (readyPeerSlots.Count < GetExpectedRemotePeerCount())
        {
            return;
        }

        if (IsOnlineHostAuthority())
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
        ResetOnlineTransitionTracking();

    }

    private int GetExpectedOnlineTransitionId()
    {
        return activeOnlineTransitionId > 0 ? activeOnlineTransitionId : onlineTransitionSequence + 1;
    }

    private void ResetOnlineTransitionTracking()
    {
        onlineTransitionSequence = 0;
        activeOnlineTransitionId = 0;
        lastAppliedGameplayStageTransitionId = 0;
        localGameplayReadyTransitionId = 0;
        remoteGameplayReadyTransitionId = 0;
        pendingRemoteGameplayReadyTransitionId = 0;
        pendingStageSelectTransitionId = 0;
        pendingRemoteSceneReadyTransitionId = 0;
        pendingOpponentShopTransitionId = 0;
        pendingStageSelectTotalRoundsPlayed = -1;
    }

    private void BeginTrackedOnlineTransition(int transitionId)
    {
        activeOnlineTransitionId = transitionId;
        isTransitioning = true;
        localSceneTransitionReady = false;
        remoteSceneTransitionReady = false;
        sceneReadyPeerSlots.Clear();
        pendingSceneReadyBySlot.Clear();
        hasPendingRemoteSceneReady = false;
        pendingRemoteSceneReadyTransitionId = 0;
        pendingRemoteSceneReadyType = 0;
        pendingRemoteSceneReadySignature = 0;
        RefreshNetworkActivityGrace();
    }

    private void CompleteTrackedOnlineTransition()
    {
        if (activeOnlineTransitionId > 0)
        {
            onlineTransitionSequence = Mathf.Max(onlineTransitionSequence, activeOnlineTransitionId);
        }

        activeOnlineTransitionId = 0;
        localSceneTransitionReady = false;
        remoteSceneTransitionReady = false;
        hasPendingRemoteSceneReady = false;
        pendingRemoteSceneReadyTransitionId = 0;
        pendingRemoteSceneReadyType = 0;
        pendingRemoteSceneReadySignature = 0;

        localGameplayReadyTransitionId = 0;
        remoteGameplayReadyTransitionId = 0;
        pendingRemoteGameplayReadyTransitionId = 0;
        hasPendingStageSelect = false;
        pendingStageSelectTransitionId = 0;
        pendingStageSelectSceneType = 0;
        pendingStageSelectSceneSignature = 0;
        pendingStageSelectIndex = -1;
        pendingStageSelectRngState = 0;
        pendingStageSelectTotalRoundsPlayed = -1;
        pendingOpponentShopTransitionId = 0;
        gameplayReadyPeerSlots.Clear();
        sceneReadyPeerSlots.Clear();
        pendingGameplayReadyBySlot.Clear();
        pendingGameplayReadyTransitionBySlot.Clear();
        pendingSceneReadyBySlot.Clear();
        RefreshNetworkActivityGrace();
    }

    /// <summary>
    /// Permanently eliminates a disconnected player from an online match. The player is
    /// left in <c>players[]</c> (slot indices are baked into serialized state, so the
    /// array is never resized) but flagged <c>!isConnected</c> so round/win logic skips it
    /// and it never respawns. Called from the rollback drop path on every surviving peer.
    /// </summary>
    public void MarkPlayerDisconnected(int slot, int frame)
    {
        if (slot < 0 || slot >= players.Length)
        {
            return;
        }

        bool newlyMarked = onlineDisconnectedSlots.Add(slot);
        ApplyDisconnectedPlayerSlot(slot, cleanupProjectiles: true);

        if (newlyMarked)
        {
            PlayerController p = players[slot];
            int pID = p != null ? p.pID : slot + 1;
            Debug.LogWarning($"[GameManager] Player {pID} (slot {slot}) disconnected at frame {frame}; eliminated from match.");
        }
    }

    private void ApplyDisconnectedPlayerSlots(bool cleanupProjectiles)
    {
        if (onlineDisconnectedSlots.Count == 0)
        {
            return;
        }

        foreach (int slot in onlineDisconnectedSlots)
        {
            ApplyDisconnectedPlayerSlot(slot, cleanupProjectiles);
        }
    }

    private void ApplyDisconnectedPlayerSlot(int slot, bool cleanupProjectiles)
    {
        if (slot < 0 || slot >= players.Length)
        {
            return;
        }

        PlayerController p = players[slot];
        if (p != null)
        {
            p.isConnected = false;
            p.isAlive = false;
            p.currentPlayerHealth = 0;

            // Clear the dropped player's lingering shots so every peer converges on the same
            // clean state (mirrors the death cleanup in CheckDeathsAndRoundEnd).
            if (cleanupProjectiles)
            {
                ProjectileManager.Instance?.DeleteTargetPlayerProjectiles(p.pID);
            }

            // Stop the dropped player's looping auras/VFX. A disconnected player no longer runs
            // PlayerUpdate -> UpdateResources, so these would otherwise emit forever (lingering
            // visuals and a steadily worsening particle load that follows the player into the
            // Shop scene). Stop*() is intentionally not rollback-gated, so this clears cleanly.
            if (VFX_Manager.Instance != null)
            {
                VFX_Manager.Instance.StopVisualEffect(VisualEffects.FLOW_STATE_AURA, p.pID, true);
                VFX_Manager.Instance.StopVisualEffect(VisualEffects.DEMON_AURA, p.pID, true);
                VFX_Manager.Instance.StopVisualEffect(VisualEffects.REPS_AURA, p.pID, true);
                VFX_Manager.Instance.StopVisualEffect(VisualEffects.SUPER_ARMOR, p.pID, true);
                VFX_Manager.Instance.StopVisualEffect(VisualEffects.BLOCKING, p.pID, true);
            }
        }

        // Drop the player from all transition bookkeeping so scene transitions, which gate
        // on "all connected players ready", no longer wait on a peer that will never report.
        readyPeerSlots.Remove(slot);
        gameplayReadyPeerSlots.Remove(slot);
        sceneReadyPeerSlots.Remove(slot);
        pendingGameplayReadyBySlot.Remove(slot);
        pendingGameplayReadyTransitionBySlot.Remove(slot);
        pendingSceneReadyBySlot.Remove(slot);
    }

    /// <summary>
    /// True if the player in <paramref name="slot"/> is still connected to the match.
    /// Unknown/out-of-range slots are treated as disconnected.
    /// </summary>
    public bool IsPlayerSlotConnected(int slot)
    {
        return slot >= 0
            && slot < players.Length
            && !onlineDisconnectedSlots.Contains(slot)
            && players[slot] != null
            && players[slot].isConnected;
    }

    /// <summary>
    /// Number of players still connected to an online match.
    /// </summary>
    private int CountConnectedPlayers()
    {
        int count = 0;
        for (int i = 0; i < playerCount; i++)
        {
            if (IsPlayerSlotConnected(i))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Declares the lone remaining connected player the match winner and runs the existing
    /// online end-transition (→ End screen). Invoked when disconnects reduce an online match
    /// to a single player.
    /// </summary>
    public void WinAsLastPlayer()
    {
        if (!isOnlineMatchActive)
        {
            return;
        }

        int winnerSlot = -1;
        for (int i = 0; i < playerCount; i++)
        {
            if (IsPlayerSlotConnected(i))
            {
                winnerSlot = i;
                break;
            }
        }

        if (winnerSlot < 0)
        {
            // No one left (shouldn't normally happen) — just tear the match down.
            StopMatch("All players disconnected");
            return;
        }

        endWinnerPid = players[winnerSlot].pID;
        bigWinner = players[winnerSlot];
        gameOver = true;
        Debug.LogWarning($"[GameManager] Last player standing: P{endWinnerPid} wins the match by disconnect.");
        GameEnd();
    }

    private void ApplyOnlineEndWinner(int winnerPid)
    {
        endWinnerPid = winnerPid;
        bigWinner = winnerPid > 0 && winnerPid <= playerCount ? players[winnerPid - 1] : null;
        endWinnerPalette = bigWinner != null
            && bigWinner.matchPalette != null
            && winnerPid - 1 >= 0
            && winnerPid - 1 < bigWinner.matchPalette.Length
                ? bigWinner.matchPalette[winnerPid - 1]
                : null;
    }

    // Send lobby ready signal
    public void SendLobbyReadyForGameplay()
    {
        if (!isOnlineMatchActive || MatchMessageManager.Instance == null)
            return;

        GameplayReadyContext readyContext = GetCurrentGameplayReadyContext();
        if (readyContext == GameplayReadyContext.None)
        {
            return;
        }

        localPlayerReadyForGameplay = true;
        gameplayReadyPeerSlots.Add(localPlayerIndex);
        localGameplayReadyContext = readyContext;
        localGameplayReadyTransitionId = GetExpectedOnlineTransitionId();
        //Debug.Log("Local player ready for gameplay transition - sending signal");

        if (readyContext == GameplayReadyContext.Shop)
        {
            MatchMessageManager.Instance.SendShopReadySignal(localGameplayReadyTransitionId);
        }
        else
        {
            MatchMessageManager.Instance.SendLobbyReadySignal(localGameplayReadyTransitionId);
        }

        CheckBothPlayersReadyForGameplay();
    }

    public void OnOpponentReadyForGameplayFromLobby(int transitionId)
    {
        OnOpponentReadyForGameplay(remotePlayerIndex, GameplayReadyContext.Lobby, transitionId);
    }

    public void OnOpponentReadyForGameplayFromShop(int transitionId)
    {
        OnOpponentReadyForGameplay(remotePlayerIndex, GameplayReadyContext.Shop, transitionId);
    }

    public void OnPeerReadyForGameplayFromLobby(int playerSlot, int transitionId)
    {
        OnOpponentReadyForGameplay(playerSlot, GameplayReadyContext.Lobby, transitionId);
    }

    public void OnPeerReadyForGameplayFromShop(int playerSlot, int transitionId)
    {
        OnOpponentReadyForGameplay(playerSlot, GameplayReadyContext.Shop, transitionId);
    }

    private void OnOpponentReadyForGameplay(int playerSlot, GameplayReadyContext readyContext, int transitionId)
    {
        if (!IsPlayerSlotConnected(playerSlot))
        {
            return;
        }

        int expectedTransitionId = GetExpectedOnlineTransitionId();
        if (transitionId < expectedTransitionId)
        {
            return;
        }

        if (transitionId > expectedTransitionId || GetCurrentGameplayReadyContext() != readyContext)
        {
            if (IsRosterBasedOnlineMatch())
            {
                pendingGameplayReadyBySlot[playerSlot] = readyContext;
                pendingGameplayReadyTransitionBySlot[playerSlot] = transitionId;
            }
            pendingRemoteGameplayReadyContext = readyContext;
            pendingRemoteGameplayReadyTransitionId = transitionId;
            return;
        }

        remotePlayerReadyForGameplay = true;
        gameplayReadyPeerSlots.Add(playerSlot);
        remoteGameplayReadyContext = readyContext;
        remoteGameplayReadyTransitionId = transitionId;
        CheckBothPlayersReadyForGameplay();
    }

    public void OnOpponentSceneTransitionReady(int transitionId, byte sceneType, int sceneSignature)
    {
        OnPeerSceneTransitionReady(remotePlayerIndex, transitionId, sceneType, sceneSignature);
    }

    public void OnPeerSceneTransitionReady(int playerSlot, int transitionId, byte sceneType, int sceneSignature)
    {
        if (!IsPlayerSlotConnected(playerSlot))
        {
            return;
        }

        if (!isTransitioning)
        {
            return;
        }

        if (transitionId != activeOnlineTransitionId)
        {
            if (transitionId > activeOnlineTransitionId)
            {
                if (IsRosterBasedOnlineMatch())
                {
                    pendingSceneReadyBySlot[playerSlot] = (transitionId, sceneType, sceneSignature);
                }
                hasPendingRemoteSceneReady = true;
                pendingRemoteSceneReadyTransitionId = transitionId;
                pendingRemoteSceneReadyType = sceneType;
                pendingRemoteSceneReadySignature = sceneSignature;
            }
            return;
        }

        if (IsRosterBasedOnlineMatch())
        {
            pendingSceneReadyBySlot[playerSlot] = (transitionId, sceneType, sceneSignature);
        }

        if (sceneType == GetNetworkSceneTypeCode() && sceneSignature == GetNetworkSceneSignature())
        {
            remoteSceneTransitionReady = true;
            sceneReadyPeerSlots.Add(playerSlot);
            CheckSceneTransitionReady();
            return;
        }

        hasPendingRemoteSceneReady = true;
        pendingRemoteSceneReadyTransitionId = transitionId;
        pendingRemoteSceneReadyType = sceneType;
        pendingRemoteSceneReadySignature = sceneSignature;
    }

    // Check if both players are ready to transition
    private void CheckBothPlayersReadyForGameplay()
    {
        GameplayReadyContext currentReadyContext = GetCurrentGameplayReadyContext();
        if (currentReadyContext == GameplayReadyContext.None || isTransitioning)
        {
            return;
        }

        if (IsRosterBasedOnlineMatch())
        {
            if (gameplayReadyPeerSlots.Contains(localPlayerIndex)
                && gameplayReadyPeerSlots.Count >= CountConnectedPlayers()
                && localGameplayReadyTransitionId == GetExpectedOnlineTransitionId())
            {
                BeginTrackedOnlineTransition(GetExpectedOnlineTransitionId());
                LoadRandomGameplayStage();
            }
            return;
        }

        if (localPlayerReadyForGameplay
            && remotePlayerReadyForGameplay
            && localGameplayReadyTransitionId == GetExpectedOnlineTransitionId()
            && remoteGameplayReadyTransitionId == GetExpectedOnlineTransitionId()
            && localGameplayReadyContext == currentReadyContext
            && remoteGameplayReadyContext == currentReadyContext)
        {
            //Debug.Log("Both players ready - transitioning to Gameplay");
            BeginTrackedOnlineTransition(GetExpectedOnlineTransitionId());
            LoadRandomGameplayStage();
        }
    }

    private void CheckSceneTransitionReady()
    {
        if (!isTransitioning)
        {
            return;
        }

        if (IsRosterBasedOnlineMatch())
        {
            if (sceneReadyPeerSlots.Contains(localPlayerIndex) && sceneReadyPeerSlots.Count >= CountConnectedPlayers())
            {
                isTransitioning = false;
                CompleteTrackedOnlineTransition();
            }
            return;
        }

        if (localSceneTransitionReady && remoteSceneTransitionReady)
        {
            isTransitioning = false;
            CompleteTrackedOnlineTransition();
        }
    }

    private void ApplyPendingSceneTransitionReadyIfAvailable()
    {
        if (IsRosterBasedOnlineMatch())
        {
            if (!isTransitioning)
            {
                return;
            }

            List<int> readySlots = new List<int>();
            foreach (KeyValuePair<int, (int transitionId, byte sceneType, int sceneSignature)> pendingReady in pendingSceneReadyBySlot)
            {
                if (pendingReady.Value.transitionId == activeOnlineTransitionId
                    && pendingReady.Value.sceneType == GetNetworkSceneTypeCode()
                    && pendingReady.Value.sceneSignature == GetNetworkSceneSignature())
                {
                    readySlots.Add(pendingReady.Key);
                }
            }

            for (int i = 0; i < readySlots.Count; i++)
            {
                sceneReadyPeerSlots.Add(readySlots[i]);
                pendingSceneReadyBySlot.Remove(readySlots[i]);
            }

            CheckSceneTransitionReady();
            return;
        }

        if (!isTransitioning || !hasPendingRemoteSceneReady)
        {
            return;
        }

        if (pendingRemoteSceneReadyTransitionId != activeOnlineTransitionId
            || pendingRemoteSceneReadyType != GetNetworkSceneTypeCode()
            || pendingRemoteSceneReadySignature != GetNetworkSceneSignature())
        {
            return;
        }

        hasPendingRemoteSceneReady = false;
        pendingRemoteSceneReadyTransitionId = 0;
        pendingRemoteSceneReadyType = 0;
        pendingRemoteSceneReadySignature = 0;
        remoteSceneTransitionReady = true;
        if (remotePlayerIndex >= 0)
        {
            sceneReadyPeerSlots.Add(remotePlayerIndex);
        }
        CheckSceneTransitionReady();
    }

    private GameplayReadyContext GetCurrentGameplayReadyContext()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == "MainMenu")
        {
            return GameplayReadyContext.Lobby;
        }

        if (activeScene.name == "Shop")
        {
            return GameplayReadyContext.Shop;
        }

        return GameplayReadyContext.None;
    }

    private void ApplyPendingGameplayReadyIfAvailable()
    {
        GameplayReadyContext currentReadyContext = GetCurrentGameplayReadyContext();
        if (currentReadyContext == GameplayReadyContext.None)
        {
            return;
        }

        if (IsRosterBasedOnlineMatch())
        {
            List<int> readySlots = new List<int>();
            foreach (KeyValuePair<int, GameplayReadyContext> pendingReady in pendingGameplayReadyBySlot)
            {
                if (pendingReady.Value == currentReadyContext
                    && pendingGameplayReadyTransitionBySlot.TryGetValue(pendingReady.Key, out int pendingTransitionId)
                    && pendingTransitionId == GetExpectedOnlineTransitionId())
                {
                    readySlots.Add(pendingReady.Key);
                }
            }

            for (int i = 0; i < readySlots.Count; i++)
            {
                int slot = readySlots[i];
                pendingGameplayReadyBySlot.Remove(slot);
                pendingGameplayReadyTransitionBySlot.Remove(slot);
                gameplayReadyPeerSlots.Add(slot);
            }

            CheckBothPlayersReadyForGameplay();
            return;
        }

        if (pendingRemoteGameplayReadyContext != currentReadyContext
            || pendingRemoteGameplayReadyTransitionId != GetExpectedOnlineTransitionId())
        {
            return;
        }

        pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
        pendingRemoteGameplayReadyTransitionId = 0;
        remotePlayerReadyForGameplay = true;
        remoteGameplayReadyContext = currentReadyContext;
        remoteGameplayReadyTransitionId = GetExpectedOnlineTransitionId();
        CheckBothPlayersReadyForGameplay();
    }

    public bool HandleOnlineStageSelect(int transitionId, byte packetSceneType, int packetSceneSignature, int stageIndex, uint hostStageRngState, int hostTotalRoundsPlayed = -1, uint hostGameplayRngState = 0, int hostRandomCallCount = -1)
    {
        int expectedTransitionId = GetExpectedOnlineTransitionId();
        byte currentSceneType = GetNetworkSceneTypeCode();
        bool isGameplayStageCorrection = packetSceneType == 1
            && currentSceneType == 1
            && stageIndex >= 0
            && stageIndex < stages.Count
            && stageIndex != currentStageIndex;

        if (packetSceneType == 1
            && transitionId > 0
            && transitionId < lastAppliedGameplayStageTransitionId)
        {
            Debug.LogWarning($"Ignoring stale gameplay stage select packet. Transition={transitionId}, LastApplied={lastAppliedGameplayStageTransitionId}, StageIndex={stageIndex}, CurrentStageIndex={currentStageIndex}");
            return false;
        }

        if (transitionId < expectedTransitionId && !isGameplayStageCorrection)
        {
            return false;
        }

        if (activeOnlineTransitionId > 0 && transitionId != activeOnlineTransitionId && !isGameplayStageCorrection)
        {
            if (transitionId > activeOnlineTransitionId)
            {
                hasPendingStageSelect = true;
                pendingStageSelectTransitionId = transitionId;
                pendingStageSelectSceneType = packetSceneType;
                pendingStageSelectSceneSignature = packetSceneSignature;
                pendingStageSelectIndex = stageIndex;
                pendingStageSelectRngState = hostStageRngState;
                pendingStageSelectTotalRoundsPlayed = hostTotalRoundsPlayed;
                pendingStageSelectGameplayRngState = hostGameplayRngState;
                pendingStageSelectRandomCallCount = hostRandomCallCount;
            }
            return false;
        }

        int currentSceneSignature = GetNetworkSceneSignature();

        if (packetSceneType == 1
            && currentSceneType != 1
            && stageIndex >= 0
            && stageIndex < stages.Count)
        {
            if (activeOnlineTransitionId == 0)
            {
                BeginTrackedOnlineTransition(transitionId);
            }

            if (hostTotalRoundsPlayed >= 0)
            {
                ApplyOnlineTotalRoundsPlayed(hostTotalRoundsPlayed);
            }
            else if (transitionId == expectedTransitionId)
            {
                AdvanceRoundCountOnce();
            }

            ApplyOnlineGameplayRngState(hostGameplayRngState, hostRandomCallCount);
            MarkGameplayStageTransitionApplied(transitionId);
            ApplyOnlineStageSelection(stageIndex, hostStageRngState);
            return true;
        }

        if (packetSceneType == currentSceneType)
        {
            if (activeOnlineTransitionId == 0)
            {
                BeginTrackedOnlineTransition(transitionId);
            }

            if (packetSceneType == 1 && hostTotalRoundsPlayed >= 0)
            {
                ApplyOnlineTotalRoundsPlayed(hostTotalRoundsPlayed);
            }
            else if (packetSceneType == 1 && transitionId == expectedTransitionId)
            {
                AdvanceRoundCountOnce();
            }

            if (packetSceneType == 1)
            {
                ApplyOnlineGameplayRngState(hostGameplayRngState, hostRandomCallCount);
                MarkGameplayStageTransitionApplied(transitionId);
            }

            ApplyOnlineStageSelection(stageIndex, hostStageRngState);
            return true;
        }

        bool isTransientSceneState = isTransitioning
            || currentSceneType == 0
            || currentSceneSignature == 99999
            || currentSceneSignature == 199999
            || currentSceneSignature == 299999;

        if (isTransientSceneState)
        {
            hasPendingStageSelect = true;
            pendingStageSelectTransitionId = transitionId;
            pendingStageSelectSceneType = packetSceneType;
            pendingStageSelectSceneSignature = packetSceneSignature;
            pendingStageSelectIndex = stageIndex;
            pendingStageSelectRngState = hostStageRngState;
            pendingStageSelectTotalRoundsPlayed = hostTotalRoundsPlayed;
            pendingStageSelectGameplayRngState = hostGameplayRngState;
            pendingStageSelectRandomCallCount = hostRandomCallCount;
            return true;
        }

        Debug.LogWarning($"Ignoring stale stage select packet. PacketSceneType={packetSceneType}, LocalSceneType={currentSceneType}, PacketScene={packetSceneSignature}, LocalScene={currentSceneSignature}, StageIndex={stageIndex}");
        return false;
    }

    public void HandleInputSceneSignatureMismatch(int senderSlot, int packetSceneSignature)
    {
        if (!isOnlineMatchActive || !IsOnlineHostAuthority() || MatchMessageManager.Instance == null)
        {
            return;
        }

        int localSceneSignature = GetNetworkSceneSignature();
        bool localGameplay = GetNetworkSceneTypeCode() == 1;
        bool packetGameplay = packetSceneSignature >= 100000 && packetSceneSignature < 200000;
        if (!localGameplay || !packetGameplay || packetSceneSignature == localSceneSignature || currentStageIndex < 0)
        {
            return;
        }

        int transitionId = activeOnlineTransitionId > 0 ? activeOnlineTransitionId : onlineTransitionSequence;
        if (transitionId <= 0)
        {
            transitionId = GetExpectedOnlineTransitionId();
        }

        MatchMessageManager.Instance.SendStageSelect(transitionId, currentStageIndex, stageRngState);
        RefreshNetworkActivityGrace();
    }

    private void MarkGameplayStageTransitionApplied(int transitionId)
    {
        if (transitionId > 0)
        {
            lastAppliedGameplayStageTransitionId = Mathf.Max(lastAppliedGameplayStageTransitionId, transitionId);
        }
    }

    private void ApplyOnlineTotalRoundsPlayed(int totalRoundsPlayed)
    {
        if (dataManager == null)
        {
            dataManager = DataManager.Instance;
        }

        if (dataManager == null)
        {
            return;
        }

        dataManager.totalRoundsPlayed = Mathf.Max(0, totalRoundsPlayed);
        ramNeededToWinRound = (ushort)(300 + 100 * dataManager.totalRoundsPlayed);
        onlineRoundAdvanceApplied = true;
    }

    public void StashHostGameplayRngFromStageSelect(uint sentRngState, int sentRandomCallCount)
    {
        if (!isOnlineMatchActive || !IsOnlineHostAuthority())
        {
            return;
        }

        hasPendingHostGameplayRngRestore = true;
        pendingHostGameplayRngRestoreState = sentRngState;
        pendingHostGameplayRngRestoreCallCount = sentRandomCallCount;
    }

    private void ApplyPendingHostGameplayRngRestoreIfAvailable()
    {
        if (!hasPendingHostGameplayRngRestore)
        {
            return;
        }

        hasPendingHostGameplayRngRestore = false;
        if (!isOnlineMatchActive || !IsOnlineHostAuthority() || GetNetworkSceneTypeCode() != 1)
        {
            return;
        }

        uint discardedState = rngState;
        ApplyOnlineGameplayRngState(pendingHostGameplayRngRestoreState, pendingHostGameplayRngRestoreCallCount);
        if (discardedState != pendingHostGameplayRngRestoreState)
        {
            Debug.Log($"[OnlineState] Host restored gameplay RNG to the stage-select value it broadcast (state {discardedState} -> {pendingHostGameplayRngRestoreState}). Transition-window draws discarded.");
        }
    }

    private void ApplyOnlineGameplayRngState(uint hostGameplayRngState, int hostRandomCallCount)
    {
        if (hostRandomCallCount < 0)
        {
            return;
        }

        rngState = hostGameplayRngState;
        randomCallCount = hostRandomCallCount;
    }

    private void ApplyPendingStageSelectIfAvailable()
    {
        if (!hasPendingStageSelect)
        {
            return;
        }

        if (pendingStageSelectTransitionId != GetExpectedOnlineTransitionId()
            || pendingStageSelectSceneType != GetNetworkSceneTypeCode())
        {
            return;
        }

        hasPendingStageSelect = false;
        if (activeOnlineTransitionId == 0)
        {
            BeginTrackedOnlineTransition(pendingStageSelectTransitionId);
        }
        int pendingIndex = pendingStageSelectIndex;
        int pendingTransitionId = pendingStageSelectTransitionId;
        byte pendingSceneType = pendingStageSelectSceneType;
        uint pendingRngState = pendingStageSelectRngState;
        int pendingTotalRoundsPlayed = pendingStageSelectTotalRoundsPlayed;
        uint pendingGameplayRngState = pendingStageSelectGameplayRngState;
        int pendingRandomCallCount = pendingStageSelectRandomCallCount;
        pendingStageSelectTransitionId = 0;
        pendingStageSelectSceneType = 0;
        pendingStageSelectSceneSignature = 0;
        pendingStageSelectIndex = -1;
        pendingStageSelectRngState = 0;
        pendingStageSelectTotalRoundsPlayed = -1;
        pendingStageSelectGameplayRngState = 0;
        pendingStageSelectRandomCallCount = -1;
        if (pendingSceneType == 1)
        {
            if (pendingTotalRoundsPlayed >= 0)
            {
                ApplyOnlineTotalRoundsPlayed(pendingTotalRoundsPlayed);
            }
            ApplyOnlineGameplayRngState(pendingGameplayRngState, pendingRandomCallCount);
            MarkGameplayStageTransitionApplied(pendingTransitionId);
        }
        ApplyOnlineStageSelection(pendingIndex, pendingRngState);
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
            ResetOnlineTransitionTracking();

            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.LeaveLobby();
            }

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
            SetNetworkInfoVisible(false);
            localPlayerReadyForGameplay = false;
            remotePlayerReadyForGameplay = false;
            onlineDisconnectedSlots.Clear();
            readyPeerSlots.Clear();
            gameplayReadyPeerSlots.Clear();
            sceneReadyPeerSlots.Clear();
            ResetOnlineRosterState();
            localGameplayReadyContext = GameplayReadyContext.None;
            remoteGameplayReadyContext = GameplayReadyContext.None;
            pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
            localGameplayReadyTransitionId = 0;
            remoteGameplayReadyTransitionId = 0;
            pendingRemoteGameplayReadyTransitionId = 0;
            hasPendingStageSelect = false;
            pendingStageSelectTransitionId = 0;
            pendingStageSelectSceneType = 0;
            pendingStageSelectSceneSignature = 0;
            pendingStageSelectIndex = -1;
            pendingStageSelectRngState = 0;
            pendingStageSelectTotalRoundsPlayed = -1;
            localSceneTransitionReady = false;
            remoteSceneTransitionReady = false;
            hasPendingRemoteSceneReady = false;
            pendingRemoteSceneReadyTransitionId = 0;
            pendingRemoteSceneReadyType = 0;
            pendingRemoteSceneReadySignature = 0;

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
        ProjectileManager.Instance.DestroyAllProjectiles();

        //Debug.Log("Match stopped and state reset");
    }

    public void ResetToMainMenuAfterHostDisconnect(string reason = "Host disconnected")
    {
        Debug.LogWarning($"[OnlineMatch] {reason}. Returning surviving players to MainMenu.");
        StopMatch(reason);
        ExecuteOrder66("SoloLobby");
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
        syncedInput = new ulong[Mathf.Max(2, IsRosterBasedOnlineMatch() ? playerCount : 2)];
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
            ApplyPendingStageSelectIfAvailable();
            ApplyPendingGameplayReadyIfAvailable();
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
            ApplyPendingStageSelectIfAvailable();
            ApplyPendingGameplayReadyIfAvailable();
            for (int i = 0; i < playerCount; i++)
            {
                players[i].roundRam = 0; // reset round RAM to prevent carryover from lobby
                players[i].storedKillBonus = 0;
            }
            bool isRollback = RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame;
            goDoorPrefab.CheckOpenDoor();
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
                BeginOnlineShopTransition(pendingOpponentShopTransitionId > 0 ? pendingOpponentShopTransitionId : GetExpectedOnlineTransitionId());
                pendingOpponentShopTransitionId = 0;
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
        if (floppyObjects == null || floppyObjects.Length == 0)
        {
            FindAllFloppyDisks();
        }
        if (floppyObjects == null) return;

        for (int i = 0; i < floppyObjects.Length; i++)
        {
            GameObject floppy = floppyObjects[i];
            if (floppy == null) continue;
            FloppyPickup disk = floppy.GetComponent<FloppyPickup>();
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

        // Round-start registration gate
        if (frameNumber == 0
            && activeOnlineRoster != null
            && playerCount < activeOnlineRoster.PlayerCount)
        {
            Debug.Log($"[OnlineState] Holding round start: {playerCount}/{activeOnlineRoster.PlayerCount} players registered.");
            return;
        }

        LogSimHeartbeatIfDue();

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
        rbManager.DiagBeginTick();
        rbManager.RollbackEvent();

        localPlayerInput = GatherInputForOnline();
        rbManager.SendLocalInput(localPlayerInput);

        if (!rbManager.AllowUpdate())
        {
            return;
        }

        //codePrevFrame = codeCurrentFrame;
        //jumpPrevFrame = jumpCurrentFrame;

        frameNumber++;
        rbManager.DiagMarkAdvance();
        rbManager.MaybeApplyAdaptiveInputDelay();
        syncedInput = rbManager.SynchronizeInput();

        Scene activeScene = SceneManager.GetActiveScene();

        UpdateGameState(syncedInput);

        UpdateSceneLogic(syncedInput);

        // ONLINE LOBBY LOGIC (MainMenu scene)
        if (activeScene.name == "MainMenu")
        {
            if (!rbManager.isRollbackFrame && MainMenuScreen != null && players[0] != null)
            {
                MainMenuScreen.SetActive(false);
            }

            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i] != null)
                {
                    gates[i].SimulateOnline(rbManager.isRollbackFrame);
                }
            }

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
            goDoorPrefab.BroadcastSnapshotForNewOnlineEntries(rbManager.isRollbackFrame);

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
        else if (activeScene.name == "Shop")
        {
            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i] != null)
                {
                    gates[i].SimulateOnline(rbManager.isRollbackFrame);
                }
            }
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

        // BestoNet's CheckTimeSync / StartFrameExtensions / 
        // ExtendFrame trio so that when this client is ahead of the
        // slowest peer, it slows itself down by ~1.5ms/frame instead of letting AllowUpdate's
        // hard hold dominate. Prevents the "everyone holds for the slowest peer" cascade
        // observed with MultiplayerMaxConsecutiveFrameDrops=0.
        rbManager.RunFramePacing();
    }

    // Rate-limited log for "FixedUpdate bailed out before RunOnlineFrame ran" cases. Without
    // this, isTransitioning / isWaitingForOpponent / wrong-scene early returns are silent and
    // we can't tell from the log whether the sim is being held by the netcode or by an outer
    // gate. Logs at most once per second per unique reason so it doesn't spam.
    private void LogSimSkip(string reason)
    {
        if (!logSimDiagnostics) return;
        float now = UnityEngine.Time.unscaledTime;
        if (reason == lastSimSkipReason && now - lastSimSkipLogTime < 1f) return;
        lastSimSkipLogTime = now;
        lastSimSkipReason = reason;
        Debug.Log($"[SimDiag] FixedUpdate skipped ({reason}). isOnline={isOnlineMatchActive} frame={frameNumber}");
    }

    // Heartbeat from RunOnlineFrame. Fires every 60 sim frames and prints the elapsed
    // wall-clock seconds since the previous heartbeat - so divide 60 / elapsedSec to get
    // effective sim Hz. Use this when a peer is drifting and nothing else explains it.
    private void LogSimHeartbeatIfDue()
    {
        if (!logSimDiagnostics) return;
        if (frameNumber - lastSimHeartbeatFrame < 60) return;
        float now = UnityEngine.Time.unscaledTime;
        float elapsed = lastSimHeartbeatTime > 0f ? now - lastSimHeartbeatTime : -1f;
        float effectiveHz = elapsed > 0f ? (frameNumber - lastSimHeartbeatFrame) / elapsed : -1f;
        lastSimHeartbeatFrame = frameNumber;
        lastSimHeartbeatTime = now;
        Debug.Log($"[SimDiag] Heartbeat frame={frameNumber} time={now:F2}s elapsed={elapsed:F3}s effHz={effectiveHz:F1}");
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

        if (roundEndFrameCounter < RoundEndTransitionFrameThreshold)
        {
            roundEndFrameCounter++;
        }

        if (!isRealFrame || roundEndFrameCounter < RoundEndTransitionFrameThreshold)
        {
            roundTransitionPending = roundEndFrameCounter >= RoundEndTransitionFrameThreshold;
            return;
        }

        roundTransitionPending = false;
        roundEndFrameCounter = 0;
        PerformRoundTransition();
    }

    private void SerializeFloppyState(BinaryWriter bw)
    {
        FindAllFloppyDisks();

        GameObject[] activeFloppies = floppyObjects ?? Array.Empty<GameObject>();
        bw.Write(activeFloppies.Length);

        for (int i = 0; i < activeFloppies.Length; i++)
        {
            GameObject floppy = activeFloppies[i];
            FloppyPickup disk = floppy != null ? floppy.GetComponent<FloppyPickup>() : null;
            if (floppy == null || disk == null)
            {
                bw.Write(0);
                bw.Write(string.Empty);
                bw.Write(0f);
                bw.Write(0f);
                bw.Write((byte)0);
                bw.Write(false);
                continue;
            }

            bw.Write(disk.ownerPID);
            bw.Write(disk.diskName ?? string.Empty);
            bw.Write(floppy.transform.position.x);
            bw.Write(floppy.transform.position.y);
            bw.Write(disk.GetSelectHoldCounter());
            bw.Write(disk.IsDescriptionVisible());
        }
    }

    private void DeserializeFloppyState(BinaryReader br)
    {
        int floppyCount = br.ReadInt32();
        savedFloppyStateBuffer.Clear();

        for (int i = 0; i < floppyCount; i++)
        {
            int ownerPid = br.ReadInt32();
            string diskName = br.ReadString();
            float posX = br.ReadSingle();
            float posY = br.ReadSingle();
            byte holdCounter = br.ReadByte();
            bool showDescription = br.ReadBoolean();
            savedFloppyStateBuffer.Add(new SavedFloppyState(ownerPid, diskName, new Vector2(posX, posY), holdCounter, showDescription));
        }

        FindAllFloppyDisks();
        if (floppyObjects != null)
        {
            for (int i = 0; i < floppyObjects.Length; i++)
            {
                GameObject floppy = floppyObjects[i];
                if (floppy == null) continue;

                FloppyPickup disk = floppy.GetComponent<FloppyPickup>();
                int savedIndex = FindMatchingSavedFloppyIndex(disk, floppy.transform.position);
                if (savedIndex < 0)
                {
                    floppy.SetActive(false);
                    Destroy(floppy);
                    continue;
                }

                SavedFloppyState savedFloppy = savedFloppyStateBuffer[savedIndex];
                ApplySavedFloppyState(floppy, disk, savedFloppy);
                savedFloppy.restored = true;
                savedFloppyStateBuffer[savedIndex] = savedFloppy;
            }
        }

        List<GameObject> validGambas = GetValidGambaObjects(refreshIfNeeded: true);
        for (int savedIndex = 0; savedIndex < savedFloppyStateBuffer.Count; savedIndex++)
        {
            SavedFloppyState savedFloppy = savedFloppyStateBuffer[savedIndex];
            if (savedFloppy.restored || savedFloppy.ownerPid <= 0 || string.IsNullOrEmpty(savedFloppy.diskName))
            {
                continue;
            }

            for (int i = 0; i < validGambas.Count; i++)
            {
                GameObject gambaGO = validGambas[i];
                if (gambaGO == null) continue;

                GambaMachine gamba = gambaGO.GetComponent<GambaMachine>();
                if (gamba == null || gamba.ownerPID != savedFloppy.ownerPid)
                {
                    continue;
                }

                GameObject restoredDisk = gamba.SpawnFloppyDisk(savedFloppy.ownerPid, savedFloppy.position, savedFloppy.diskName, false, false);
                if (restoredDisk != null)
                {
                    FloppyPickup disk = restoredDisk.GetComponent<FloppyPickup>();
                    if (disk != null)
                    {
                        ApplySavedFloppyState(restoredDisk, disk, savedFloppy);
                    }
                }
                break;
            }
        }

        FindAllFloppyDisks();
    }

    private int FindMatchingSavedFloppyIndex(FloppyPickup disk, Vector3 currentPosition)
    {
        if (disk == null)
        {
            return -1;
        }

        for (int i = 0; i < savedFloppyStateBuffer.Count; i++)
        {
            SavedFloppyState savedFloppy = savedFloppyStateBuffer[i];
            if (savedFloppy.restored
                || savedFloppy.ownerPid != disk.ownerPID
                || savedFloppy.diskName != disk.diskName
                || !ApproximatelySameFloppyPosition(currentPosition, savedFloppy.position))
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    private static bool ApproximatelySameFloppyPosition(Vector3 currentPosition, Vector2 savedPosition)
    {
        const float tolerance = 0.01f;
        return Mathf.Abs(currentPosition.x - savedPosition.x) <= tolerance
            && Mathf.Abs(currentPosition.y - savedPosition.y) <= tolerance;
    }

    private static void ApplySavedFloppyState(GameObject floppy, FloppyPickup disk, SavedFloppyState savedFloppy)
    {
        if (floppy != null)
        {
            floppy.transform.position = new Vector3(savedFloppy.position.x, savedFloppy.position.y, floppy.transform.position.z);
            floppy.SetActive(true);
        }

        if (disk == null)
        {
            return;
        }

        disk.ownerPID = savedFloppy.ownerPid;
        disk.diskName = savedFloppy.diskName;
        disk.SetSelectHoldCounter(savedFloppy.holdCounter);
        disk.SetDescriptionVisible(savedFloppy.showDescription, false);
    }

    private void PerformRoundTransition()
    {
        if (isOnlineMatchActive && !IsOnlineHostAuthority())
        {
            roundTransitionPending = true;
            return;
        }

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

        bool hasMaxSpells = AllActivePlayersHaveMaxSpells();

        if (hasMaxSpells)
        {
            if (isOnlineMatchActive)
            {
                for (int i = 0; i < playerCount; i++)
                {
                    players[i].roundRam = 0; // reset round RAM
                }
                localPlayerReadyForGameplay = false;
                remotePlayerReadyForGameplay = false;
                gameplayReadyPeerSlots.Clear();
                localGameplayReadyContext = GameplayReadyContext.None;
                remoteGameplayReadyContext = GameplayReadyContext.None;
                pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
                localGameplayReadyTransitionId = 0;
                remoteGameplayReadyTransitionId = 0;
                pendingRemoteGameplayReadyTransitionId = 0;
                hasPendingStageSelect = false;
                pendingStageSelectTransitionId = 0;
                pendingStageSelectSceneType = 0;
                pendingStageSelectSceneSignature = 0;
                pendingStageSelectIndex = -1;
                pendingStageSelectRngState = 0;
                pendingStageSelectTotalRoundsPlayed = -1;
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
            // Match over -> the End scene shows the winner. Keep this banner SHORT (4s) so it does
            // NOT linger onto the End screen: unlike Shop/Gameplay, the End scene has no transition
            // banner of its own to supersede it, so a long-lived banner here bleeds onto it.
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
            string nextPhase = AllActivePlayersHaveMaxSpells() ? "Beginning Next Round..." : "Beginning Shop Phase...";
            message = "Round Ended : Player " + lastRoundWinnerPID + " wins the match! " + nextPhase;

            // Online scene transitions wait for BOTH clients to reach the destination scene
            // (scene-sync); at high ping that can take several seconds with the round over and the
            // sim idle, so a fixed 4s message vanishes and looks frozen. Keep the banner up for the
            // whole wait with a "syncing" note.
            float transitionMessageSeconds = 4f;
            if (isOnlineMatchActive)
            {
                message += " Syncing players...";
                transitionMessageSeconds = 30f;
            }

            if (roundEndedText != null)
            {
                roundEndedText.text = message;
            }
            if (tempUI != null)
            {
                StartCoroutine(tempUI.DisplayTransitionScreen(transitionMessageSeconds, message));
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
        Scene activeScene = SceneManager.GetActiveScene();
        if (playerInputManager != null)
        {
            if (activeScene.name == "MainMenu" || activeScene.name == "SoloLobby")
            {
                playerInputManager.enabled = true;
                playerInputManager.EnableJoining();

                if (playerCount >= 1 && activeScene.name == "SoloLobby")
                {
                    playerInputManager.DisableJoining();
                    playerInputManager.enabled = false;
                }
            }
            else
            {
                playerInputManager.DisableJoining();
                playerInputManager.enabled = false;
            }
        }

        ulong[] inputs = new ulong[playerCount];
        for (int i = 0; i < inputs.Length; ++i)
        {
            inputs[i] = players[i].GetInputs();
        }

        if (activeScene.name == "End")
        {
            if (tempUI != null)
            {
                tempUI.gameObject.SetActive(false);
            }
            for (int i = 0; i < inputs.Length; ++i)
            {
                InputSnapshot inputSnap = InputConverter.ConvertFromLong(inputs[i]);
                if (endInputEnabled && (inputSnap.ButtonStates[1] is ButtonState.Pressed or ButtonState.Held))
                {
                    sceneManager.MainMenu();
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
                players[i].storedKillBonus = 0;
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
            //buttons.SetActive(true);
            if (onboardManager == null)
            {
                onboardManager = FindAnyObjectByType<OnboardManager>();
                onboardManager.enabled = false;
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

        if (activeScene.name == "SoloLobby")
        {
            buttons.SetActive(false);
        }

        if (activeScene.name == "MainMenu")
        {

            goDoorPrefab.CheckOpenDoor();

            if (goDoorPrefab.CheckAllPlayersReady() && goDoorPrefab.isPrimed)
            {
                //if (goDoorPrefab.soloModes)
                //{
                //    goDoorPrefab.isPrimed = false;
                //    tempUI.SetSoloMenuActive(true);
                //}
                //{
                    LoadRandomGameplayStage();
                //}
                
            }

            // if (!isOnlineMatchActive && onlineHostDoor != null)
            // {
            //     onlineHostDoor.CheckOpenDoor();
            //     onlineHostDoor.CheckHostTrigger();
            // }

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
                    else if (AllActivePlayersHaveMaxSpells())
                    {
                        for (int i = 0; i < playerCount; i++)
                        {
                            players[i].roundRam = 0; // reset round RAM
                            players[i].storedKillBonus = 0;
                        }
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
                        // Reset round RAM HERE, not only once the Shop scene loads
                        for (int i = 0; i < playerCount; i++)
                        {
                            players[i].roundRam = 0;
                            players[i].storedKillBonus = 0;
                        }
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

        for (int i = 0; i < playerNPCs.Count; i++)
        {
            playerNPCs[i].PlayerUpdate(5);
        }

        for (int i = 0; i < playerCount; i++)
        {
            if (players[i].isAlive)
            {
                players[i].ProcEffectUpdate();
            }
        }
    }

    private bool AllActivePlayersHaveMaxSpells()
    {
        bool foundActivePlayer = false;

        for (int i = 0; i < playerCount; i++)
        {
            PlayerController player = players[i];
            if (player == null)
            {
                continue;
            }

            if (isOnlineMatchActive && !IsPlayerSlotConnected(i))
            {
                continue;
            }

            foundActivePlayer = true;
            if (player.spellList == null || player.spellList.Count < 6)
            {
                return false;
            }
        }

        return foundActivePlayer;
    }

    //gets called everytime a new player enters, recreates player array
    public void GetPlayerControllers(PlayerInput playerInput)
    {
        if (playerInput == null || playerCount >= players.Length)
        {
            return;
        }

        if (isOnlineMatchActive)
        {
            //Debug.Log("GetPlayerControllers called but online match active - ignoring");
            return;
        }

        // Check if this player is already registered
        PlayerController existingPlayer = playerInput.GetComponent<PlayerController>();
        if (existingPlayer == null)
        {
            return;
        }

        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] == existingPlayer)
            {
                Debug.LogWarning($"Player {existingPlayer.name} already registered at index {i} - ignoring duplicate registration");
                return; // Already registered, don't add again!
            }
        }

        //if this player doesn't have a valid user (aka if its a dummy) add it to playerNPCs instead
        if (!playerInput.user.valid || existingPlayer.npcOverride)
        {
            if (!playerNPCs.Contains(existingPlayer)){
                playerNPCs.Add(existingPlayer);
                Debug.Log("Anotha player NPC added");
                AnimationManager.Instance.InitializePlayerVisuals(existingPlayer, 0);//This currently makes the dummy just always player 1 visuals
            }
            return;
        }

        //Debug.Log($"[GetPlayerControllers] Adding new player. Current playerCount={playerCount}");

        players[playerCount] = existingPlayer;
        players[playerCount].inputs.AssignInputDevice(playerInput.devices[0]);
        SettingsManager.Instance?.TryApplyControlOptionsForPlayer(players[playerCount]);
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

    public void UpdatePlayerBounties(bool applyVisuals = true, bool roundOver = false)
    {
        Debug.Log($"-----------------Updating Bounties------------------");
        ushort averageRoundRam = 0;
        int averageRoundWins = 0;
        //bool disregardRam = false;
        for (int i = 0; i < playerCount; i++)
        {
            averageRoundRam += players[i].roundRam;
            averageRoundWins += players[i].roundsWon;
        }
        averageRoundRam = (ushort)(averageRoundRam / playerCount);
        averageRoundWins = averageRoundWins / playerCount;
        

        for (int i = 0; i < playerCount; i++)
        {
            int ramRoundBounty = roundOver? 0: (players[i].roundRam - averageRoundRam)/3;
            Debug.Log($"Player {i+1} Old Bounty: {players[i].ramBounty}");
            players[i].ramBounty = (short)( ramRoundBounty + (100*(players[i].roundsWon - averageRoundWins)));
            Debug.Log($"Player {i+1} New Bounty: {players[i].ramBounty}");
        }
        
        if (!applyVisuals)
        {
            return;
        }

        UpdateBountyVFX();
    }

    public void UpdateBountyVFX()
    {
        //give the player with the highest bounty the bounty aura VFX
        int playerWithHighestBountyIndex = -1;
        int largestBounty = 0;
        for (int i = 0; i < playerCount; i++)
        {
            //remove the bounty VFX from this player
            players[i].hasHighestBounty = false;
            //VFX_Manager.Instance.StopVisualEffect(VisualEffects.BOUNTY_AURA, i + 1, true);

            if (players[i].ramBounty > largestBounty)
            {
                playerWithHighestBountyIndex = i;
                largestBounty = players[i].ramBounty;
            }

            //Debug.Log("Bounty VFX | Player " + (i + 1) + " has a bounty of " + players[i].ramBounty);
        }
        //Debug.Log("Bounty VFX | Highest bounty player = " + players[playerWithHighestBountyIndex].pID);

        //give the bounty VFX to the player with the highest bounty
        if (playerWithHighestBountyIndex >= 0) players[playerWithHighestBountyIndex].hasHighestBounty = true;
            //VFX_Manager.Instance.PlayAuraVisualEffect(VisualEffects.BOUNTY_AURA, players[playerWithHighestBountyIndex].position + FixedVec2.FromFloat(0f, 102f), playerWithHighestBountyIndex + 1, players[playerWithHighestBountyIndex].gameObject.transform);
        //if (playerWithHighestBountyIndex >= 0) VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BOUNTY_AURA, players[playerWithHighestBountyIndex].position + FixedVec2.FromFloat(0f, 102f), playerWithHighestBountyIndex + 1, true, players[playerWithHighestBountyIndex].gameObject.transform);
    }

    //get the player with the highest bounty but do NOT update bounty VFX. Return -1 if there no player has a bounty
    public int GetPlayerWithHighestBounty()
    {
        //if all bounties are 0,...
        if(AllBountiesAreZero())
        {
            //return -1
            return -1;
        }

        //create a variable to hold the index of the player with the highest bounty
        int _playerWithHighestBountyIndex = 0;

        //iterate through players list and find the player with the highest bounty      
        for (int i = 0; i < playerCount; i++)
        {
            if (players[i].ramBounty > players[_playerWithHighestBountyIndex].ramBounty)
            {
                _playerWithHighestBountyIndex = i;
            }
        }

        //return the player index with the highest bounty
        return _playerWithHighestBountyIndex;
    }

    public bool AllBountiesAreZero()
    {
        //iterate through players array
        for (int i = 0; i < playerCount; i++)
        {
            //if any player bounty is NOT 0,...
            if (players[i].ramBounty != 0)
            {
                //return false
                return false;
            }
        }

        //if all player bounties were 0, return true
        return true;
    }

    public bool CheckDeathsAndRoundEnd(PlayerController[] playerControllers)
    {

        if(roundOver) { return true; }

        bool isRollback = RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame;

        foreach (PlayerController player in playerControllers)
        {
            // Disconnected players are eliminated for good: never respawn, never score.
            if (!player.isConnected) { continue; }

            //check for player deaths
            if(!player.isAlive)
            {
                Debug.Log($"-----------------Player {player.pID} Has just died ------------------");
                //go through each player and award them ram based on the percentage of the other player's health they took (damage matrix)
                foreach (PlayerController p in playerControllers)
                {
                    if (!p.isConnected) { continue; }

                    // Never credit the victim for their own death
                    // Keep their contributions against other players and any legitimate pending kill bonus intact for those players' death payouts
                    if (isOnlineMatchActive && p == player)
                    {
                        damageMatrix[player.pID - 1, p.pID - 1] = 0;
                        Debug.Log($"[OnlineScoring] Player {p.pID} received no RAM for their own death.");
                        continue;
                    }

                    int damagePercent = damageMatrix[player.pID - 1, p.pID - 1];
                    int bountyCut = Math.Max(-PlayerController.baseRamLifeWorth, (damagePercent * player.ramBounty) / 100);
                    int totalKillParticipationRamEarned = damagePercent * PlayerController.baseRamLifeWorth / 100 + bountyCut;
                    // Guard the clamp's MAX with Max(0, ...): on a simultaneous multi-kill the death
                    // loop runs again for this same killer, and an earlier victim's payout (kill
                    // bonus) can already have pushed p.roundRam to/above the threshold. That makes
                    // (ramNeededToWinRound-1-p.roundRam) negative; Mathf.Clamp(x, 0, negative) returns
                    // the negative, and (ushort)(-1)=65535 then overflows p.roundRam back below the
                    // threshold -- which is why killing 2+ players at once failed to win the round
                    // Clamping the max at 0 awards 0 here instead of wrapping.
                    int CollectedGold = Mathf.Clamp(totalKillParticipationRamEarned, 0, Mathf.Max(0, ramNeededToWinRound - 1 - p.roundRam));
                    p.roundRam += (ushort)CollectedGold;
                    p.roundRam = (ushort)Mathf.Clamp(p.roundRam + p.storedKillBonus,0,ramNeededToWinRound);
                    p.SpawnToast($"+{totalKillParticipationRamEarned + p.storedKillBonus} RAM", GameManager.colors["yellow"]);
                    Debug.Log($" player {p.pID}: +{totalKillParticipationRamEarned + p.storedKillBonus} RAM");
                    p.storedKillBonus = 0;
                    

                    damageMatrix[player.pID - 1, p.pID - 1] = 0; //reset damage matrix for next death
                }
                Debug.Log($"-------------------------------------------------------------------");
                

                // Clear lingering projectiles from the dead player so both clients respawn
                // into the same clean state instead of carrying old shots across deaths.
                ProjectileManager.Instance.DeleteTargetPlayerProjectiles(player.pID);

                // Respawn position is deterministic state and must be recomputed during rollback too.
                FixedVec2 spawnPos = GetRandomSpawnVec2();
                player.SpawnPlayer(spawnPos);
            }
        }

        //then check winner conditions (most ram at the end of the round)
        foreach (PlayerController player in playerControllers)
        {
            if (!player.isConnected) { continue; }
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
                            if (!isRollback)
                            {
                                players[i].playerNum.enabled = false;
                                players[i].inputDisplay.enabled = false;
                            }
                            if (players[i].roundsWon >= 3) 
                            { 
                                gameOver = true;
                                bigWinner = winner;
                                endWinnerPid = winner.pID;
                                endWinnerPalette = winner.matchPalette != null
                                    && winner.pID - 1 >= 0
                                    && winner.pID - 1 < winner.matchPalette.Length
                                    ? winner.matchPalette[winner.pID - 1]
                                    : null;
                            }

                        }
                        if (!isRollback)
                        {
                            playerWinText.enabled = true;
                        }
                        UpdatePlayerBounties(!isRollback, true);
                    }
                }
                
                return true;
            }
        }
        UpdatePlayerBounties(!isRollback);
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
                // A disconnected player stays eliminated across rounds — don't respawn it.
                if (!IsPlayerSlotConnected(i))
                {
                    players[i].isConnected = false;
                    players[i].isAlive = false;
                    continue;
                }
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
        onlineDisconnectedSlots.Clear();
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
                players[i].isConnected = true; // Fresh match: clear any prior disconnect.
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
        onlineDisconnectedSlots.Clear();

        SetMenuActive(true);

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
        if (currentStageIndex == -1)
        {
            return lobbySO.playerSpawnTransform;
        }
        if (currentStageIndex == -2)
        {
            return TutorialSO.playerSpawnTransform;
        }
        if (currentStageIndex == -3)
        {
            return trainingGroundsSO.playerSpawnTransform;
        }
        if (currentStageIndex == -4)
        {
            return soloLobbySO.playerSpawnTransform;
        }
        else
        {
            return stages[currentStageIndex].playerSpawnTransform;
        }
    }

    public Vector2[] GetNPCSpawnPositions()
    {
        if (currentStageIndex == -1)
        {
            return lobbySO.npcSpawnTransform;
        }
        if (currentStageIndex == -2)
        {
            return TutorialSO.npcSpawnTransform;
        }
        if (currentStageIndex == -3)
        {
            return trainingGroundsSO.npcSpawnTransform;
        }
        if (currentStageIndex == -4)
        {
            return soloLobbySO.npcSpawnTransform;
        }
        else
        {
            return stages[currentStageIndex].npcSpawnTransform;
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

    public int GetOnlineShopChoiceRandom(int ownerPid, int activationCount, int choiceIndex, int maxValue)
    {
        if (maxValue <= 0)
        {
            return 0;
        }

        unchecked
        {
            uint state = (uint)randomSeed;
            state ^= 0x9E3779B9u * (uint)(CurrentTotalRoundsPlayed + 1);
            state ^= 0x85EBCA6Bu * (uint)Mathf.Max(1, ownerPid);
            state ^= 0xC2B2AE35u * (uint)Mathf.Max(0, activationCount);
            state ^= 0x27D4EB2Fu * (uint)Mathf.Max(1, choiceIndex + 1);

            state ^= state >> 16;
            state *= 0x7FEB352Du;
            state ^= state >> 15;
            state *= 0x846CA68Bu;
            state ^= state >> 16;

            return (int)(state % (uint)maxValue);
        }
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
            int transitionId = GetExpectedOnlineTransitionId();
            if (IsOnlineHostAuthority() && MatchMessageManager.Instance != null)
            {
                SendAuthoritativeOnlineLobbySnapshot();
                MatchMessageManager.Instance.SendShopTransitionSignal(transitionId);
            }
            BeginOnlineShopTransition(transitionId);
            return;
        }
        sceneManager.LoadScene("Shop");
        SetStage(-1);

        //update bounty vfx
        UpdateBountyVFX();
        Debug.Log("HERE");
        //play a new shop song
        //BGM_Manager.Instance.StartAndPlaySong();
    }

    private void BeginOnlineShopTransition(int transitionId)
    {
        if (isTransitioning && SceneManager.GetActiveScene().name == "Shop")
        {
            return;
        }

        BeginTrackedOnlineTransition(transitionId);
        localPlayerReadyForGameplay = false;
        remotePlayerReadyForGameplay = false;
        gameplayReadyPeerSlots.Clear();
        localGameplayReadyContext = GameplayReadyContext.None;
        remoteGameplayReadyContext = GameplayReadyContext.None;
        pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
        localGameplayReadyTransitionId = 0;
        remoteGameplayReadyTransitionId = 0;
        pendingRemoteGameplayReadyTransitionId = 0;
        hasPendingStageSelect = false;
        pendingStageSelectTransitionId = 0;
        pendingStageSelectSceneType = 0;
        pendingStageSelectSceneSignature = 0;
        pendingStageSelectIndex = -1;
        pendingStageSelectRngState = 0;
        pendingStageSelectTotalRoundsPlayed = -1;
        sceneManager.LoadScene("Shop");
        SetStage(-1);
    }

    public void OnOpponentShopTransition(int transitionId, byte sceneType, int sceneSignature)
    {
        OnPeerShopTransition(remotePlayerIndex, transitionId, sceneType, sceneSignature);
    }

    public void OnPeerShopTransition(int playerSlot, int transitionId, byte sceneType, int sceneSignature)
    {
        if (!isOnlineMatchActive)
        {
            return;
        }

        if (IsRosterBasedOnlineMatch() && !IsOnlineHostSlot(playerSlot))
        {
            return;
        }

        int expectedTransitionId = GetExpectedOnlineTransitionId();
        if (transitionId < expectedTransitionId)
        {
            return;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        byte currentSceneType = GetNetworkSceneTypeCode();
        int currentSceneSignature = GetNetworkSceneSignature();

        if (sceneType != 1)
        {
            return;
        }

        if (activeSceneName == "Gameplay"
            && (sceneType != currentSceneType || sceneSignature != currentSceneSignature))
        {
            return;
        }

        if (activeSceneName == "Shop")
        {
            return;
        }

        if (activeSceneName != "Gameplay")
        {
            pendingOpponentShopTransition = true;
            pendingOpponentShopTransitionId = transitionId;
            return;
        }

        if (!roundOver && !isTransitioning)
        {
            roundOver = true;
            roundTransitionPending = true;
            roundEndFrameCounter = RoundEndTransitionFrameThreshold;
        }

        pendingOpponentShopTransition = false;
        pendingOpponentShopTransitionId = 0;
        AdvanceRoundCountOnce();
        BeginOnlineShopTransition(transitionId);
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

        endInputEnabled = false;

        //reset all ram values for players so they don't carry over to the end screen or next match
        for (int i = 0; i < playerCount; i++)
        {
            players[i].roundRam = 0;
            players[i].storedKillBonus = 0;

        }

        gameOver = false;
        roundOver = false;

        dataManager.SaveToFile();
        ProjectileManager.Instance.DestroyAllProjectiles();
        if (isOnlineMatchActive)
        {
            int winnerPid = endWinnerPid > 0 ? endWinnerPid : (bigWinner != null ? bigWinner.pID : -1);
            int transitionId = GetExpectedOnlineTransitionId();
            if (IsOnlineHostAuthority() && MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.SendEndTransitionSignal(transitionId, winnerPid);
            }
            BeginOnlineEndTransition(transitionId, winnerPid);
            return;
        }
        else
        {
            isRunning = false;
        }
        StopAllPlayerAuras();
        sceneManager.LoadScene("End");

        //play a new end song
        //BGM_Manager.Instance.StartAndPlaySong();
    }

    private void BeginOnlineEndTransition(int transitionId, int winnerPid)
    {
        if (isTransitioning && SceneManager.GetActiveScene().name == "End")
        {
            ApplyOnlineEndWinner(winnerPid);
            return;
        }

        ApplyOnlineEndWinner(winnerPid);
        BeginTrackedOnlineTransition(transitionId);
        localPlayerReadyForGameplay = false;
        remotePlayerReadyForGameplay = false;
        gameplayReadyPeerSlots.Clear();
        localGameplayReadyContext = GameplayReadyContext.None;
        remoteGameplayReadyContext = GameplayReadyContext.None;
        pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
        localGameplayReadyTransitionId = 0;
        remoteGameplayReadyTransitionId = 0;
        pendingRemoteGameplayReadyTransitionId = 0;
        hasPendingStageSelect = false;
        pendingStageSelectTransitionId = 0;
        pendingStageSelectSceneType = 0;
        pendingStageSelectSceneSignature = 0;
        pendingStageSelectIndex = -1;
        pendingStageSelectRngState = 0;
        pendingStageSelectTotalRoundsPlayed = -1;

        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] == null) continue;
            players[i].roundRam = 0;
        }

        gameOver = false;
        roundOver = false;
        ProjectileManager.Instance.DeleteAllProjectiles();
        isRunning = false;
        StopAllPlayerAuras();
        sceneManager.LoadScene("End");
    }

    private void StopAllPlayerAuras()
    {
        if (VFX_Manager.Instance == null || players == null)
        {
            return;
        }

        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] == null)
            {
                continue;
            }

            int pid = players[i].pID;
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.FLOW_STATE_AURA, pid, true);
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.DEMON_AURA, pid, true);
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.REPS_AURA, pid, true);
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.STOCK_AURA, pid, true);
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.BOUNTY_AURA, pid, true);
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.SUPER_ARMOR, pid, true);
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.BLOCKING, pid, true);
        }
    }

    public void OnPeerEndTransition(int playerSlot, int transitionId, byte sceneType, int sceneSignature, int winnerPid)
    {
        if (!isOnlineMatchActive)
        {
            return;
        }

        if (IsRosterBasedOnlineMatch() && !IsOnlineHostSlot(playerSlot))
        {
            return;
        }

        int expectedTransitionId = GetExpectedOnlineTransitionId();
        if (transitionId < expectedTransitionId)
        {
            return;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (activeSceneName == "End")
        {
            ApplyOnlineEndWinner(winnerPid);
            return;
        }

        if (activeSceneName == "Gameplay"
            && (sceneType != GetNetworkSceneTypeCode() || sceneSignature != GetNetworkSceneSignature()))
        {
            return;
        }

        BeginOnlineEndTransition(transitionId, winnerPid);
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
        if (currentStageIndex == -2)
        {
            tutorialMapGO.SetActive(true);
            currentStage = tutorialMapGO.name;
            return;
        }
        if (currentStageIndex == -3)
        {
            trainingGroundsGO.SetActive(true);
            currentStage = trainingGroundsGO.name;
            return;
        }
        if (currentStageIndex == -4)
        {
            soloLobbyGO.SetActive(true);
            currentStage = soloLobbyGO.name;
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

    public int GetNetworkSceneSignature()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        int sceneBase;

        switch (activeSceneName)
        {
            case "Gameplay":
                sceneBase = 100000;
                break;
            case "Shop":
                return 199999;
            case "MainMenu":
                sceneBase = 300000;
                break;
            case "End":
                sceneBase = 400000;
                break;
            default:
                sceneBase = 500000;
                break;
        }

        return sceneBase + currentStageIndex;
    }

    public byte GetNetworkSceneTypeCode()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        return activeSceneName switch
        {
            "Gameplay" => 1,
            "Shop" => 2,
            "MainMenu" => 3,
            "End" => 4,
            _ => 0
        };
    }

    public void LoadRandomGameplayStage()
    {
        if (isOnlineMatchActive)
        {
            if (IsOnlineHostAuthority())
            {
                SelectAndBroadcastStage(activeOnlineTransitionId > 0 ? activeOnlineTransitionId : GetExpectedOnlineTransitionId());
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

        ////if gameStages is empty,...
        //if (gameStages.Count <= 0)
        //{
        //    //fill it back up
        //    FillGameStages();
        //}

        //int _gameStageIndex = GetNextRandom(0, gameStages.Count);
        //int _newStageIndex = stages.FindIndex(x => x == gameStages[_gameStageIndex]);

        int _gameStageIndex;
        int _newStageIndex;

        //if gameStages is empty,...
        if (gameStages.Count <= 0)
        {
            //fill gameStages back up
            FillGameStages();

            //Get the stage index of a random non looping stage
            _gameStageIndex = GetStageIndexWithoutLooping();
        }
        else
        {
            //Get the stage index of a random stage
            _gameStageIndex = GetNextStageRandom(0, gameStages.Count);
        }

        //get the actual stage index from gameStageIndex
        _newStageIndex = stages.FindIndex(x => x == gameStages[_gameStageIndex]);

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

    private void SelectAndBroadcastStage(int transitionId)
    {
        int gameStageIndex;
        int newStageIndex;

        //if gameStages is empty,...
        if (gameStages.Count <= 0)
        {
            //fill gameStages back up
            FillGameStages();

            //Get the stage index of a random non looping stage
            gameStageIndex = GetStageIndexWithoutLooping();
        }
        else
        {
            //Get the stage index of a random stage
            gameStageIndex = GetNextStageRandom(0, gameStages.Count);
        }

        //get the actual stage index from gameStageIndex
        newStageIndex = stages.FindIndex(x => x == gameStages[gameStageIndex]);

        if (activeOnlineTransitionId == 0)
        {
            BeginTrackedOnlineTransition(transitionId);
        }

        SendAuthoritativeOnlineLobbySnapshot();
        MarkGameplayStageTransitionApplied(transitionId);
        ApplyOnlineStageSelection(newStageIndex, stageRngState);

        if (MatchMessageManager.Instance != null)
        {
            MatchMessageManager.Instance.SendStageSelect(transitionId, newStageIndex, stageRngState);
        }
    }

    public void ApplyOnlineStageSelection(int stageIndex, uint? syncedStageRngState = null)
    {
        ApplyOnlineStageSelectionState(stageIndex, syncedStageRngState);
        isTransitioning = true;
        localSceneTransitionReady = false;
        sceneManager.LoadScene("Gameplay");
    }

    private void ApplyOnlineStageSelectionState(int stageIndex, uint? syncedStageRngState = null)
    {
        if (syncedStageRngState.HasValue)
        {
            stageRngState = syncedStageRngState.Value;
        }

        if (playerInputManager != null)
        {
            playerInputManager.DisableJoining();
            playerInputManager.enabled = false;
        }

        if (gameStages.Count <= 0)
        {
            FillGameStages();
        }

        if (stageIndex >= 0 && stageIndex < stages.Count)
        {
            gameStages.Remove(stages[stageIndex]);
        }

        SetStage(stageIndex);
    }

    private bool TryApplyPendingGameplayStageSelectForLoadedGameplay()
    {
        if (!hasPendingStageSelect || pendingStageSelectSceneType != 1)
        {
            return false;
        }

        int expectedTransitionId = activeOnlineTransitionId > 0 ? activeOnlineTransitionId : GetExpectedOnlineTransitionId();
        if (pendingStageSelectTransitionId != expectedTransitionId
            || pendingStageSelectIndex < 0
            || pendingStageSelectIndex >= stages.Count)
        {
            return false;
        }

        int pendingTransitionId = pendingStageSelectTransitionId;
        int pendingIndex = pendingStageSelectIndex;
        uint pendingRngState = pendingStageSelectRngState;
        int pendingTotalRoundsPlayed = pendingStageSelectTotalRoundsPlayed;
        uint pendingGameplayRngState = pendingStageSelectGameplayRngState;
        int pendingRandomCallCount = pendingStageSelectRandomCallCount;

        ClearPendingStageSelect();

        if (activeOnlineTransitionId == 0)
        {
            BeginTrackedOnlineTransition(pendingTransitionId);
        }

        if (pendingTotalRoundsPlayed >= 0)
        {
            ApplyOnlineTotalRoundsPlayed(pendingTotalRoundsPlayed);
        }

        ApplyOnlineGameplayRngState(pendingGameplayRngState, pendingRandomCallCount);
        MarkGameplayStageTransitionApplied(pendingTransitionId);
        ApplyOnlineStageSelectionState(pendingIndex, pendingRngState);
        return true;
    }

    private void SelectFallbackOnlineGameplayStage()
    {
        int selectedStageIndex = SelectRandomGameplayStageIndex(useStageRng: true);
        if (selectedStageIndex < 0)
        {
            return;
        }

        SetStage(selectedStageIndex);

        if (IsOnlineHostAuthority() && MatchMessageManager.Instance != null)
        {
            int transitionId = activeOnlineTransitionId > 0 ? activeOnlineTransitionId : GetExpectedOnlineTransitionId();
            MarkGameplayStageTransitionApplied(transitionId);
            MatchMessageManager.Instance.SendStageSelect(transitionId, selectedStageIndex, stageRngState);
        }
    }

    private int SelectRandomGameplayStageIndex(bool useStageRng)
    {
        if (gameStages.Count <= 0)
        {
            FillGameStages();
        }

        if (gameStages.Count <= 0)
        {
            return stages.Count > 0 ? 0 : -1;
        }

        int gameStageIndex = useStageRng
            ? GetNextStageRandom(0, gameStages.Count)
            : GetNextRandom(0, gameStages.Count);
        StageDataSO selectedStage = gameStages[gameStageIndex];
        int selectedStageIndex = stages.FindIndex(stage => stage == selectedStage);
        gameStages.RemoveAt(gameStageIndex);
        return selectedStageIndex;
    }

    private void ClearPendingStageSelect()
    {
        hasPendingStageSelect = false;
        pendingStageSelectTransitionId = 0;
        pendingStageSelectSceneType = 0;
        pendingStageSelectSceneSignature = 0;
        pendingStageSelectIndex = -1;
        pendingStageSelectRngState = 0;
        pendingStageSelectTotalRoundsPlayed = -1;
        pendingStageSelectGameplayRngState = 0;
        pendingStageSelectRandomCallCount = -1;
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //Debug.Log($"Scene loaded: {scene.name}");

        // Must run before anything in the new scene can consume the gameplay RNG: the old scene's
        // sim is finished at this point, the new round's sim has not started. See the comment on
        // StashHostGameplayRngFromStageSelect.
        ApplyPendingHostGameplayRngRestoreIfAvailable();

        RefreshSceneObjectReferences();
        HitboxManager.Instance.GetActiveCamera();
        ProjectileManager.Instance.DeleteAllProjectiles();

        if (scene.name == "End")
        {
            endInputEnabled = false;

            // Clear stage geometry and the persistent HUD off the End screen in BOTH modes, so end screen shows only the winner
            ClearStages();
            HidePersistentUiForEndScene();

            if (isOnlineMatchActive)
            {
                isRunning = false;
                if (isTransitioning)
                {
                    if (MatchMessageManager.Instance != null)
                    {
                        MatchMessageManager.Instance.ResetFrameSyncForSceneTransition();
                    }

                    if (RollbackManager.Instance != null)
                    {
                        RollbackManager.Instance.ClearVars();
                    }

                    localSceneTransitionReady = true;
                    sceneReadyPeerSlots.Add(localPlayerIndex);
                    ApplyPendingSceneTransitionReadyIfAvailable();
                    if (MatchMessageManager.Instance != null)
                    {
                        MatchMessageManager.Instance.SendSceneTransitionReadySignal(activeOnlineTransitionId);
                    }
                    CheckSceneTransitionReady();
                }
            }
        }
        else
        {
            endInputEnabled = false;
            if (scene.name == "MainMenu")
            {
                endWinnerPid = -1;
                endWinnerPalette = null;
                bigWinner = null;
            }
        }

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

        ramNeededToWinRound = (ushort)( baseRamNeeddedtowin + 100 * roundsPlayed);

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
            FindAllFloppyDisks();
        }

        // For ONLINE gameplay
        if (isOnlineMatchActive && scene.name == "Gameplay" && isTransitioning)
        {
            //Debug.Log("Gameplay Scene Loaded - Resuming Online Match");
            bool appliedPendingGameplayStageSelect = TryApplyPendingGameplayStageSelectForLoadedGameplay();
            onlineRoundAdvanceApplied = false;
            roundOver = false;
            gameOver = false;
            roundEndFrameCounter = 0;
            roundEndTimer = 0f;
            roundTransitionPending = false;
            roundEndUIShown = false;
            lastRoundWinnerPID = -1;
            pendingOpponentShopTransition = false;
            pendingOpponentShopTransitionId = 0;
            localPlayerReadyForGameplay = false;
            remotePlayerReadyForGameplay = false;
            gameplayReadyPeerSlots.Clear();
            localGameplayReadyContext = GameplayReadyContext.None;
            remoteGameplayReadyContext = GameplayReadyContext.None;
            pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
            localGameplayReadyTransitionId = 0;
            remoteGameplayReadyTransitionId = 0;
            pendingRemoteGameplayReadyTransitionId = 0;
            if (!appliedPendingGameplayStageSelect)
            {
                ClearPendingStageSelect();
            }
            localSceneTransitionReady = false;
            frameNumber = 0;
            localPlayerInput = 5;
            syncedInput = new ulong[Mathf.Max(2, IsRosterBasedOnlineMatch() ? playerCount : 2)];
            timeoutFrames = 0;
            for (int i = 0; i < playerCount; i++)
            {
                if (players[i] != null)
                {
                    players[i].roundRam = 0;
                }
            }

            if (MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.ResetFrameSyncForSceneTransition();
            }

            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.ClearVars();
                RollbackManager.Instance.MarkAllRemoteSlotsPendingUntilInput();
            }

            if (currentStageIndex < 0)
            {
                SelectFallbackOnlineGameplayStage();
            }

            ResetPlayers();
            ProjectileManager.Instance.InitializeAllProjectiles();
            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.SaveState();
            }

            localSceneTransitionReady = true;
            sceneReadyPeerSlots.Add(localPlayerIndex);
            ApplyPendingSceneTransitionReadyIfAvailable();
            if (MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.SendSceneTransitionReadySignal(activeOnlineTransitionId);
            }
            CheckSceneTransitionReady();
        }

        // Handle shop scene loading for online
        if (isOnlineMatchActive && scene.name == "Shop" && isTransitioning)
        {
            //Debug.Log("Shop Scene Loaded - Resuming Online Match in Shop");
            SetStage(-1);
            roundOver = false;
            gameOver = false;
            roundEndFrameCounter = 0;
            roundEndTimer = 0f;
            roundTransitionPending = false;
            roundEndUIShown = false;
            lastRoundWinnerPID = -1;
            pendingOpponentShopTransition = false;
            pendingOpponentShopTransitionId = 0;
            localPlayerReadyForGameplay = false;
            remotePlayerReadyForGameplay = false;
            gameplayReadyPeerSlots.Clear();
            localGameplayReadyContext = GameplayReadyContext.None;
            remoteGameplayReadyContext = GameplayReadyContext.None;
            pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
            localGameplayReadyTransitionId = 0;
            remoteGameplayReadyTransitionId = 0;
            pendingRemoteGameplayReadyTransitionId = 0;
            hasPendingStageSelect = false;
            pendingStageSelectTransitionId = 0;
            pendingStageSelectSceneType = 0;
            pendingStageSelectSceneSignature = 0;
            pendingStageSelectIndex = -1;
            pendingStageSelectRngState = 0;
            pendingStageSelectTotalRoundsPlayed = -1;
            localSceneTransitionReady = false;
            frameNumber = 0;
            localPlayerInput = 5;
            syncedInput = new ulong[Mathf.Max(2, IsRosterBasedOnlineMatch() ? playerCount : 2)];
            timeoutFrames = 0;
            ResetOnlineShopChoiceFlags();

            if (MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.ResetFrameSyncForSceneTransition();
            }

            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.ClearVars();
                RollbackManager.Instance.MarkAllRemoteSlotsPendingUntilInput();
            }

            InitializeOnlineShopSceneState();
            ResetPlayers();
            ProjectileManager.Instance.InitializeAllProjectiles();
            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.SaveState();
            }
            localSceneTransitionReady = true;
            sceneReadyPeerSlots.Add(localPlayerIndex);
            ApplyPendingSceneTransitionReadyIfAvailable();
            if (MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.SendSceneTransitionReadySignal(activeOnlineTransitionId);
            }
            CheckSceneTransitionReady();
            // Ready flags are already reset in RoundEnd()
        }
            GameObject[] curtains = GameObject.FindGameObjectsWithTag("LoadCurtain");
            Debug.Log(curtains.Length);
            if(curtains.Length > 0)
            {
                curtains[0].SetActive(true);
            }
        sceneManager.RemoveScreenCover(()=>
        {
            BGM_Manager.Instance.StartAndPlaySong();
        });
    }

    private void ResetOnlineShopChoiceFlags()
    {
        if (!isOnlineMatchActive)
        {
            return;
        }

        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] != null)
            {
                players[i].chosenSpell = false;
            }
        }
    }

    public void ClearStages()
    {
        for (int i = 0; i < tempMapGOs.Count; i++)
        {
            tempMapGOs[i].SetActive(false);
        }
        lobbyMapGO.SetActive(false);
        tutorialMapGO.SetActive(false);
        trainingGroundsGO.SetActive(false);
        soloLobbyGO.SetActive(false);
    }

    private void HidePersistentUiForEndScene()
    {
        if (tempUI != null)
        {
            tempUI.gameObject.SetActive(false);
        }

        if (shopImage != null)
        {
            shopImage.enabled = false;
        }

        if (playerWinText != null)
        {
            playerWinText.enabled = false;
        }

        if (roundEndedText != null)
        {
            roundEndedText.enabled = false;
        }

        if (networkInfo != null)
        {
            networkInfo.SetActive(false);
        }
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
            bool ownerCanUseShop = hasActiveOwner
                && players[gamba.ownerPID - 1].spellList != null
                && players[gamba.ownerPID - 1].spellList.Count < 6
                && !players[gamba.ownerPID - 1].chosenSpell;

            gamba.ownerPlayer = hasActiveOwner ? players[gamba.ownerPID - 1] : null;
            gamba.activatedCount = ownerCanUseShop ? 0 : 3;
            gamba.isActive = ownerCanUseShop;
            gamba.ApplyVisualState();
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
            FirstTimeBootTutorial();
        }
    }

    public void FirstTimeBootTutorial()
    {
        if (SettingsManager.Instance.IsFirstLaunch())
        {
            SettingsManager.Instance.MarkFirstLaunchComplete();
            LoadTutorial();
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
                FloppyPickup disk = go != null ? go.GetComponent<FloppyPickup>() : null;
                return disk != null ? disk.ownerPID : int.MaxValue;
            })
            .ThenBy(go => go != null ? go.transform.position.x : float.MaxValue)
            .ThenBy(go => go != null ? go.transform.position.y : float.MaxValue)
            .ThenBy(go =>
            {
                FloppyPickup disk = go != null ? go.GetComponent<FloppyPickup>() : null;
                return disk != null ? disk.diskName : string.Empty;
            })
            .ToArray();
    }

    public GameObject[] FindFloppyDisksofPID(int ownerPID)
    {
        FindAllFloppyDisks();

        return (floppyObjects ?? Array.Empty<GameObject>())
            .Where(go =>
            {
                FloppyPickup disk = go != null ? go.GetComponent<FloppyPickup>() : null;
                return disk != null && disk.ownerPID == ownerPID;
            })
            .ToArray();
    }

    // ---------------------------------------------------------Central State Serialization Methods-----------------------------------------

    private struct SavedProjectileState
    {
        public int prefabIndex;
        public long dataStart;
        public int dataLength;

        public SavedProjectileState(int prefabIndex, long dataStart, int dataLength)
        {
            this.prefabIndex = prefabIndex;
            this.dataStart = dataStart;
            this.dataLength = dataLength;
        }
    }

    private readonly List<SavedProjectileState> savedProjectileStateBuffer = new List<SavedProjectileState>(32);
    private readonly HashSet<int> savedProjectileIndexSet = new HashSet<int>();

    private struct SavedFloppyState
    {
        public int ownerPid;
        public string diskName;
        public Vector2 position;
        public byte holdCounter;
        public bool showDescription;
        public bool restored;

        public SavedFloppyState(int ownerPid, string diskName, Vector2 position, byte holdCounter, bool showDescription)
        {
            this.ownerPid = ownerPid;
            this.diskName = diskName;
            this.position = position;
            this.holdCounter = holdCounter;
            this.showDescription = showDescription;
            this.restored = false;
        }
    }

    private readonly List<SavedFloppyState> savedFloppyStateBuffer = new List<SavedFloppyState>(12);
    private readonly List<string> savedP1ChoiceBuffer = new List<string>(3);
    private readonly List<string> savedP2ChoiceBuffer = new List<string>(3);
    private readonly List<string> savedP3ChoiceBuffer = new List<string>(3);
    private readonly List<string> savedP4ChoiceBuffer = new List<string>(3);

    /// <summary>
    /// Serializes the entire deterministic game state managed by GameManager.
    /// Includes players and active projectiles.
    /// </summary>
    /// <returns>A byte array representing the game state snapshot.</returns>
    public byte[] SerializeManagedState()
    {
        var __hitchSw = logSnapshotHitchTiming ? System.Diagnostics.Stopwatch.StartNew() : null;
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

                bool includeLobbyShopState = ShouldIncludeLobbyShopState();
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
                    bw.Write(p3_shopIndex);
                    bw.Write(p4_shopIndex);

                    bw.Write(p1_lastCycleFrame);
                    bw.Write(p2_lastCycleFrame);

                    // Serialize shop spell choices themselves
                    if (shopManager != null)
                    {
                        SerializeStringList(bw, shopManager.GetP1Choices());
                        SerializeStringList(bw, shopManager.GetP2Choices());
                        SerializeStringList(bw, shopManager.GetP3Choices());
                        SerializeStringList(bw, shopManager.GetP4Choices());
                    }
                    else
                    {
                        // No shop active, write empty lists
                        bw.Write(0); // p1_choices count
                        bw.Write(0); // p2_choices count
                        bw.Write(0); // p3_choices count
                        bw.Write(0); // p4_choices count
                    }

                    // Also serialize if players have chosen their shop spell
                    for (int i = 0; i < playerCount; i++)
                    {
                        bw.Write(players[i].chosenSpell);
                    }
                }

                SerializeActiveProjectileStates(bw);

                bw.Write(includeLobbyShopState);
                if (includeLobbyShopState)
                {
                    bw.Write(gates.Length);
                    foreach (var gate in gates)
                    {
                        bool hasGate = gate != null;
                        bw.Write(hasGate);
                        if (hasGate)
                        {
                            gate.Serialize(bw);
                        }
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
                        bool isActive = gamba != null && gamba.isActive;
                        bw.Write(isActive);
                    }

                    SerializeFloppyState(bw);
                }

                byte[] __serialized = memoryStream.ToArray();
                LogHitchTiming("SerializeManagedState", __hitchSw, playerCount);
                return __serialized;
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

            SerializeLobbyShopHashState(bw);

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

            SerializeLobbyShopHashState(bw);

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

    private void SerializeActiveProjectileStates(BinaryWriter bw)
    {
        List<BaseProjectile> masterList = ProjectileManager.Instance.projectilePrefabs;
        Stream stream = bw.BaseStream;
        long countPosition = stream.Position;
        bw.Write(0);
        int activeCount = 0;

        for (int prefabIndex = 0; prefabIndex < masterList.Count; prefabIndex++)
        {
            BaseProjectile projectile = masterList[prefabIndex];
            if (projectile == null || !projectile.gameObject.activeSelf)
            {
                continue;
            }

            activeCount++;
            bw.Write(prefabIndex);
            WriteLengthPrefixedProjectileState(bw, projectile);
        }

        long endPosition = stream.Position;
        stream.Position = countPosition;
        bw.Write(activeCount);
        stream.Position = endPosition;
    }

    private static void WriteLengthPrefixedProjectileState(BinaryWriter bw, BaseProjectile projectile)
    {
        Stream stream = bw.BaseStream;
        long lengthPosition = stream.Position;
        bw.Write(0);
        long dataStart = stream.Position;

        projectile.Serialize(bw);

        long dataEnd = stream.Position;
        int dataLength = checked((int)(dataEnd - dataStart));
        stream.Position = lengthPosition;
        bw.Write(dataLength);
        stream.Position = dataEnd;
    }

    private void DeserializeActiveProjectileStates(BinaryReader br)
    {
        int savedProjectileCount = br.ReadInt32();
        List<BaseProjectile> masterList = ProjectileManager.Instance.projectilePrefabs;
        savedProjectileStateBuffer.Clear();
        savedProjectileIndexSet.Clear();

        for (int i = 0; i < savedProjectileCount; i++)
        {
            int prefabIndex = br.ReadInt32();
            int dataLength = br.ReadInt32();
            long dataStart = br.BaseStream.Position;
            long dataEnd = dataStart + dataLength;

            if (prefabIndex >= 0 && prefabIndex < masterList.Count && masterList[prefabIndex] != null)
            {
                savedProjectileStateBuffer.Add(new SavedProjectileState(prefabIndex, dataStart, dataLength));
                savedProjectileIndexSet.Add(prefabIndex);
            }

            br.BaseStream.Position = dataEnd;
        }

        long projectilePayloadEnd = br.BaseStream.Position;

        for (int prefabIndex = 0; prefabIndex < masterList.Count; prefabIndex++)
        {
            BaseProjectile projectile = masterList[prefabIndex];
            if (projectile == null || !projectile.gameObject.activeSelf || savedProjectileIndexSet.Contains(prefabIndex))
            {
                continue;
            }

            ProjectileManager.Instance.DeleteProjectile(projectile);
        }

        for (int i = 0; i < savedProjectileStateBuffer.Count; i++)
        {
            SavedProjectileState savedProjectile = savedProjectileStateBuffer[i];
            BaseProjectile projectile = masterList[savedProjectile.prefabIndex];
            if (!projectile.gameObject.activeSelf)
            {
                projectile.ResetValues();
                projectile.gameObject.SetActive(true);
            }

            br.BaseStream.Position = savedProjectile.dataStart;
            projectile.Deserialize(br);
            br.BaseStream.Position = savedProjectile.dataStart + savedProjectile.dataLength;
        }

        br.BaseStream.Position = projectilePayloadEnd;
        ProjectileManager.Instance.SynchronizeActiveProjectiles();
    }

    private bool ShouldIncludeLobbyShopState()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.name != "Gameplay";
    }

    private void SerializeLobbyShopHashState(BinaryWriter bw)
    {
        bool includeLobbyShopState = ShouldIncludeLobbyShopState();
        bw.Write(includeLobbyShopState);
        if (!includeLobbyShopState)
        {
            return;
        }

        bw.Write(gates.Length);
        foreach (SpellCode_Gate gate in gates)
        {
            bw.Write(gate != null);
            if (gate != null)
            {
                bw.Write(gate.isOpen);
            }
        }

        List<GameObject> validGambas = GetValidGambaObjects(refreshIfNeeded: true);
        bw.Write(validGambas.Count);
        foreach (GameObject gambaGO in validGambas)
        {
            GambaMachine gamba = gambaGO != null ? gambaGO.GetComponent<GambaMachine>() : null;
            bw.Write(gamba != null ? gamba.activatedCount : 0);
            bw.Write(gamba != null ? gamba.resetTimer : (byte)0);
            bw.Write(gamba != null ? gamba.GetStartingSpellPos() : 0);
            bool isActive = gamba != null && gamba.isActive;
            bw.Write(isActive);
        }

        SerializeFloppyState(bw);
    }

    // Online-only: set true while DeserializeManagedState is running. PlayerController's
    // RebuildSpellListFromSaved consults this so the (expensive) projectile-pool rebuild
    // can be batched to a single call at the end of the deserialize pass instead of firing
    // once per mismatching player. Offline path is untouched.
    [System.NonSerialized]
    public bool isApplyingManagedStateDeserialize = false;
    private bool _pendingProjectilePoolRebuild = false;

    // TEMP diagnostic for the pre-snapshot lobby hitch. Times the snapshot-path operations and logs
    // [HitchDiag] only when one exceeds the threshold, so it stays quiet unless there's a real spike.
    // Once the dominant cost is identified and the real fix lands, set this false / remove it.
    [SerializeField] public bool logSnapshotHitchTiming = true;
    private const double SnapshotHitchLogThresholdMs = 0.5;

    public void LogHitchTiming(string label, System.Diagnostics.Stopwatch stopwatch, int detail = -1)
    {
        if (stopwatch == null) return;
        stopwatch.Stop();
        double ms = stopwatch.Elapsed.TotalMilliseconds;
        if (ms < SnapshotHitchLogThresholdMs) return;
        if (detail >= 0)
        {
            Debug.Log($"[HitchDiag] {label} took {ms:F2} ms (n={detail})");
        }
        else
        {
            Debug.Log($"[HitchDiag] {label} took {ms:F2} ms");
        }
    }

    /// <summary>
    /// Online-only: called by PlayerController.RebuildSpellListFromSaved during a
    /// snapshot/rollback apply. While the deserialize pass is in progress, the rebuild is
    /// deferred to a single call at the end. Outside of deserialize, rebuilds immediately
    /// so direct callers see the legacy behavior.
    /// </summary>
    public void RequestProjectilePoolRebuild()
    {
        if (isApplyingManagedStateDeserialize)
        {
            _pendingProjectilePoolRebuild = true;
            return;
        }

        if (ProjectileManager.Instance != null)
        {
            ProjectileManager.Instance.InitializeAllProjectiles();
        }
    }

    /// <summary>
    /// Deserializes and applies a game state snapshot.
    /// Restores players and manages projectile activation/state.
    /// </summary>
    /// <param name="stateData">The byte array snapshot to load.</param>
    public void DeserializeManagedState(byte[] stateData)
    {
        // Online-only: batch any projectile-pool rebuilds requested by per-player
        // RebuildSpellListFromSaved calls. See RequestProjectilePoolRebuild above.
        isApplyingManagedStateDeserialize = true;
        _pendingProjectilePoolRebuild = false;
        var __hitchSw = logSnapshotHitchTiming ? System.Diagnostics.Stopwatch.StartNew() : null;
        try
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

                int playersToRead = Mathf.Clamp(savedPlayerCount, 0, players.Length);
                for (int i = 0; i < playersToRead; i++)
                {
                    if (players[i] == null && i < playerCount && playerPrefab != null)
                    {
                        CreateOnlinePlayerForSlot(i, i == localPlayerIndex);
                    }

                    if (players[i] != null)
                    {
                        players[i].Deserialize(br);
                    }
                    else
                    {
                        throw new InvalidDataException($"Cannot deserialize saved player slot {i}; no player object is available.");
                    }
                }
                ApplyDisconnectedPlayerSlots(cleanupProjectiles: false);

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
                    p3_shopIndex = br.ReadInt32();
                    p4_shopIndex = br.ReadInt32();
                    p1_lastCycleFrame = br.ReadInt32();
                    p2_lastCycleFrame = br.ReadInt32();

                    // Deserialize shop spell choices
                    DeserializeStringListInto(br, savedP1ChoiceBuffer);
                    DeserializeStringListInto(br, savedP2ChoiceBuffer);
                    DeserializeStringListInto(br, savedP3ChoiceBuffer);
                    DeserializeStringListInto(br, savedP4ChoiceBuffer);
                    if (shopManager != null)
                    {
                        shopManager.SetChoicesForPlayer(0, savedP1ChoiceBuffer);
                        shopManager.SetChoicesForPlayer(1, savedP2ChoiceBuffer);
                        shopManager.SetChoicesForPlayer(2, savedP3ChoiceBuffer);
                        shopManager.SetChoicesForPlayer(3, savedP4ChoiceBuffer);
                    }

                    for (int i = 0; i < playersToRead; i++)
                    {
                        bool chosenSpell = br.ReadBoolean();
                        if (i < playerCount && players[i] != null)
                        {
                            players[i].chosenSpell = chosenSpell;
                        }
                    }
                }

                // Online-only: any per-player RebuildSpellListFromSaved calls during the
                // player loop above requested a deferred pool rebuild. Do it ONCE here, now
                // that every player's spell list is finalised, so the projectile prefab
                // ordering matches the host's and the prefabIndex values we're about to
                // read from the stream resolve correctly.
                if (_pendingProjectilePoolRebuild)
                {
                    _pendingProjectilePoolRebuild = false;
                    if (ProjectileManager.Instance != null)
                    {
                        ProjectileManager.Instance.InitializeAllProjectiles();
                    }
                }

                DeserializeActiveProjectileStates(br);
                ApplyDisconnectedPlayerSlots(cleanupProjectiles: true);

                bool hasLobbyShopTail = br.ReadBoolean();
                if (hasLobbyShopTail)
                {
                    int gateCount = br.ReadInt32();
                    for (int i = 0; i < gateCount; i++)
                    {
                        bool hasGate = br.ReadBoolean();
                        if (!hasGate)
                        {
                            continue;
                        }

                        bool isOpen = br.ReadBoolean();
                        if (i < gates.Length && gates[i] != null)
                        {
                            gates[i].SetOpen(isOpen);
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
                                gamba.isActive = isActive;
                                gamba.ApplyVisualState();
                            }
                        }
                    }

                    DeserializeFloppyState(br);
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
        finally
        {
            isApplyingManagedStateDeserialize = false;
            _pendingProjectilePoolRebuild = false;
            LogHitchTiming("DeserializeManagedState", __hitchSw, playerCount);
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

    private void DeserializeStringListInto(BinaryReader br, List<string> list)
    {
        list.Clear();
        int count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            list.Add(br.ReadString());
        }
    }

    /// <summary>
    /// Build the available stage pool for this match based on player count.
    /// </summary>
    private void FillGameStages()
    {
        //first, fill gameStages with all possible stages,...
        gameStages = new List<StageDataSO>(stages);

        //then, based on playerCount, remove all irrelevant stages from gameStages 
        switch (playerCount)
        {
            case 2:
                gameStages.RemoveAll(stage => stage != null && stage.stageType != StageType.Duel);
                break;
            case 3:
                gameStages.RemoveAll(stage => stage != null && stage.stageType != StageType.General);
                break;
            case 4:
                gameStages.RemoveAll(stage => stage != null && stage.stageType == StageType.Duel);
                break;
        }
    }

    private bool IsRosterBasedOnlineMatch()
    {
        return activeOnlineRoster != null;
    }

    private bool DoesActiveOnlineRosterMatch(OnlineMatchRoster roster)
    {
        if (activeOnlineRoster == null || roster == null || activeOnlineRoster.PlayerCount != roster.PlayerCount)
        {
            return false;
        }

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer == null || !activeOnlineRoster.TryGetSteamIdForSlot(peer.PlayerSlot, out Steamworks.SteamId activeSteamId) || activeSteamId != peer.SteamId)
            {
                return false;
            }
        }

        return activeOnlineRoster.LocalPlayerSlot == roster.LocalPlayerSlot;
    }

    private void ApplyOnlineRoster(OnlineMatchRoster roster)
    {
        ResetOnlineRosterState();
        activeOnlineRoster = roster;
        localPlayerIndex = roster.LocalPlayerSlot;
        remotePlayerIndex = -1;

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer == null)
            {
                continue;
            }

            onlineSlotToPeer[peer.PlayerSlot] = peer.SteamId;
            onlinePeerToSlot[peer.SteamId] = peer.PlayerSlot;
            if (peer.PlayerSlot != localPlayerIndex && remotePlayerIndex < 0)
            {
                remotePlayerIndex = peer.PlayerSlot;
            }
        }
    }

    private void CreateOnlinePlayerForSlot(int slot, bool isLocal)
    {
        if (slot < 0 || slot >= players.Length || playerPrefab == null)
        {
            return;
        }

        GameObject p = InstantiateOnlinePlayerObject();
        players[slot] = p.GetComponent<PlayerController>();
        AnimationManager.Instance.InitializePlayerVisuals(players[slot], slot);

        if (players[slot].playerNum != null)
        {
            players[slot].playerNum.text = "P" + (slot + 1);
        }

        PlayerInput pInput = p.GetComponent<PlayerInput>();
        if (isLocal)
        {
            players[slot].inputs.AssignInputDevice(null);
            ConfigureOnlineLocalPlayerInput(pInput, players[slot].inputs);
            SettingsManager.Instance?.TryApplyControlOptionsForPlayer(players[slot]);
            players[slot].CheckForInputs(true, false);
        }
        else
        {
            MarkOnlineRemotePlayerInputInactive(players[slot]);
        }

        players[slot].InitCharacter();
    }

    private GameObject InstantiateOnlinePlayerObject()
    {
        GameObject playerObject = Instantiate(playerPrefab);
        DontDestroyOnLoad(playerObject);
        return playerObject;
    }

    private void ResetOnlineReadyForGameplayState()
    {
        localPlayerReadyForGameplay = false;
        remotePlayerReadyForGameplay = false;
        gameplayReadyPeerSlots.Clear();
        localGameplayReadyContext = GameplayReadyContext.None;
        remoteGameplayReadyContext = GameplayReadyContext.None;
        pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
        localGameplayReadyTransitionId = 0;
        remoteGameplayReadyTransitionId = 0;
        pendingRemoteGameplayReadyTransitionId = 0;
        pendingGameplayReadyBySlot.Clear();
        pendingGameplayReadyTransitionBySlot.Clear();
    }

    private void PruneOnlineReadyForGameplayState(OnlineMatchRoster roster)
    {
        if (roster?.Peers == null)
        {
            ResetOnlineReadyForGameplayState();
            return;
        }

        HashSet<int> validSlots = new HashSet<int>();
        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer != null && IsPlayerSlotConnected(peer.PlayerSlot))
            {
                validSlots.Add(peer.PlayerSlot);
            }
        }

        List<int> readySlotsToRemove = new List<int>();
        foreach (int slot in gameplayReadyPeerSlots)
        {
            if (!validSlots.Contains(slot))
            {
                readySlotsToRemove.Add(slot);
            }
        }

        for (int i = 0; i < readySlotsToRemove.Count; i++)
        {
            gameplayReadyPeerSlots.Remove(readySlotsToRemove[i]);
        }

        List<int> pendingSlotsToRemove = new List<int>();
        foreach (int slot in pendingGameplayReadyBySlot.Keys)
        {
            if (!validSlots.Contains(slot))
            {
                pendingSlotsToRemove.Add(slot);
            }
        }

        for (int i = 0; i < pendingSlotsToRemove.Count; i++)
        {
            pendingGameplayReadyBySlot.Remove(pendingSlotsToRemove[i]);
            pendingGameplayReadyTransitionBySlot.Remove(pendingSlotsToRemove[i]);
        }

        if (!validSlots.Contains(localPlayerIndex))
        {
            localPlayerReadyForGameplay = false;
            localGameplayReadyContext = GameplayReadyContext.None;
            localGameplayReadyTransitionId = 0;
        }

        if (remotePlayerIndex < 0 || !validSlots.Contains(remotePlayerIndex))
        {
            remotePlayerReadyForGameplay = false;
            remoteGameplayReadyContext = GameplayReadyContext.None;
            remoteGameplayReadyTransitionId = 0;
            pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
            pendingRemoteGameplayReadyTransitionId = 0;
        }
    }

    public bool IsOnlineLobbyAcceptingAdditionalPlayers()
    {
        if (!isOnlineMatchActive)
        {
            return true;
        }

        if (SceneManager.GetActiveScene().name != "MainMenu" || isTransitioning)
        {
            return false;
        }

        return playerCount < players.Length;
    }

    public bool CanStartOrRefreshOnlineLobby(OnlineMatchRoster roster)
    {
        if (roster == null || roster.PlayerCount < 2 || roster.PlayerCount > players.Length)
        {
            return false;
        }

        if (!isOnlineMatchActive)
        {
            return true;
        }

        if (!IsOnlineLobbyAcceptingAdditionalPlayers())
        {
            return false;
        }

        int currentRosterCount = activeOnlineRoster != null ? activeOnlineRoster.PlayerCount : playerCount;
        return roster.PlayerCount > currentRosterCount;
    }

    public bool IsOnlineHostAuthority()
    {
        if (activeOnlineRoster != null)
        {
            return activeOnlineRoster.HostSteamId == Steamworks.SteamClient.SteamId;
        }

        return localPlayerIndex == 0;
    }

    public bool IsOnlineHostSlot(int playerSlot)
    {
        if (activeOnlineRoster == null)
        {
            return playerSlot == remotePlayerIndex || playerSlot == 0;
        }

        return activeOnlineRoster.TryGetSteamIdForSlot(playerSlot, out Steamworks.SteamId slotSteamId)
            && activeOnlineRoster.HostSteamId.IsValid
            && slotSteamId.IsValid
            && slotSteamId.Value == activeOnlineRoster.HostSteamId.Value;
    }

    private int GetExpectedRemotePeerCount()
    {
        if (!IsRosterBasedOnlineMatch())
        {
            return isOnlineMatchActive ? 1 : 0;
        }

        int count = 0;
        for (int i = 0; i < activeOnlineRoster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = activeOnlineRoster.Peers[i];
            if (peer != null && peer.PlayerSlot != localPlayerIndex && IsPlayerSlotConnected(peer.PlayerSlot))
            {
                count++;
            }
        }

        return count;
    }

    public int ResolvePlayerSlotForSteamId(Steamworks.SteamId steamId)
    {
        return onlinePeerToSlot.TryGetValue(steamId, out int slot) ? slot : -1;
    }

    private void ResetOnlineRosterState()
    {
        activeOnlineRoster = null;
        onlineSlotToPeer.Clear();
        onlinePeerToSlot.Clear();
        readyPeerSlots.Clear();
        gameplayReadyPeerSlots.Clear();
        sceneReadyPeerSlots.Clear();
        pendingGameplayReadyBySlot.Clear();
        pendingGameplayReadyTransitionBySlot.Clear();
        pendingSceneReadyBySlot.Clear();
    }

    /// <summary>
    /// Allocate space for and randomize the array of stages that a game can choose from. No duplicate stages are allowed in this array
    /// </summary>
    private void RandomizeGameStages()
    {
        FillGameStages();

        //Debug.Log("Before culling: gameStages.Count = " + gameStages.Count);

        //delete random stages from gameStages until gameStages.Length equals 9
        while (gameStages.Count > 9)
        {
            gameStages.RemoveAt(GetNextStageRandom(0, gameStages.Count));
        }

        //Debug.Log("After culling: gameStages.Count = " + gameStages.Count);
    }

    /// <summary>
    /// Get the stage index of a random, non looping stage within gameStages
    /// </summary>
    /// <returns>The stage index as an int</returns>
    private int GetStageIndexWithoutLooping()
    {
        //integer to make sure while loop does not go forever
        int _loopCheck = 0;

        //temp integer to store and return the stage index
        int _gameStageIndex;

        //get a new random stage until the found stage is NOT looping
        do
        {
            //find a new random stage index
            _gameStageIndex = GetNextStageRandom(0, gameStages.Count);

            //increment _loopCheck
            _loopCheck++;
        }
        while (gameStages[_gameStageIndex].borderType == BorderType.Loop && _loopCheck < 100);

        //return _gameStageIndex
        return _gameStageIndex;
    }
}
