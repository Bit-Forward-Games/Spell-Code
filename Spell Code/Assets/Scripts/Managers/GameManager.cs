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
        { "black", HexToColor("#000000") },
        { "evil color", HexToColor("#140e1e") }
    };

    public enum Gamemode
    {
        Normal,
        Turbo,
        Elimination,
        Fighter,
        Chaos
    }

    public Gamemode gamemode;

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


    [NonSerialized]
    public PlayerController bigWinner = null;
    public bool endInputEnabled = false;
    [NonSerialized]
    public int endWinnerPid = -1;
    [NonSerialized]
    public Texture2D endWinnerPalette = null;
    public int OnlineEndOptionsEpoch { get; private set; }
    private int preparedOnlineRematchEpoch = -1;
    private bool rematchPreparationStarted;
    private readonly Dictionary<Renderer, bool> endScreenRendererVisibility = new Dictionary<Renderer, bool>();

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
    private bool pendingOnlineFloppySnapshot;
    private string pendingOnlineFloppySnapshotReason;

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
    private readonly Dictionary<int, float> completedSceneReadyResponseTimeBySlot = new Dictionary<int, float>();
    private readonly Dictionary<int, int> pendingPeerDropFrames = new Dictionary<int, int>();
    private readonly Dictionary<int, HashSet<int>> peerDropAcknowledgedSlots = new Dictionary<int, HashSet<int>>();

    // StartOnlineMatch assigns the roster before creating/initializing its player objects, then
    // raises isOnlineMatchActive after that bootstrap is complete. UI code uses this narrow state
    // to avoid treating those online players as offline during InitCharacter -> SpawnPlayer.
    public bool IsOnlineMatchInitializing =>
        activeOnlineRoster != null && !isOnlineMatchActive;

    // True for the whole online-entry handshake: from the moment a lobby invite/host/Quick Match
    // starts connecting, through the roster bootstrap, until the match's simulation actually runs.
    // The lobby presentation (announcer banner + code-mode prompts) is suppressed for this entire
    // window so only the "JOINING/STARTING MATCH..." label is on screen; everything else appears
    // together on the frame the match goes live. isWaitingForOpponent is the tail of it -- players
    // exist and isOnlineMatchActive is already set there, but FixedUpdate still skips the sim.
    public bool IsOnlineEntryPending
    {
        get
        {
            if (isOnlineMatchActive)
            {
                return isWaitingForOpponent;
            }

            if (IsOnlineMatchInitializing)
            {
                return true;
            }

            SteamLobbyManager lobbyManager = SteamLobbyManager.Instance;
            return lobbyManager != null
                && (lobbyManager.IsJoiningMatch
                    || lobbyManager.IsStartingMatch
                    || lobbyManager.IsPartyEntryPending);
        }
    }

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
    private int requestedOnlineEndLoadTransitionId = 0;
    private int lastAppliedGameplayStageTransitionId = 0;
    private int lastAppliedRematchLobbyTransitionId = 0;
    private int lastAppliedRematchLobbySeed = 0;
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

    public void SetResolution()
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
        // The hub scenes keep their pfb_GameManager instance INACTIVE (only the boot scene's is
        // active) and normally rely on the persistent GameManager arriving with the player — which
        // this method just destroyed. On this cold load the scene's dormant copy must be woken or
        // GameManager.Instance stays null forever (black screen, camera NRE spam, and the deferred
        // online host/join resumes never fire). Static handler: survives this object's destruction.
        SceneManager.sceneLoaded -= ActivateDormantGameManagerAfterOrder66;
        SceneManager.sceneLoaded += ActivateDormantGameManagerAfterOrder66;
        SceneManager.LoadScene(scene);
        //Camera.main.GetComponentInChildren<Image>().enabled = false;
    }

    private static void ActivateDormantGameManagerAfterOrder66(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= ActivateDormantGameManagerAfterOrder66;

        // If the scene's own copy was active (e.g. SoloLobby, the boot scene), its Awake already
        // claimed the singleton and there is nothing to wake.
        if (Instance != null)
        {
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!root.activeSelf && root.GetComponentInChildren<GameManager>(true) != null)
            {
                Debug.Log($"[GameManager] Cold load of '{scene.name}': activating the scene's dormant GameManager.");
                root.SetActive(true);

                // The GameManager only subscribed OnSceneLoaded (via OnEnable) just now — AFTER
                // this scene's sceneLoaded event dispatched — so its per-scene-arrival work never
                // ran for THIS load: scene references, stage/curtain setup, and critically
                // RemoveScreenCover (without it the transition cover sits over the screen while
                // the scene runs underneath). Invoke it manually; this matches the pre-merge
                // ordering where an active scene copy subscribed during the load and received the
                // event before Start.
                if (Instance != null)
                {
                    Instance.OnSceneLoaded(scene, mode);
                }
                return;
            }
        }

        Debug.LogError($"[GameManager] Cold load of '{scene.name}': no GameManager found in the scene (active or dormant). The scene cannot run.");
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
        onboardManager = GetComponent<OnboardManager>();

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

        // Must run from Update, FixedUpdate is skipped entirely while isTransitioning, which is
        // exactly the state this watchdog un-sticks.
        UpdateOnlineTransitionWatchdog();

        // A controller connected mid-match only becomes usable once it is paired to the local
        // player's InputUser. Runs from Update so a pause (timeScale 0) still picks it up.
        if (onlineInputDevicesDirty)
        {
            onlineInputDevicesDirty = false;
            if (isOnlineMatchActive)
            {
                EnsureOnlineLocalPlayerInputActive();
            }
        }

        // Don't touch PlayerInputManager during online matches
        if (!isOnlineMatchActive)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            // Modal menus freeze the sim (timeScale = 0 stops FixedUpdate), but PlayerInputManager
            // listens for join presses on unscaled input from Update — without this gate a new
            // player could join the lobby while someone has the game paused.
            gameObject.GetComponent<PlayerInputManager>().enabled =
                (activeScene.name == "MainMenu" || activeScene.name == "SoloLobby") && !IsModalMenuOpen();
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

        if (SteamManager.DebugToolsEnabled)
        {
            //if = is pressed, player 1 win
            if (UnityEngine.Input.GetKeyDown(KeyCode.Equals))
            {
                players[0].roundRam = 600;
            }

            PrivateBetaDebugHotkeys();
        }
    }

    //int because OnClick() doesn't accept enums as parameters
    public void SetGamemode(int mode)
    {
        gamemode = (Gamemode)mode;
        Debug.Log("Gamemode set to: " + gamemode);

        switch (gamemode)
        {
            case Gamemode.Normal: //0
                loadMainMenu();
                break;

            case Gamemode.Turbo: //1
                loadMainMenu();
                break;

            case Gamemode.Elimination: //2
                loadMainMenu();
                break;

            case Gamemode.Fighter: //3
                loadMainMenu();
                break;

            case Gamemode.Chaos: //4
                loadMainMenu();
                break;
        }
    }

    /// <summary>
    /// True while a modal menu has the offline game frozen (pause menu, gamemode selectors,
    /// tutorial/code-mode prompts). Player joining must be blocked while one is up.
    /// </summary>
    private bool IsModalMenuOpen()
    {
        if (tempUI == null)
        {
            return false;
        }

        if (tempUI.pause != null && tempUI.pause.paused)
        {
            return true;
        }

        // The online menus (Online Play / VS Friends lobby / VS the World) are modal for the same
        // reason: a second player must not press start and join the local roster while someone is
        // arranging an online match that is about to replace it.
        if (OnlineMenuPanel.OpenPanelCount > 0)
        {
            return true;
        }

        //|| tempUI.codeModePromptMenuOpened[localPlayerIndex]
        return tempUI.soloGamemodesMenuOpened
            || tempUI.multiplayerGamemodesMenuOpened
            || tempUI.tutorialPromptMenuOpened
            ;
    }

    private void PrivateBetaDebugHotkeys()
    {
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

        // Shift + \ wipes this account's Steam achievements so the unlock paths can be tested
        // again. A combo rather than a single key like the rest of these: it's the only one
        // here that destroys progress, and \ is otherwise unused.
        if (UnityEngine.Input.GetKey(KeyCode.LeftShift)
            && UnityEngine.Input.GetKeyDown(KeyCode.Backslash))
        {
            SteamAchievements.ResetAllForTesting();
        }
    }
    public void loadMainMenu()
    {
        sceneManager.LoadScene("MainMenu");
        SetStage(-1);
        //ResetPlayers();
        players[0].ClearSpellList();
    }

    public void LoadTutorial()
    {
        
        sceneManager.LoadScene("Tutorial");
        SetStage(-2);
        //ResetPlayers();
        players[0].ClearSpellList();
    }

    public void loadTrainingGrounds()
    {
        sceneManager.LoadScene("TrainingGrounds");
        SetStage(-3);
        //ResetPlayers();
        players[0].ClearSpellList();
    }

    public void loadSoloLobby()
    {
        // Warm transitions (kicks/debug paths) bypass SceneUiManager.SoloLobby, so they need the same
        // unconditional online-entry cancellation as the normal cold return path.
        SteamLobbyManager.CancelOnlineEntryAndLeaveLobby();
        sceneManager.LoadScene("SoloLobby");
        SetStage(-4);
        //ResetPlayers();
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
            SetLocalOnlineInputCaptureSuppressed(true);
            LogSimSkip("isTransitioning");
            return;
        }
        Scene activeScene = SceneManager.GetActiveScene();

        // ONLINE LOBBY WAIT STATE
        if (isOnlineMatchActive && isWaitingForOpponent)
        {
            SetLocalOnlineInputCaptureSuppressed(true);
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
            SetLocalOnlineInputCaptureSuppressed(true);
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

    private ulong GatherInputForOnline(out InputPlayerBindings pendingInputCapture)
    {
        pendingInputCapture = null;
        PlayerController localPlayer = localPlayerIndex >= 0 && localPlayerIndex < players.Length
            ? players[localPlayerIndex]
            : null;

        if (StressTestController.Instance != null && StressTestController.Instance.UseDeterministicInput)
        {
            localPlayer?.inputs.SetOnlineInputCaptureSuppressed(true);
            ulong stressInput = StressTestController.Instance.GetDeterministicInput(frameNumber);
            return PlayerController.PackOnlineControlOptions(stressInput, localPlayer);
        }

        if (localPlayer != null && localPlayer.inputs.IsActive)
        {
            if (localPlayer.IsLocalOnlinePauseMenuOpen())
            {
                localPlayer.inputs.SetOnlineInputCaptureSuppressed(true);
                // Only NEW frames (current + InputDelay onward) go neutral while paused. The
                // already-buffered frames were sent to peers and must play out unchanged —
                // rewriting them (the old NeutralizePendingLocalInputs) desyncs at high ping
                // because peers have already verified those frames and drop the correction.
                return PlayerController.PackOnlineControlOptions(5UL, localPlayer);
            }

            localPlayer.inputs.SetOnlineInputCaptureSuppressed(false);
            ulong input = (ulong)localPlayer.inputs.PeekOnlineInputs();
            pendingInputCapture = localPlayer.inputs;
            return PlayerController.PackOnlineControlOptions(input, localPlayer);
        }
        return PlayerController.PackOnlineControlOptions(5UL, localPlayer); // neutral
        //return GatherRawInput(); // fallback to raw input gathering if player controller or inputs are not available
    }

    public void ResetLocalOnlineInputCaptureForNewTimeline()
    {
        if (players == null
            || localPlayerIndex < 0
            || localPlayerIndex >= players.Length
            || players[localPlayerIndex] == null)
        {
            return;
        }

        players[localPlayerIndex].inputs.ResetOnlineInputCapture();
    }

    public void SetLocalOnlineInputCaptureSuppressed(bool suppressed)
    {
        if (players == null
            || localPlayerIndex < 0
            || localPlayerIndex >= players.Length
            || players[localPlayerIndex] == null)
        {
            return;
        }

        players[localPlayerIndex].inputs.SetOnlineInputCaptureSuppressed(suppressed);
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
        PlayerInput playerInput = player != null ? player.GetComponent<PlayerInput>() : null;
        if (playerInput != null)
        {
            playerInput.DeactivateInput();
            if (playerInput.user.valid)
            {
                playerInput.user.UnpairDevices();
            }
        }

        player?.inputs?.SetActiveWithoutChangingActions(false);
    }

    private PlayerController GetPreOnlineLocalControlPlayer()
    {
        if (players == null)
        {
            return null;
        }

        if ((isOnlineMatchActive || IsOnlineMatchInitializing)
            && localPlayerIndex >= 0
            && localPlayerIndex < players.Length
            && players[localPlayerIndex] != null)
        {
            return players[localPlayerIndex];
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].inputs != null && players[i].inputs.IsActive)
            {
                return players[i];
            }
        }

        if (localPlayerIndex >= 0
            && localPlayerIndex < players.Length
            && players[localPlayerIndex] != null)
        {
            return players[localPlayerIndex];
        }

        return players.FirstOrDefault(player => player != null);
    }

    // Set by OnInputDeviceChanged, consumed in Update.
    private bool onlineInputDevicesDirty;

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

        // Re-pairing and binding refreshes can synchronously fire canceled/started callbacks.
        // Baseline around the operation so hot-plugging cannot create a phantom gameplay edge.
        localPlayer.inputs.SetOnlineInputCaptureSuppressed(true);
        PlayerInput playerInput = localPlayer.GetComponent<PlayerInput>();
        localPlayer.inputs.AssignInputDevice(null);
        ConfigureOnlineLocalPlayerInput(playerInput, localPlayer.inputs);
        SettingsManager.Instance?.TryApplyControlOptionsForPlayer(localPlayer);
        localPlayer.CheckForInputs(true, false);
        bool keepSuppressed = isTransitioning
            || isWaitingForOpponent
            || !IsOnlineSimulationScene(SceneManager.GetActiveScene())
            || localPlayer.IsLocalOnlinePauseMenuOpen();
        localPlayer.inputs.SetOnlineInputCaptureSuppressed(keepSuppressed);
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

    /// <summary>
    /// Game mode the current online match is being played under. Set from the host's pick in the 
    /// VS Friends lobby, the default for everything else.
    /// </summary>
    public OnlineGameModeSelection ActiveOnlineGameMode { get; private set; } = OnlineGameModeSelection.Default;

    /// <summary>
    /// Adopts the game mode the lobby published. Called on EVERY peer with the SAME id, read out of
    /// the same Steam lobby data, immediately before the match starts (and again for a drop-in
    /// joiner) that is what keeps the mode from being a source of desync. An unknown or empty id
    /// resolves to the default, so a peer on an older build lands somewhere every peer can name.
    /// </summary>
    public void ApplyOnlineGameMode(string gameModeId, string gameModeDisplayName = null)
    {
        OnlineGameModeSelection mode = OnlineGameModeSelection.Resolve(gameModeId, gameModeDisplayName);
        Gamemode resolvedGamemode = ResolveOnlineGamemode(mode.Id);
        bool modeChanged = ActiveOnlineGameMode.Id != mode.Id
            || ActiveOnlineGameMode.DisplayName != mode.DisplayName
            || gamemode != resolvedGamemode;

        // Always reassert both values. GameManager survives scene changes and the offline chooser can
        // change gamemode independently, so returning early for repeated lobby metadata could leave
        // this peer running stale local rules.
        ActiveOnlineGameMode = mode;
        gamemode = resolvedGamemode;

        if (modeChanged)
        {
            Debug.Log($"[GameManager] Online game mode set to '{mode.Id}' ({mode.DisplayName}) -> Gamemode.{gamemode}.");
        }
    }

    private static Gamemode ResolveOnlineGamemode(string gameModeId)
    {
        if (string.Equals(gameModeId, "turbo", StringComparison.OrdinalIgnoreCase))
        {
            return Gamemode.Turbo;
        }

        if (string.Equals(gameModeId, "elimination", StringComparison.OrdinalIgnoreCase))
        {
            return Gamemode.Elimination;
        }

        if (string.Equals(gameModeId, "fighter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameModeId, "fighting-game", StringComparison.OrdinalIgnoreCase))
        {
            return Gamemode.Fighter;
        }

        if (string.Equals(gameModeId, "chaos", StringComparison.OrdinalIgnoreCase))
        {
            return Gamemode.Chaos;
        }

        // Normal, empty, and unknown ids all use the safe baseline. Unknown matters for forward
        // compatibility: a peer on an older build that has never heard of a mode still resolves to
        // something both machines agree on rather than splitting the rules.
        return Gamemode.Normal;
    }

    private static string GetOnlineGameModeId(Gamemode mode)
    {
        switch (mode)
        {
            case Gamemode.Turbo:
                return "turbo";
            case Gamemode.Elimination:
                return "elimination";
            case Gamemode.Fighter:
                return "fighting-game";
            case Gamemode.Chaos:
                return "chaos";
            default:
                return OnlineGameModeSelection.DefaultId;
        }
    }

    public void StartOnlineMatch(OnlineMatchRoster roster)
    {
        if (!TryGetOnlineRosterSlotCount(roster, out int simulationSlotCount)
            || playerPrefab == null)
        {
            Debug.LogWarning("[GameManager] Refused to start an online match with an invalid roster or missing player prefab.");
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
        tempUI?.CloseAllCodeModePrompts();
        SettingsManager.Instance?.BeginOnlineLocalControlSession(GetPreOnlineLocalControlPlayer());

        RollbackManager.Instance.InputDelay = Mathf.Max(RollbackManager.Instance.InputDelay, 3);
        onlineDisconnectedSlots.Clear();
        ApplyOnlineRoster(roster);

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
        // playerCount is the serialized/input slot span, not necessarily the number of peers.
        // P1 + P3 therefore uses slots 0..2, with slot 1 represented by an inert placeholder.
        playerCount = simulationSlotCount;
        syncedInput = new ulong[Mathf.Max(2, playerCount)];
        for (int i = 0; i < syncedInput.Length; i++)
        {
            syncedInput[i] = 5UL;
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

        ApplyOnlineRosterSlotOccupancy(
            roster,
            playerCount,
            preserveExistingDisconnects: false,
            newlyOccupiedSlots: null);

        RollbackManager.Instance.Init(roster);
        if (StressTestController.Instance != null && StressTestController.Instance.enableStressTest)
        {
            StressTestController.Instance.ResetForNewMatch();
        }

        MatchMessageManager.Instance?.StartMatch(roster);
        MatchMessageManager.Instance?.SendReadySignal();

        isOnlineMatchActive = true;
        isWaitingForOpponent = true;
        SetLocalOnlineInputCaptureSuppressed(true);
        SetNetworkInfoVisible(true);
        ProjectileManager.Instance.InitializeAllProjectiles();
        SetStage(-1);
        ResetPlayers();
        isRunning = true;
        SteamLobbyManager.Instance?.NotifyOnlineMatchStarted();
    }

    public bool TryRefreshOnlineLobbyRoster(OnlineMatchRoster roster)
    {
        if (!TryGetOnlineRosterSlotCount(roster, out int rosterSlotCount)
            || !CanStartOrRefreshOnlineLobby(roster)
            || playerPrefab == null)
        {
            return false;
        }

        HashSet<int> previouslyOccupiedSlots = new HashSet<int>();
        if (activeOnlineRoster?.Peers != null)
        {
            for (int i = 0; i < activeOnlineRoster.Peers.Count; i++)
            {
                OnlineMatchPeerInfo peer = activeOnlineRoster.Peers[i];
                if (peer != null)
                {
                    previouslyOccupiedSlots.Add(peer.PlayerSlot);
                }
            }
        }

        HashSet<int> newlyOccupiedSlots = new HashSet<int>();
        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer != null && !previouslyOccupiedSlots.Contains(peer.PlayerSlot))
            {
                newlyOccupiedSlots.Add(peer.PlayerSlot);
            }
        }

        ApplyOnlineRoster(roster);

        // Never shrink an already-running serialized slot span. A later member may fill a gap or
        // extend it (P1+P2 adding P4 creates inert P3 plus live P4), but every existing peer must
        // retain the same state layout while the roster update and snapshot are exchanged.
        playerCount = Mathf.Max(playerCount, rosterSlotCount);
        bool createdPlayer = false;
        for (int slot = 0; slot < playerCount; slot++)
        {
            bool slotIsOccupied = roster.TryGetSteamIdForSlot(slot, out Steamworks.SteamId _);

            // A slot that used to be an inert placeholder now belongs to a joining peer. Recreate
            // it so no eliminated health/input/UI state leaks into the new participant.
            if (slotIsOccupied && newlyOccupiedSlots.Contains(slot) && players[slot] != null)
            {
                Destroy(players[slot].gameObject);
                players[slot] = null;
            }

            if (players[slot] == null)
            {
                CreateOnlinePlayerForSlot(slot, slotIsOccupied && slot == localPlayerIndex);
                createdPlayer = true;
            }
        }

        ApplyOnlineRosterSlotOccupancy(
            roster,
            playerCount,
            preserveExistingDisconnects: true,
            newlyOccupiedSlots: newlyOccupiedSlots);

        // A disconnected slot's machine is permanently closed by SimulateOnline so it cannot
        // generate choices for an inert placeholder. Re-open only the machines whose slots were
        // filled by this roster expansion, and bind them to the newly-created player object before
        // the authoritative lobby snapshot is saved/sent. This is required for sparse P3/P4
        // drop-ins in both Normal and Chaos modes.
        if (newlyOccupiedSlots.Count > 0)
        {
            foreach (GameObject gambaGO in GetValidGambaObjects(refreshIfNeeded: true))
            {
                GambaMachine gamba = gambaGO != null ? gambaGO.GetComponent<GambaMachine>() : null;
                int ownerSlot = gamba != null ? gamba.ownerPID - 1 : -1;
                if (ownerSlot < 0
                    || ownerSlot >= playerCount
                    || !newlyOccupiedSlots.Contains(ownerSlot)
                    || players[ownerSlot] == null)
                {
                    continue;
                }

                gamba.ownerPlayer = players[ownerSlot];
                gamba.ResetLobbyState();
            }
        }
        EnsureOnlineLocalPlayerInputActive();

        syncedInput = new ulong[Mathf.Max(2, playerCount)];
        for (int i = 0; i < syncedInput.Length; i++)
        {
            syncedInput[i] = 5UL;
        }
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
        // Waiting-screen input is local UI input, not deterministic lobby gameplay input. Keep it
        // suppressed until GatherInputForOnline opens capture and baselines on the first real tick.
        SetLocalOnlineInputCaptureSuppressed(true);
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
        // Waiting-screen input is local UI input, not deterministic lobby gameplay input. Keep it
        // suppressed until GatherInputForOnline opens capture and baselines on the first real tick.
        SetLocalOnlineInputCaptureSuppressed(true);

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
        requestedOnlineEndLoadTransitionId = 0;
        lastAppliedGameplayStageTransitionId = 0;
        lastAppliedRematchLobbyTransitionId = 0;
        lastAppliedRematchLobbySeed = 0;
        localGameplayReadyTransitionId = 0;
        remoteGameplayReadyTransitionId = 0;
        pendingRemoteGameplayReadyTransitionId = 0;
        pendingStageSelectTransitionId = 0;
        pendingRemoteSceneReadyTransitionId = 0;
        pendingOpponentShopTransitionId = 0;
        pendingStageSelectTotalRoundsPlayed = -1;
        pendingPeerDropFrames.Clear();
        peerDropAcknowledgedSlots.Clear();
        completedSceneReadyResponseTimeBySlot.Clear();
        onlineTransitionLivenessGraceArmed = false;
        onlineTransitionLivenessGraceDeadline = 0f;
    }

    private void BeginTrackedOnlineTransition(int transitionId)
    {
        activeOnlineTransitionId = transitionId;
        isTransitioning = true;
        localSceneTransitionReady = false;
        remoteSceneTransitionReady = false;
        sceneReadyPeerSlots.Clear();
        pendingSceneReadyBySlot.Clear();
        completedSceneReadyResponseTimeBySlot.Clear();
        hasPendingRemoteSceneReady = false;
        pendingRemoteSceneReadyTransitionId = 0;
        pendingRemoteSceneReadyType = 0;
        pendingRemoteSceneReadySignature = 0;
        nextOnlineTransitionWatchdogTime = 0f;
        onlineTransitionLivenessGraceArmed = false;
        onlineTransitionLivenessGraceDeadline = 0f;
        RefreshNetworkActivityGrace();
    }

    private void CompleteTrackedOnlineTransition()
    {
        bool completedRematchLobbyTransition = activeOnlineTransitionId > 0
            && activeOnlineTransitionId == lastAppliedRematchLobbyTransitionId
            && SceneManager.GetActiveScene().name == "MainMenu";

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
        onlineTransitionLivenessGraceArmed = false;
        onlineTransitionLivenessGraceDeadline = 0f;
        RefreshNetworkActivityGrace();

        if (completedRematchLobbyTransition && tempUI != null)
        {
            tempUI.gameObject.SetActive(true);
            if (roundEndedText != null)
            {
                roundEndedText.enabled = true;
            }
            SetNetworkInfoVisible(true);
        }
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

    /// <summary>
    /// Host-side End-screen timeout handling. The gameplay simulation is already stopped here, so
    /// remove the stale slot directly rather than trying to rollback/resimulate the completed match.
    /// </summary>
    public bool DropUnresponsiveEndScreenPeer(int slot)
    {
        if (!isOnlineMatchActive
            || !IsOnlineHostAuthority()
            || SceneManager.GetActiveScene().name != "End"
            || slot < 0
            || slot >= players.Length
            || slot == localPlayerIndex
            || IsOnlineHostSlot(slot)
            || !IsPlayerSlotConnected(slot))
        {
            return false;
        }

        return DropUnresponsivePeerOutsideSimulation(slot, "End screen rematch vote");
    }

    /// <summary>
    /// Host-side timeout handling while gameplay is paused for an online scene transition.
    /// Removing the peer directly lets the remaining scene-ready quorum complete without
    /// attempting to rollback a simulation that is not currently running.
    /// </summary>
    public bool DropUnresponsiveOnlineTransitionPeer(int slot)
    {
        if (!isOnlineMatchActive
            || !isTransitioning
            || !IsOnlineHostAuthority()
            || slot < 0
            || slot >= players.Length
            || slot == localPlayerIndex
            || IsOnlineHostSlot(slot)
            || !IsPlayerSlotConnected(slot))
        {
            return false;
        }

        bool dropped = DropUnresponsivePeerOutsideSimulation(slot, "scene transition");
        if (dropped && CountConnectedPlayers() < 2)
        {
            ResetToMainMenuAfterHostDisconnect("Every other player disconnected during scene transition");
        }
        return dropped;
    }

    private bool DropUnresponsivePeerOutsideSimulation(int slot, string context)
    {
        int dropFrame = frameNumber;
        pendingPeerDropFrames[slot] = dropFrame;
        peerDropAcknowledgedSlots[slot] = new HashSet<int> { localPlayerIndex };
        StartCoroutine(BroadcastPeerDropOutsideSimulationUntilAcknowledged(slot, dropFrame));
        ApplyPeerDropOutsideSimulation(slot, dropFrame);
        Debug.LogWarning($"[OnlineMatch] Timed out P{slot + 1} during {context}; removed from the connected-player quorum.");
        return true;
    }

    public void ApplyEndScreenPeerDrop(int slot, int dropFrame)
    {
        ApplyPeerDropOutsideSimulation(slot, dropFrame);
    }

    public void ApplyPeerDropOutsideSimulation(int slot, int dropFrame)
    {
        if (!isOnlineMatchActive
            || (SceneManager.GetActiveScene().name != "End" && !isTransitioning)
            || slot < 0
            || slot >= players.Length
            || slot == localPlayerIndex)
        {
            return;
        }

        if (RollbackManager.Instance == null
            || !RollbackManager.Instance.DropRemoteSlotOutsideSimulation(slot, dropFrame))
        {
            MarkPlayerDisconnected(slot, dropFrame);
            MatchMessageManager.Instance?.DropPeerTransport(slot);
        }

        readyPeerSlots.Remove(slot);
        gameplayReadyPeerSlots.Remove(slot);
        sceneReadyPeerSlots.Remove(slot);
        pendingGameplayReadyBySlot.Remove(slot);
        pendingGameplayReadyTransitionBySlot.Remove(slot);
        pendingSceneReadyBySlot.Remove(slot);

        if (isTransitioning)
        {
            CheckSceneTransitionReady();
        }
    }

    public void OnPeerDropAcknowledged(int senderSlot, int droppedSlot, int dropFrame)
    {
        if (!isOnlineMatchActive
            || !IsOnlineHostAuthority()
            || !IsPlayerSlotConnected(senderSlot)
            || !pendingPeerDropFrames.TryGetValue(droppedSlot, out int expectedDropFrame)
            || expectedDropFrame != dropFrame
            || !peerDropAcknowledgedSlots.TryGetValue(droppedSlot, out HashSet<int> acknowledgedSlots))
        {
            return;
        }

        acknowledgedSlots.Add(senderSlot);
    }

    private IEnumerator BroadcastPeerDropOutsideSimulationUntilAcknowledged(int slot, int dropFrame)
    {
        while (isOnlineMatchActive && !HaveAllSurvivingPeerDropAcknowledgements(slot, dropFrame))
        {
            MatchMessageManager.Instance?.SendPeerDrop(slot, dropFrame);
            yield return new WaitForSecondsRealtime(0.25f);
        }

        pendingPeerDropFrames.Remove(slot);
        peerDropAcknowledgedSlots.Remove(slot);
    }

    private bool HaveAllSurvivingPeerDropAcknowledgements(int droppedSlot, int dropFrame)
    {
        if (!pendingPeerDropFrames.TryGetValue(droppedSlot, out int expectedDropFrame)
            || expectedDropFrame != dropFrame
            || !peerDropAcknowledgedSlots.TryGetValue(droppedSlot, out HashSet<int> acknowledgedSlots))
        {
            return true;
        }

        for (int slot = 0; slot < players.Length; slot++)
        {
            if (slot != droppedSlot
                && IsPlayerSlotConnected(slot)
                && !acknowledgedSlots.Contains(slot))
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyDisconnectedPlayerSlots(bool cleanupProjectiles)
    {
        // A lobby snapshot serializes PlayerController.isConnected but not this runtime set. Import
        // any disconnected slot from the restored state so a later roster refresh cannot revive it.
        if (activeOnlineRoster != null)
        {
            for (int slot = 0; slot < playerCount; slot++)
            {
                if (players[slot] != null && !players[slot].isConnected)
                {
                    onlineDisconnectedSlots.Add(slot);
                }
            }
        }

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
            if (p.spriteRenderer != null) p.spriteRenderer.enabled = false;
            if (p.inputDisplay != null) p.inputDisplay.enabled = false;
            if (p.playerNum != null) p.playerNum.enabled = false;

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
        if (slot < 0 || slot >= players.Length || players[slot] == null)
        {
            return false;
        }

        // Connectivity/disconnect elimination belongs to peer-roster matches. Offline local
        // players retain the original behavior: every registered slot is active.
        if (activeOnlineRoster == null)
        {
            return true;
        }

        return !onlineDisconnectedSlots.Contains(slot) && players[slot].isConnected;
    }

    /// <summary>
    /// Number of players still connected to an online match.
    /// </summary>
    public int ActivePlayerCount =>
        activeOnlineRoster != null ? CountConnectedPlayers() : playerCount;

    public int GetConnectedPlayerSlotMask()
    {
        int mask = 0;
        for (int slot = 0; slot < players.Length; slot++)
        {
            if (IsPlayerSlotConnected(slot))
            {
                mask |= 1 << slot;
            }
        }
        return mask;
    }

    /// <summary>
    /// Drops every online slot that is NOT in the mask, outside the simulation. Used by the End
    /// screen when only some players chose Rematch: the leavers are removed exactly like a clean
    /// disconnect, so the survivors keep their existing P1-P4 slots and every downstream consumer
    /// (GetConnectedPlayerSlotMask, ActivePlayerCount, the rematch transition) is already correct
    /// without needing to know a vote happened.
    ///
    /// Call this only AFTER the leavers have acknowledged the result -- dropping their transport
    /// first would strand them without ever hearing the outcome.
    /// </summary>
    public void DropOnlineSlotsOutsideMask(int survivingSlotMask)
    {
        ApplyOnlineConnectedPlayerSlotMask(survivingSlotMask);
    }

    private void ApplyOnlineConnectedPlayerSlotMask(int connectedSlotMask)
    {
        if (activeOnlineRoster == null || connectedSlotMask < 0)
        {
            return;
        }

        for (int slot = 0; slot < playerCount; slot++)
        {
            if (!activeOnlineRoster.TryGetSteamIdForSlot(slot, out Steamworks.SteamId _)
                || (connectedSlotMask & (1 << slot)) != 0)
            {
                continue;
            }

            if (RollbackManager.Instance == null
                || !RollbackManager.Instance.DropRemoteSlotOutsideSimulation(slot, frameNumber))
            {
                onlineDisconnectedSlots.Add(slot);
                ApplyDisconnectedPlayerSlot(slot, cleanupProjectiles: false);
                MatchMessageManager.Instance?.DropPeerTransport(slot);
            }
        }
    }

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

    private int CountRegisteredOnlineRosterPlayers()
    {
        if (activeOnlineRoster?.Peers == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < activeOnlineRoster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = activeOnlineRoster.Peers[i];
            if (peer != null
                && peer.PlayerSlot >= 0
                && peer.PlayerSlot < players.Length
                && players[peer.PlayerSlot] != null)
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
        GameEnd(endedByDisconnect: true);
    }

    private void ApplyOnlineEndWinner(int winnerPid)
    {
        endWinnerPid = winnerPid;
        bigWinner = winnerPid > 0 && winnerPid <= playerCount ? players[winnerPid - 1] : null;
        endWinnerPalette = bigWinner != null
            && bigWinner.matchPalettes != null
            && winnerPid - 1 >= 0
            && winnerPid - 1 < bigWinner.matchPalettes.Length
                ? bigWinner.matchPalettes[winnerPid - 1]
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
        OnPeerSceneTransitionReady(remotePlayerIndex, transitionId, sceneType, sceneSignature, false);
    }

    public void OnPeerSceneTransitionReady(
        int playerSlot,
        int transitionId,
        byte sceneType,
        int sceneSignature,
        bool isRecoveryResponse = false)
    {
        // Scene-ready signals are sent ONCE per peer (Reliable, but one-shot at the application
        // level). Discarding one here used to be unrecoverable — the waiter's FixedUpdate
        // early-returns on isTransitioning, so a missed ready froze it in the loading limbo
        // forever while the other side played on. Every rejection below therefore either stashes
        // the ready for the transition watchdog to re-apply, or logs why it was dropped.
        if (!IsPlayerSlotConnected(playerSlot))
        {
            // The arrival block's ResetPlayers/roster churn can leave the slot momentarily
            // unconnected while a ready lands. Stash it; the watchdog re-applies once the slot
            // settles.
            Debug.LogWarning($"[OnlineTransition] Scene-ready from slot {playerSlot} while slot not connected (id={transitionId}, type={sceneType}, sig={sceneSignature}). Stashing as pending.");
            StashPendingSceneReady(playerSlot, transitionId, sceneType, sceneSignature);
            return;
        }

        if (!isTransitioning)
        {
            // A peer can complete locally while another survivor is still missing its ready
            // packet. The waiter keeps broadcasting its own ready once per second, so answer it
            // with this completed id. Recovery responses are explicitly tagged and never answered,
            // which prevents completed peers from bouncing ready packets back and forth forever.
            if (transitionId == onlineTransitionSequence
                && sceneType == GetNetworkSceneTypeCode()
                && sceneSignature == GetNetworkSceneSignature()
                && !isRecoveryResponse
                && MatchMessageManager.Instance != null)
            {
                float now = Time.unscaledTime;
                if (!completedSceneReadyResponseTimeBySlot.TryGetValue(playerSlot, out float lastResponseTime)
                    || now - lastResponseTime >= 0.5f)
                {
                    completedSceneReadyResponseTimeBySlot[playerSlot] = now;
                    MatchMessageManager.Instance.SendSceneTransitionReadySignal(
                        transitionId,
                        isRecoveryResponse: true);
                }
                return;
            }

            // A peer can finish loading before we begin (or after we completed) our own
            // transition. A stale id can never match a future activeOnlineTransitionId (ids
            // strictly increase), so stashing is safe and a discarded-early ready is not.
            Debug.LogWarning($"[OnlineTransition] Scene-ready from slot {playerSlot} while not transitioning (id={transitionId}, type={sceneType}, sig={sceneSignature}). Stashing as pending.");
            StashPendingSceneReady(playerSlot, transitionId, sceneType, sceneSignature);
            return;
        }

        if (transitionId != activeOnlineTransitionId)
        {
            if (transitionId > activeOnlineTransitionId)
            {
                StashPendingSceneReady(playerSlot, transitionId, sceneType, sceneSignature);
            }
            else
            {
                Debug.LogWarning($"[OnlineTransition] Dropping stale scene-ready from slot {playerSlot} (id={transitionId} < active={activeOnlineTransitionId}).");
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

    private void StashPendingSceneReady(int playerSlot, int transitionId, byte sceneType, int sceneSignature)
    {
        if (IsRosterBasedOnlineMatch() && playerSlot >= 0)
        {
            pendingSceneReadyBySlot[playerSlot] = (transitionId, sceneType, sceneSignature);
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

    // Self-healing for the scene-transition handshake. Each peer announces "scene ready" once;
    // the receive path has timing windows where that single announcement can be stashed or
    // missed (slot momentarily unconnected during arrival resets, ready landing outside the
    // receiver's transitioning window, pended under an id the receiver hadn't reached yet).
    // FixedUpdate early-returns while isTransitioning, so a waiter cannot recover on its own
    // Runs from Update on unscaled time: re-send the ready, re-evaluate stashed
    // peer readies, and log the wait state so any residual stall names its blocking gate.
    private float nextOnlineTransitionWatchdogTime;
    private bool onlineTransitionLivenessGraceArmed;
    private float onlineTransitionLivenessGraceDeadline;

    private void UpdateOnlineTransitionWatchdog()
    {
        if (!isOnlineMatchActive || !isTransitioning)
        {
            return;
        }

        if (Time.unscaledTime < nextOnlineTransitionWatchdogTime)
        {
            return;
        }
        nextOnlineTransitionWatchdogTime = Time.unscaledTime + 1f;

        if (localSceneTransitionReady && MatchMessageManager.Instance != null)
        {
            MatchMessageManager.Instance.SendSceneTransitionReadySignal(activeOnlineTransitionId);
        }

        if (IsOnlineHostAuthority() && MatchMessageManager.Instance != null)
        {
            // A peer that missed the one authoritative stage packet never reaches Gameplay and
            // therefore can never answer scene-ready. Re-send the exact cached packet while this
            // transition is pending; receivers treat same-id duplicates as idempotent below.
            MatchMessageManager.Instance.ResendLastStageSelect(activeOnlineTransitionId);
            MatchMessageManager.Instance.ResendLastRematchLobbyTransition(activeOnlineTransitionId);
        }

        ApplyPendingSceneTransitionReadyIfAvailable();
        CheckSceneTransitionReady();

        if (isTransitioning)
        {
            UpdateOnlineTransitionPeerLiveness();
        }

        if (!isOnlineMatchActive || !isTransitioning)
        {
            return;
        }

        Debug.LogWarning($"[OnlineTransition] Waiting on scene-ready handshake. id={activeOnlineTransitionId} localReady={localSceneTransitionReady} readySlots=[{string.Join(",", sceneReadyPeerSlots)}] connected={CountConnectedPlayers()} pendingBySlot={pendingSceneReadyBySlot.Count} scene={SceneManager.GetActiveScene().name} sig={GetNetworkSceneSignature()}");
    }

    private void UpdateOnlineTransitionPeerLiveness()
    {
        // Do not time out a legitimate slow loader. Start a full grace window only after this
        // machine has finished loading and can once again pump/receive network packets.
        if (!localSceneTransitionReady)
        {
            return;
        }

        if (!onlineTransitionLivenessGraceArmed)
        {
            onlineTransitionLivenessGraceArmed = true;
            onlineTransitionLivenessGraceDeadline = Time.unscaledTime + TRANSITION_NETWORK_GRACE_SECONDS;
            return;
        }

        if (Time.unscaledTime < onlineTransitionLivenessGraceDeadline)
        {
            return;
        }

        MatchMessageManager messageManager = MatchMessageManager.Instance;
        if (messageManager == null)
        {
            ResetToMainMenuAfterHostDisconnect("Network transport disappeared during scene transition");
            return;
        }

        if (IsOnlineHostAuthority())
        {
            for (int slot = 0; slot < players.Length; slot++)
            {
                if (slot == localPlayerIndex
                    || !IsPlayerSlotConnected(slot)
                    || messageManager.IsPeerResponsive(slot, TRANSITION_NETWORK_GRACE_SECONDS))
                {
                    continue;
                }

                DropUnresponsiveOnlineTransitionPeer(slot);
                if (!isOnlineMatchActive)
                {
                    return;
                }
            }
            return;
        }

        int hostSlot = -1;
        for (int slot = 0; slot < players.Length; slot++)
        {
            if (IsOnlineHostSlot(slot))
            {
                hostSlot = slot;
                break;
            }
        }

        if (hostSlot < 0
            || !IsPlayerSlotConnected(hostSlot)
            || !messageManager.IsPeerResponsive(hostSlot, TRANSITION_NETWORK_GRACE_SECONDS))
        {
            ResetToMainMenuAfterHostDisconnect("Host stopped responding during scene transition");
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

    public bool HandleOnlineStageSelect(int transitionId, byte packetSceneType, int packetSceneSignature, int stageIndex, uint hostStageRngState, int hostTotalRoundsPlayed = -1, uint hostGameplayRngState = 0, int hostRandomCallCount = -1, int connectedPlayerSlotMask = -1)
    {
        int expectedTransitionId = GetExpectedOnlineTransitionId();
        byte currentSceneType = GetNetworkSceneTypeCode();
        bool isGameplayStageCorrection = packetSceneType == 1
            && currentSceneType == 1
            && stageIndex >= 0
            && stageIndex < stages.Count
            && stageIndex != currentStageIndex;

        // Rematches now transition End -> MainMenu through REMATCH_LOBBY_TRANSITION. A delayed or
        // cached gameplay STAGE_SELECT must never bring persistent stage geometry back over End (or
        // skip the starter lobby by loading Gameplay directly).
        if (packetSceneType == 1 && currentSceneType == 4)
        {
            Debug.LogWarning($"Ignoring gameplay stage select while the End scene is active. Transition={transitionId}, StageIndex={stageIndex}");
            return false;
        }

        if (packetSceneType == 1
            && transitionId > 0
            && transitionId < lastAppliedGameplayStageTransitionId)
        {
            Debug.LogWarning($"Ignoring stale gameplay stage select packet. Transition={transitionId}, LastApplied={lastAppliedGameplayStageTransitionId}, StageIndex={stageIndex}, CurrentStageIndex={currentStageIndex}");
            return false;
        }

        bool isDuplicateActiveGameplayStage = packetSceneType == 1
            && transitionId > 0
            && transitionId == activeOnlineTransitionId
            && transitionId == lastAppliedGameplayStageTransitionId
            && stageIndex == currentStageIndex;
        if (isDuplicateActiveGameplayStage)
        {
            if (localSceneTransitionReady && MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.SendSceneTransitionReadySignal(transitionId);
            }
            return true;
        }

        if (transitionId < expectedTransitionId && !isGameplayStageCorrection)
        {
            return false;
        }

        if (activeOnlineTransitionId > 0
            && transitionId != activeOnlineTransitionId
            && !isGameplayStageCorrection)
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
            ApplyOnlineConnectedPlayerSlotMask(connectedPlayerSlotMask);

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
        if (!isOnlineMatchActive
            || isTransitioning
            || !IsOnlineHostAuthority()
            || MatchMessageManager.Instance == null)
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
        // Must use the SAME base as the OnSceneLoaded computation. This hardcoded 300 against
        // that path's baseRamNeeddedtowin (400) put the two machines a permanent 100 apart for the
        // same round count, and ramNeededToWinRound is part of SerializeSharedGameplayHashState
        // so the shared hash diverged on frame one and never reconverged.
        ramNeededToWinRound = (ushort)(baseRamNeeddedtowin + 100 * dataManager.totalRoundsPlayed);
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
        OnlineEndOptionsEpoch = 0;
        preparedOnlineRematchEpoch = -1;
        rematchPreparationStarted = false;
        endScreenRendererVisibility.Clear();

        if (isOnlineMatchActive)
        {
            //Debug.Log("Cleaning up online match state...");
            PlayerController onlineLocalPlayer = players != null
                && localPlayerIndex >= 0
                && localPlayerIndex < players.Length
                    ? players[localPlayerIndex]
                    : null;
            SettingsManager.Instance?.EndOnlineLocalControlSession(onlineLocalPlayer);

            ResetOnlineTransitionTracking();
            tempUI?.CloseAllCodeModePrompts();

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
        Debug.LogWarning($"[OnlineMatch] {reason}. Returning surviving players to SoloLobby.");
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

        }
    }

    private void SimulateOnlineFloppies(ulong[] inputs, bool isRealFrame)
    {
        if (floppyObjects == null || floppyObjects.Length == 0)
        {
            FindAllFloppyDisks();
        }
        if (floppyObjects == null) return;

        // Keep this simulation pass stable even if a future callback refreshes floppyObjects.
        // Snapshot publication is deferred below, but the local copy also makes the iteration
        // resilient to other scene/state refreshes introduced later.
        GameObject[] floppiesToSimulate = floppyObjects;
        for (int i = 0; i < floppiesToSimulate.Length; i++)
        {
            GameObject floppy = floppiesToSimulate[i];
            if (floppy == null) continue;
            FloppyPickup disk = floppy.GetComponent<FloppyPickup>();
            if (disk != null)
            {
                disk.SimulateOnline(inputs, isRealFrame);
            }
        }

        if (isRealFrame && pendingOnlineFloppySnapshot)
        {
            string reason = pendingOnlineFloppySnapshotReason;
            pendingOnlineFloppySnapshot = false;
            pendingOnlineFloppySnapshotReason = null;
            BroadcastAuthoritativeOnlineStateSnapshot(reason);
        }
    }

    public void RequestAuthoritativeOnlineFloppySnapshot(string reason)
    {
        if (!isOnlineMatchActive)
        {
            return;
        }

        // FloppyPickup runs while SimulateOnlineFloppies is iterating floppyObjects. The host's
        // authoritative snapshot self-apply refreshes that array, so broadcasting inline can skip
        // later disks (especially Chaos's six stacked pickups). Coalesce every pickup completed in
        // this simulation pass and publish the final state after the loop instead.
        pendingOnlineFloppySnapshot = true;
        pendingOnlineFloppySnapshotReason = reason;
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
            && CountRegisteredOnlineRosterPlayers() < activeOnlineRoster.PlayerCount)
        {
            SetLocalOnlineInputCaptureSuppressed(true);
            Debug.Log($"[OnlineState] Holding round start: {CountRegisteredOnlineRosterPlayers()}/{activeOnlineRoster.PlayerCount} players registered.");
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

        localPlayerInput = GatherInputForOnline(out InputPlayerBindings pendingInputCapture);
        bool acceptedLocalInput = rbManager.SendLocalInput(localPlayerInput);
        if (acceptedLocalInput && pendingInputCapture != null)
        {
            pendingInputCapture.CommitPeekedOnlineInputs();
        }

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

        // Chaos always replaces the complete loadout between rounds. A full six-spell inventory
        // therefore enters the Shop instead of taking Normal's direct next-round shortcut.
        bool hasMaxSpells = gamemode != Gamemode.Chaos && AllActivePlayersHaveMaxSpells();

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
            message = "Game Over!!!";
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
            bool skipsShop = gamemode != Gamemode.Chaos && AllActivePlayersHaveMaxSpells();
            string nextPhase = skipsShop ? "Beginning Next Round..." : "Beginning Shop Phase...";
            message = "Player " + lastRoundWinnerPID + " wins the round! " + nextPhase;

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

        // GameEndScreen owns End-scene navigation. Keeping the old jump shortcut here would make
        // the first player's confirmation bypass the new Rematch/Main Menu group selection.
        ///shop specific update
        if (activeScene.name == "Shop")
        {
            for (int i = 0; i < playerCount; i++)
            {
                players[i].roundRam = 0; // reset round RAM to prevent carryover from lobby
                players[i].storedKillBonus = 0;
            }
            goDoorPrefab.CheckOpenDoor();

            if (goDoorPrefab.CheckAllPlayersReady())
            {
                LoadRandomGameplayStage();
            }
        }


        ///onboard manager specific update
        if (activeScene.name == "MainMenu")
        {
            if (onboardManager == null)
            {
                onboardManager = FindAnyObjectByType<OnboardManager>();
            }
            onboardManager.enabled = true;
            onboardManager.OnboardUpdate(inputs);
        }
        else
        {
            if (onboardManager != null)
            {
                onboardManager = null;
            }

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
                    else if (gamemode == Gamemode.Chaos)
                    {
                        // Reset round RAM HERE, not only once the Shop scene loads
                        for (int i = 0; i < playerCount; i++)
                        {
                            players[i].roundRam = 0;
                            players[i].storedKillBonus = 0;
                            players[i].ClearSpellList();
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
        ApplyDeterministicGamemodeRules();

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

        // Training room resource overrides are pinned last, after ProcEffectUpdate, so the per-frame
        // decay inside the universal passives (demon aura falloff especially) can't undo them.
        // No-ops outside the training scene and during online matches.
        TrainingOptionsMachine.ApplyAllOverrides();
    }

    /// <summary>
    /// Applies simulation-affecting mode rules once per simulated frame.
    /// This must remain inside UpdateGameState: rollback resimulation calls UpdateGameState directly
    /// and bypasses both RunFrame and RunOnlineFrame.
    /// </summary>
    private void ApplyDeterministicGamemodeRules()
    {
        if (gamemode != Gamemode.Turbo)
        {
            return;
        }

        foreach (PlayerController player in players)
        {
            if (player == null || player.spellList == null)
            {
                continue;
            }

            for (int i = 0; i < player.spellList.Count; i++)
            {
                if (player.spellList[i] != null)
                {
                    // Turbo reduces every spell cooldown to one simulation tick.
                    player.spellList[i].cooldown = 1;
                }
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

        int newPlayerIndex = playerCount;
        players[newPlayerIndex] = existingPlayer;
        players[newPlayerIndex]._playerPauseIndex = newPlayerIndex;
        InputDevice joinedDevice = playerInput.devices.Count > 0 ? playerInput.devices[0] : null;
        players[newPlayerIndex].inputs.AssignInputDevice(joinedDevice);
        SettingsManager.Instance?.TryApplyControlOptionsForPlayer(players[newPlayerIndex]);
        AnimationManager.Instance.InitializePlayerVisuals(players[newPlayerIndex], newPlayerIndex);

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
        int activePlayerCount = 0;
        //bool disregardRam = false;
        for (int i = 0; i < playerCount; i++)
        {
            if (!IsPlayerSlotConnected(i))
            {
                continue;
            }

            averageRoundRam += players[i].roundRam;
            averageRoundWins += players[i].roundsWon;
            activePlayerCount++;
        }

        if (activePlayerCount == 0)
        {
            return;
        }

        averageRoundRam = (ushort)(averageRoundRam / activePlayerCount);
        averageRoundWins = averageRoundWins / activePlayerCount;


        for (int i = 0; i < playerCount; i++)
        {
            if (!IsPlayerSlotConnected(i))
            {
                if (players[i] != null)
                {
                    players[i].ramBounty = 0;
                }
                continue;
            }

            int ramRoundBounty = roundOver? 0: (players[i].roundRam - averageRoundRam)/3;
            int oldBounty = players[i].ramBounty;
            
            players[i].ramBounty = (short)( ramRoundBounty + (100*(players[i].roundsWon - averageRoundWins)));
            if(oldBounty != players[i].ramBounty)
            {
                Debug.Log($"Player {i+1} Old Bounty: {oldBounty}");
                Debug.Log($"Player {i+1} New Bounty: {players[i].ramBounty}");
            }
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

            if (!IsPlayerSlotConnected(i))
            {
                continue;
            }

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
        int _playerWithHighestBountyIndex = -1;

        //iterate through players list and find the player with the highest bounty
        for (int i = 0; i < playerCount; i++)
        {
            if (!IsPlayerSlotConnected(i))
            {
                continue;
            }

            if (_playerWithHighestBountyIndex < 0
                || players[i].ramBounty > players[_playerWithHighestBountyIndex].ramBounty)
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
            if (!IsPlayerSlotConnected(i))
            {
                continue;
            }

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
                        if (!IsPlayerSlotConnected(i))
                        {
                            continue;
                        }

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
                            if (!IsPlayerSlotConnected(i))
                            {
                                continue;
                            }

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
                                endWinnerPalette = winner.matchPalettes != null
                                    && winner.pID - 1 >= 0
                                    && winner.pID - 1 < winner.matchPalettes.Length
                                    ? winner.matchPalettes[winner.pID - 1]
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
        // The real fighters are DontDestroyOnLoad and the End scene presents its winner through a
        // separate authored sprite. Respawning here would re-enable the carried character renderers
        // (and fire spawn side effects) after the match has already finished.
        if (SceneManager.GetActiveScene().name == "End")
        {
            return;
        }

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
                int spawnIndex = ResolveSpawnIndexForSlot(i, spawnPos.Length);
                players[i].SpawnPlayer(spawnPos[spawnIndex]);
                players[i].inputDisplay.enabled = true;
                players[i].playerNum.enabled = true;
            }
        }

        isSaved = false;
    }

    public void ResetPlayerFromMainMenuBounds(PlayerController player)
    {
        if (player == null || SceneManager.GetActiveScene().name != "MainMenu")
        {
            return;
        }

        int playerIndex = Array.IndexOf(players, player);
        Vector2[] spawnPositions = GetSpawnPositions();
        if (playerIndex < 0 || playerIndex >= spawnPositions.Length)
        {
            return;
        }

        player.ClearSpellList();
        player.chosenSpell = false;
        player.chosenStartingSpell = false;
        player.startingSpellAdded = false;
        player.basicsFired = 0;
        player.spellsFired = 0;
        player.spellsHit = 0;
        player.roundsWon = 0;
        // SpellCode_Gate.CheckGateBroken reads this to decide whether the gate opens or the
        // projectile is deleted, and the gate lives in Lobby_Arena as well as Tutorial. It is
        // [NonSerialized] and only ever SET (in the Tutorial scene), so without this a player who
        // ran the tutorial and then entered an online match carried true while their peer had
        // false, and the two simulated that gate differently.
        player.tutorialSpellStored = false;
        player.storedKillBonus = 0;
        player.roundRam = 0;
        player.ramBounty = 0;
        player.times = new List<Fixed>();
        player.SpawnPlayer(FixedVec2.FromFloat(spawnPositions[playerIndex].x, spawnPositions[playerIndex].y));

        player.demonX = false;
        player.bigStox = false;
        player.killeez = false;
        player.vWave = false;

        if (player.inputDisplay != null) player.inputDisplay.enabled = true;
        if (player.playerNum != null) player.playerNum.enabled = true;

        if (playerIndex < gates.Length && gates[playerIndex] != null)
        {
            gates[playerIndex].SetOpen(false);
        }

        // This reset runs inside the deterministic sim (CheckStageDataSOCollision), so under rollback
        // it re-runs. Keep the deterministic sim mutations above (spellList/stats/respawn, gamba
        // state, gate) but fire the purely-visual/local side effects only on the real frame, or they
        // flicker/re-pop on every rollback
        bool isRealFrame = RollbackManager.Instance == null || !RollbackManager.Instance.isRollbackFrame;

        for (int i = 0; i < gambas.Count; i++)
        {
            GambaMachine gamba = gambas[i] != null ? gambas[i].GetComponent<GambaMachine>() : null;
            if (gamba != null && gamba.ownerPID == playerIndex + 1)
            {
                bool preserveOnlineChaosRoll = isOnlineMatchActive && gamemode == Gamemode.Chaos;
                gamba.ResetLobbyState(preserveOnlineChaosRoll);
                gamba.isActive = false;
                if (isRealFrame) gamba.ApplyVisualState();
                break;
            }
        }

        if (onboardManager == null)
        {
            onboardManager = FindFirstObjectByType<OnboardManager>();
        }
        if (isRealFrame && onboardManager != null)
        {
            onboardManager.ResetPlayerOnboarding(playerIndex);
        }
    }

    /// <summary>
    /// Restart gamestate when "play" or "rematch" is pressed
    /// </summary>
    public void RestartGame()
    {
        gameOver = false;
        if (activeOnlineRoster == null)
        {
            onlineDisconnectedSlots.Clear();
        }
        else
        {
            // Preserve peers that dropped during the prior round/match; their transport was removed
            // and a rematch cannot silently resurrect them. Also reassert the roster's sparse gaps.
            for (int slot = 0; slot < playerCount; slot++)
            {
                if (!activeOnlineRoster.TryGetSteamIdForSlot(slot, out Steamworks.SteamId _))
                {
                    onlineDisconnectedSlots.Add(slot);
                }
            }
        }

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
                if (onlineDisconnectedSlots.Contains(i))
                {
                    ApplyDisconnectedPlayerSlot(i, cleanupProjectiles: false);
                    continue;
                }

                //this is different from ResetPlayers()
                players[i].isConnected = true; // Fresh match: clear any prior disconnect.
                players[i].ResetPlayer();
                int spawnIndex = ResolveSpawnIndexForSlot(i, fixedSpawnPositions.Length);
                players[i].SpawnPlayer(fixedSpawnPositions[spawnIndex]);
                players[i].inputDisplay.enabled = true;
                players[i].playerNum.enabled = true;
            }
        }
    }

    private void ResetPlayersForStartingSpellSelection()
    {
        if (activeOnlineRoster == null)
        {
            onlineDisconnectedSlots.Clear();
        }
        else
        {
            // Keep sparse roster gaps and peers that left the completed match inert. A rematch
            // lobby preserves the surviving roster; it must not silently resurrect a departed
            // player just because the match counters are being reset.
            for (int slot = 0; slot < playerCount; slot++)
            {
                if (!activeOnlineRoster.TryGetSteamIdForSlot(slot, out Steamworks.SteamId _))
                {
                    onlineDisconnectedSlots.Add(slot);
                }
            }
        }

        for (int slot = 0; slot < players.Length; slot++)
        {
            PlayerController player = players[slot];
            if (player == null)
            {
                continue;
            }

            if (onlineDisconnectedSlots.Contains(slot))
            {
                ApplyDisconnectedPlayerSlot(slot, cleanupProjectiles: false);
                continue;
            }

            player.isConnected = true;
            player.ResetPlayerForStartingSpellSelection();
        }
    }

    /// <summary>
    /// Resets a completed match while preserving the currently joined local players or online
    /// roster, leaving every surviving player's spell list empty for a new MainMenu starter
    /// selection. This is deliberately separate from SceneUiManager.Restart, whose direct scene
    /// load is not safe for retained players or non-host online peers.
    /// </summary>
    public bool PrepareRematchFromEnd(int onlineEndEpoch = 0)
    {
        if (SceneManager.GetActiveScene().name != "End")
        {
            return false;
        }

        if (isOnlineMatchActive)
        {
            if (onlineEndEpoch <= 0)
            {
                onlineEndEpoch = OnlineEndOptionsEpoch;
            }

            if (onlineEndEpoch <= 0 || onlineEndEpoch != OnlineEndOptionsEpoch)
            {
                return false;
            }

            if (preparedOnlineRematchEpoch == onlineEndEpoch)
            {
                return true;
            }
        }
        else if (rematchPreparationStarted)
        {
            return true;
        }

        rematchPreparationStarted = true;
        if (isOnlineMatchActive)
        {
            preparedOnlineRematchEpoch = onlineEndEpoch;
        }

        if (dataManager == null)
        {
            dataManager = DataManager.Instance;
        }
        dataManager?.ResetData();
        if (dataManager != null)
        {
            dataManager.roundTimer = 0;
        }

        endInputEnabled = false;
        endWinnerPid = -1;
        endWinnerPalette = null;
        bigWinner = null;
        gameOver = false;
        roundOver = false;
        roundEndFrameCounter = 0;
        roundEndTimer = 0f;
        roundTransitionPending = false;
        roundEndUIShown = false;
        lastRoundWinnerPID = -1;
        pendingOpponentShopTransition = false;
        pendingOpponentShopTransitionId = 0;
        onlineRoundAdvanceApplied = false;
        playersChosenSpell = false;
        isSaved = false;
        damageMatrix = new byte[4, 4];

        // Keep the real fighters hidden while the active scene is still End. Their exact renderer
        // states are restored by OnSceneLoaded only after the rematch destination has arrived.
        ResetPlayersForStartingSpellSelection();

        // A rematch is a new match, so begin a fresh filtered stage cycle instead of carrying the
        // exhausted/non-repeating stage pool from the completed game.
        FillGameStages();
        GameEndScreen.ActiveInstance?.RestoreHiddenMatchUiForRematch();

        if (playerWinText != null)
        {
            playerWinText.enabled = false;
        }
        if (roundEndedText != null)
        {
            roundEndedText.enabled = false;
        }

        Time.timeScale = 1f;
        if (isOnlineMatchActive)
        {
            RefreshNetworkActivityGrace();
        }
        else
        {
            // Keep the retained players paused while the screen cover loads MainMenu. The
            // MainMenu arrival block resumes the offline simulation after their lobby state and
            // persistent machines have been reset.
            isRunning = false;
        }

        return true;
    }

    public bool StartOfflineRematchLobbyFromEnd()
    {
        if (isOnlineMatchActive || !PrepareRematchFromEnd())
        {
            return false;
        }

        SetStage(-1);
        // SetStage establishes the lobby index now so OnSceneLoaded respawns at lobby positions,
        // but its persistent geometry must remain hidden behind the End -> MainMenu screen wipe.
        ClearStages();
        if (MainMenuScreen != null)
        {
            MainMenuScreen.SetActive(false);
        }
        sceneManager.LoadScene("MainMenu");
        return true;
    }

    /// <summary>
    /// Returns a completed online match to the retained-player MainMenu lobby through a
    /// host-authoritative transition. The existing lobby-ready/stage-select flow starts Gameplay
    /// only after everyone has chosen a starter and entered the door again.
    /// </summary>
    public bool StartOnlineRematchFromEnd(int onlineEndEpoch)
    {
        if (!isOnlineMatchActive
            || !IsOnlineHostAuthority()
            || MatchMessageManager.Instance == null
            || ActivePlayerCount < 2
            || onlineEndEpoch <= 0
            || onlineEndEpoch != OnlineEndOptionsEpoch)
        {
            return false;
        }

        int transitionId = onlineEndEpoch + 1;
        // The result-delivery coroutine has one caller, but keep this idempotent across an
        // accidental duplicate invocation while the asynchronous load still reports End.
        if (lastAppliedRematchLobbyTransitionId == transitionId
            && lastAppliedRematchLobbySeed > 0)
        {
            return true;
        }

        if (!PrepareRematchFromEnd(onlineEndEpoch))
        {
            return false;
        }

        int rematchSeed = GenerateFreshOnlineRematchSeed();
        isRunning = true;
        BeginOnlineRematchLobbyTransition(
            transitionId,
            rematchSeed,
            broadcastTransition: true);
        return true;
    }

    public bool HandleOnlineRematchLobbyTransition(
        int transitionId,
        int connectedPlayerSlotMask,
        int rematchSeed)
    {
        if (!isOnlineMatchActive
            || IsOnlineHostAuthority()
            || transitionId <= 0
            || connectedPlayerSlotMask < 0
            || rematchSeed <= 0
            || localPlayerIndex < 0
            || (connectedPlayerSlotMask & (1 << localPlayerIndex)) == 0)
        {
            return false;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (transitionId == lastAppliedRematchLobbyTransitionId)
        {
            if (rematchSeed != lastAppliedRematchLobbySeed)
            {
                Debug.LogError($"[OnlineRematch] Ignoring transition {transitionId} with changed seed {rematchSeed}; already accepted {lastAppliedRematchLobbySeed}.");
                return false;
            }

            // The host keeps resending its cached commit until every survivor answers scene-ready.
            // A peer that already completed locally must answer duplicates as well, otherwise a
            // lost final ready packet could leave only the host waiting forever.
            if (activeSceneName == "MainMenu"
                && (!isTransitioning || localSceneTransitionReady)
                && MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.SendSceneTransitionReadySignal(transitionId);
            }
            return activeSceneName == "MainMenu" || isTransitioning;
        }

        if (transitionId < lastAppliedRematchLobbyTransitionId
            || activeSceneName != "End"
            || OnlineEndOptionsEpoch <= 0
            || transitionId != OnlineEndOptionsEpoch + 1)
        {
            return false;
        }

        ApplyOnlineConnectedPlayerSlotMask(connectedPlayerSlotMask);
        if (ActivePlayerCount < 2 || !PrepareRematchFromEnd(OnlineEndOptionsEpoch))
        {
            return false;
        }

        isRunning = true;
        BeginOnlineRematchLobbyTransition(
            transitionId,
            rematchSeed,
            broadcastTransition: false);
        return true;
    }

    private void BeginOnlineRematchLobbyTransition(
        int transitionId,
        int rematchSeed,
        bool broadcastTransition)
    {
        lastAppliedRematchLobbyTransitionId = transitionId;
        lastAppliedRematchLobbySeed = rematchSeed;
        // The original online entry has its own seed handshake. A retained-session rematch skips
        // that handshake, so the host-generated seed travels atomically with this cached scene
        // transition and is applied before either peer can initialize or simulate the new lobby.
        InitializeWithSeed(rematchSeed);
        isWaitingForOpponent = false;
        localPlayerReadyForGameplay = false;
        remotePlayerReadyForGameplay = false;
        gameplayReadyPeerSlots.Clear();
        pendingGameplayReadyBySlot.Clear();
        pendingGameplayReadyTransitionBySlot.Clear();
        localGameplayReadyContext = GameplayReadyContext.None;
        remoteGameplayReadyContext = GameplayReadyContext.None;
        pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
        localGameplayReadyTransitionId = 0;
        remoteGameplayReadyTransitionId = 0;
        pendingRemoteGameplayReadyTransitionId = 0;
        pendingOpponentShopTransition = false;
        pendingOpponentShopTransitionId = 0;
        ClearPendingStageSelect();

        BeginTrackedOnlineTransition(transitionId);
        SetStage(-1);
        // Preserve the lobby index/signature for the transition without displaying its persistent
        // geometry in End during the half-second screen-cover animation.
        ClearStages();
        if (MainMenuScreen != null)
        {
            MainMenuScreen.SetActive(false);
        }
        SetNetworkInfoVisible(true);

        if (broadcastTransition)
        {
            MatchMessageManager.Instance?.SendRematchLobbyTransition(transitionId, rematchSeed);
        }

        sceneManager.LoadScene("MainMenu");
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

    private int ResolveSpawnIndexForSlot(int playerSlot, int spawnCount)
    {
        if (spawnCount <= 0)
        {
            return 0;
        }

        int slotIndex = Mathf.Clamp(playerSlot, 0, spawnCount - 1);
        bool isSparseDuel =
            activeOnlineRoster != null
            && activeOnlineRoster.PlayerCount < playerCount
            && currentStageIndex >= 0
            && currentStageIndex < stages.Count
            && stages[currentStageIndex] != null
            && stages[currentStageIndex].stageType == StageType.Duel;
        if (!isSparseDuel)
        {
            return slotIndex;
        }

        // Duel assets intentionally repeat [left, right, left, right]. Assign sparse fighters by
        // roster order so P1+P3 do not both spawn on the duplicated left position.
        int participantIndex = 0;
        for (int slot = 0; slot < playerSlot; slot++)
        {
            if (activeOnlineRoster.TryGetSteamIdForSlot(slot, out Steamworks.SteamId _))
            {
                participantIndex++;
            }
        }

        return Mathf.Clamp(participantIndex, 0, spawnCount - 1);
    }

    public PlayerController GetPlayerByPID(int pID)
    {
        if (pID == 0)
        {
            return playerNPCs.Count > 0 ? playerNPCs[0] : null;
        }

        int slot = pID - 1;
        if (slot < 0 || slot >= players.Length || players[slot] == null)
        {
            return null;
        }

        if (activeOnlineRoster != null && !IsPlayerSlotConnected(slot))
        {
            return null;
        }

        return players[slot];
    }

    public StageDataSO GetCurrentStageDataSO()
    {
        switch (currentStageIndex)
        {
            case -1:
                return lobbySO;
            case -2:
                return TutorialSO;
            case -3:
                return trainingGroundsSO;
            case -4:
                return soloLobbySO;
            default:
                return stages[currentStageIndex];
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
        hasPendingHostGameplayRngRestore = false;
        pendingHostGameplayRngRestoreState = 0;
        pendingHostGameplayRngRestoreCallCount = -1;
        Debug.Log($"[SEED] Initialized RNG with seed: {seed}");
    }

    private int GenerateFreshOnlineRematchSeed()
    {
        int seed = UnityEngine.Random.Range(1, int.MaxValue);
        if (seed == randomSeed)
        {
            seed = seed < int.MaxValue - 1 ? seed + 1 : 1;
        }
        return seed;
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

    public int GetOnlineShopChoiceRandom(
        int ownerPid,
        int activationCount,
        int choiceIndex,
        int maxValue,
        int rollGeneration = 0)
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
            state ^= 0x165667B1u * (uint)Mathf.Max(0, rollGeneration);

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
            // The host clears before broadcasting the authoritative pre-transition snapshot. Guests
            // also run the same idempotent reset in BeginOnlineShopTransition below, so packet order
            // cannot leave one peer carrying the completed round's Chaos loadout into the Shop.
            PrepareOnlineChaosShopState();
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

        PrepareOnlineChaosShopState();
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
    /// <param name="endedByDisconnect">
    /// True when the match was decided by everyone else dropping rather than being played out.
    /// Keeps the "finish a match" achievements off that path: a peer quitting shouldn't satisfy
    /// "play a full match", and it would otherwise be farmable with a friend who joins and leaves.
    /// </param>
    /// <summary>
    /// Awards the "finished an online match" achievements, from EITHER end-of-match path.
    /// GameEnd only runs on a client whose own sim plays the game-over transition out, which in
    /// practice is the host: a guest is driven straight to the End screen by the host's
    /// authoritative End packet (BeginOnlineEndTransition -> ApplyOnlineEndWinner) and never calls
    /// GameEnd at all. Awarding only there is why guests never earned Squad Goals
    /// </summary>
    private void TryAwardOnlineMatchCompletionAchievement(bool endedByDisconnect, string source)
    {
        // Every input to the decision, because a missed unlock otherwise leaves NO trace:
        // SteamAchievements.TryTrigger returns silently when Steam has not handed over the user's
        // stats yet, so an absent "[Achievements] ..." line cannot distinguish "never asked" from
        // "asked and it was refused". This says which, and which path asked.
        if (SteamManager.DebugToolsEnabled)
        {
            Debug.Log($"[Achievements] Match-end check from {source}. origin={SteamLobbyManager.ActiveMatchOrigin} "
                + $"online={isOnlineMatchActive} endedByDisconnect={endedByDisconnect} realFrame={SimGuards.IsRealFrame()}.");
        }

        if (!isOnlineMatchActive || endedByDisconnect || !SimGuards.IsRealFrame())
        {
            return;
        }

        switch (SteamLobbyManager.ActiveMatchOrigin)
        {
            case SteamLobbyManager.OnlineMatchOrigin.Friends:
                SteamAchievements.Unlock(SteamAchievements.FirstFriendsMatch);
                break;
            case SteamLobbyManager.OnlineMatchOrigin.Matchmaking:
                SteamAchievements.Unlock(SteamAchievements.FirstMatchmakingMatch);
                break;
        }
    }

    public void GameEnd(bool endedByDisconnect = false)
    {
        if (!isSaved)
        {
            dataManager.SaveMatch();
            isSaved = true;
        }

        // Awarded per machine, not per slot: every player who saw the match through earns it
        // on their own account, which is what "play a full match with friends" describes.
        TryAwardOnlineMatchCompletionAchievement(endedByDisconnect, "GameEnd");

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
        HidePersistentMatchWorldForEndScene();
        sceneManager.LoadScene("End");

        //play a new end song
        //BGM_Manager.Instance.StartAndPlaySong();
    }

    private void BeginOnlineEndTransition(int transitionId, int winnerPid)
    {
        OnlineEndOptionsEpoch = transitionId;
        preparedOnlineRematchEpoch = -1;
        rematchPreparationStarted = false;

        // Reliable End packets can be duplicated while the screen-cover tween still leaves the
        // active scene reported as Gameplay. Track this target explicitly: a different scene
        // transition can legitimately own the same active id when a disconnect decides the match.
        if (requestedOnlineEndLoadTransitionId == transitionId)
        {
            ApplyOnlineEndWinner(winnerPid);
            return;
        }

        ApplyOnlineEndWinner(winnerPid);

        // This is a guest being told the match was played out to a result, which is the only
        // end-of-match signal it gets it never runs GameEnd. A disconnect-decided match does not
        // arrive here: that is adjudicated locally and calls GameEnd(endedByDisconnect: true).
        TryAwardOnlineMatchCompletionAchievement(false, "OnlineEndTransition");

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
        HidePersistentMatchWorldForEndScene();
        requestedOnlineEndLoadTransitionId = transitionId;
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

    // Hide every player's character visuals on the End screen. Players are DontDestroyOnLoad, so they
    // and their child renderers ride into the End scene; the winner is shown via GameEndScreen's
    // separate winnerImage sprite, so the real characters (winner AND losers) should be invisible --
    // a carried-over loser was still partly on-camera. Disable the RENDERERS (not the GameObjects) so
    // each player's PlayerInput stays alive for GameEndScreen's per-player option navigation. A
    // retained rematch restores these exact states only after its destination scene has loaded.
    private void HideAllPlayerCharacters()
    {
        // Include NPCs and any still-loaded controller that has not made it into players[] (for
        // example a late-created online object). The End scene's winner art has no PlayerController,
        // so this cannot hide the authored winner presentation.
        PlayerController[] loadedPlayers = FindObjectsByType<PlayerController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < loadedPlayers.Length; i++)
        {
            PlayerController player = loadedPlayers[i];
            if (player == null)
            {
                continue;
            }

            Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer playerRenderer = renderers[r];
                if (playerRenderer != null)
                {
                    if (!endScreenRendererVisibility.ContainsKey(playerRenderer))
                    {
                        endScreenRendererVisibility[playerRenderer] = playerRenderer.enabled;
                    }
                    playerRenderer.enabled = false;
                }
            }
        }
    }

    private void HidePersistentMatchWorldForEndScene()
    {
        ClearStages();
        HideAllPlayerCharacters();
    }

    /// <summary>
    /// Reasserts the End scene's presentation without touching player input objects. Kept
    /// idempotent so both the persistent manager's scene callback and GameEndScreen.Start can call
    /// it regardless of Unity callback ordering.
    /// </summary>
    public void EnforceEndScenePresentation()
    {
        if (SceneManager.GetActiveScene().name != "End")
        {
            return;
        }

        isRunning = false;
        HidePersistentMatchWorldForEndScene();
        try
        {
            HidePersistentUiForEndScene();
        }
        catch (Exception exception)
        {
            // UI teardown must not abort the online End ready handshake or the authored winner UI.
            Debug.LogException(exception);
        }
    }

    private void RestorePlayerRenderersAfterEnd()
    {
        foreach (KeyValuePair<Renderer, bool> rendererState in endScreenRendererVisibility)
        {
            if (rendererState.Key != null)
            {
                rendererState.Key.enabled = rendererState.Value;
            }
        }

        endScreenRendererVisibility.Clear();
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
        List<PlayerController> activePlayers = new List<PlayerController>(playerCount);
        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] != null && IsPlayerSlotConnected(i))
            {
                activePlayers.Add(players[i]);
            }
        }
        return activePlayers.ToArray();
    }

    public PlayerController[] GetMatchParticipantControllers()
    {
        List<PlayerController> matchPlayers = new List<PlayerController>(playerCount);
        for (int slot = 0; slot < playerCount; slot++)
        {
            if (players[slot] == null)
            {
                continue;
            }

            // Preserve a real peer's statistics even if they disconnected, while excluding the
            // inert objects used only to hold sparse P-number gaps in rollback state.
            if (activeOnlineRoster == null
                || activeOnlineRoster.TryGetSteamIdForSlot(slot, out Steamworks.SteamId _))
            {
                matchPlayers.Add(players[slot]);
            }
        }

        return matchPlayers.ToArray();
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
        // MainMenu is the first-stage lobby for a new match. Rebuild here (not only when End is
        // reset) so local joins or online peer drops that happened while choosing starters use the
        // correct Duel/General pool at the exact moment the roster commits to Gameplay.
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            FillGameStages();
        }

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
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            FillGameStages();
        }

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        InputSystem.onDeviceChange += OnInputDeviceChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        // InputSystem.onDeviceChange is static, so a missed unsubscribe keeps calling into a
        // GameManager that ExecuteOrder66 already destroyed.
        InputSystem.onDeviceChange -= OnInputDeviceChanged;
    }

    /// <summary>
    /// Online pairs EVERY connected device to the one local player (ConfigureOnlineLocalPlayerInput),
    /// but that is a snapshot taken when the match starts -- a controller plugged in afterwards was
    /// never paired, so only the keyboard kept working. Re-pair whenever a usable device appears.
    /// Deferred to Update rather than done here because this fires from inside the input system's
    /// own update, and re-pairing devices mid-callback would mutate the user's device list while it
    /// is being enumerated.
    /// </summary>
    private void OnInputDeviceChanged(InputDevice device, InputDeviceChange change)
    {
        if (!isOnlineMatchActive)
        {
            return;
        }

        if (change != InputDeviceChange.Added
            && change != InputDeviceChange.Reconnected
            && change != InputDeviceChange.Enabled)
        {
            return;
        }

        if (!InputDeviceManager.IsValidInput(device))
        {
            return;
        }

        onlineInputDevicesDirty = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Hardware capture is wall-clock state, not rollback state. Scene/timeline changes start
        // a fresh input epoch, so no menu/transition edge may spill into the destination scene.
        ResetLocalOnlineInputCaptureForNewTimeline();
        if (isOnlineMatchActive)
        {
            SetLocalOnlineInputCaptureSuppressed(true);
        }

        bool isEndScene = scene.name == "End";
        if (isEndScene)
        {
            // Do this before camera/reference/projectile setup: the real players and stage roots are
            // persistent, and any exception in generic arrival work must not leave them on End.
            EnforceEndScenePresentation();
        }
        else
        {
            if (rematchPreparationStarted)
            {
                // There is no rendered frame inside this sceneLoaded callback, so restoration here
                // cannot flash the losing characters over the End screen. Restore before respawn so
                // SpawnPlayer can correctly make every surviving fighter's root sprite visible even
                // if that fighter was dead (and therefore hidden) when the match ended.
                RestorePlayerRenderersAfterEnd();
            }
            ResetPlayers();
        }
        //Debug.Log($"Scene loaded: {scene.name}");

        // Must run before anything in the new scene can consume the gameplay RNG: the old scene's
        // sim is finished at this point, the new round's sim has not started. See the comment on
        // StashHostGameplayRngFromStageSelect.
        ApplyPendingHostGameplayRngRestoreIfAvailable();

        RefreshSceneObjectReferences();
        HitboxManager.Instance.GetActiveCamera();
        ProjectileManager.Instance.DeleteAllProjectiles();

        if (isEndScene)
        {
            endInputEnabled = false;
            rematchPreparationStarted = false;
            preparedOnlineRematchEpoch = -1;
            if (!isOnlineMatchActive)
            {
                OnlineEndOptionsEpoch = 0;
            }

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
        bool haveRoundCount = true;
        if (dataManager == null)
        {
            dataManager = DataManager.Instance;
        }
        if (dataManager != null)
        {
            roundsPlayed = dataManager.totalRoundsPlayed;

        }
        else if (isOnlineMatchActive)
        {
            // ExecuteOrder66 destroys DataManager, so it is legitimately absent on some transitions.
            haveRoundCount = false;
        }
        else
        {
            roundsPlayed = 1;
        }

        if (haveRoundCount)
        {
            ramNeededToWinRound = (ushort)( baseRamNeeddedtowin + 100 * roundsPlayed);
        }

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

        // For an OFFLINE rematch lobby. The lobby map and machines live under the persistent game
        // manager, so loading MainMenu does not recreate them; explicitly reopen the starter
        // machines and onboarding state for the retained local players.
        if (!isOnlineMatchActive && scene.name == "MainMenu" && rematchPreparationStarted)
        {
            SetStage(-1);
            InitializeRematchLobbySceneState();
            FindAllFloppyDisks();
            if (tempUI != null)
            {
                tempUI.gameObject.SetActive(true);
            }
            if (MainMenuScreen != null)
            {
                MainMenuScreen.SetActive(false);
            }
            if (playerWinText != null)
            {
                playerWinText.enabled = false;
            }
            if (roundEndedText != null)
            {
                roundEndedText.enabled = true;
            }
            isRunning = true;
            rematchPreparationStarted = false;
        }

        // For an ONLINE rematch lobby. This is a warm return: the player objects, sparse roster,
        // Steam session, and input ownership survive, while the deterministic match baseline and
        // starter-selection machines are rebuilt before the normal lobby simulation resumes.
        if (isOnlineMatchActive && scene.name == "MainMenu" && isTransitioning)
        {
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
            onlineRoundAdvanceApplied = false;
            playersChosenSpell = false;
            localPlayerReadyForGameplay = false;
            remotePlayerReadyForGameplay = false;
            gameplayReadyPeerSlots.Clear();
            pendingGameplayReadyBySlot.Clear();
            pendingGameplayReadyTransitionBySlot.Clear();
            localGameplayReadyContext = GameplayReadyContext.None;
            remoteGameplayReadyContext = GameplayReadyContext.None;
            pendingRemoteGameplayReadyContext = GameplayReadyContext.None;
            localGameplayReadyTransitionId = 0;
            remoteGameplayReadyTransitionId = 0;
            pendingRemoteGameplayReadyTransitionId = 0;
            ClearPendingStageSelect();
            localSceneTransitionReady = false;
            frameNumber = 0;
            localPlayerInput = 5;
            syncedInput = new ulong[Mathf.Max(2, IsRosterBasedOnlineMatch() ? playerCount : 2)];
            for (int i = 0; i < syncedInput.Length; i++)
            {
                syncedInput[i] = 5UL;
            }
            timeoutFrames = 0;
            isWaitingForOpponent = false;

            tempUI?.CloseAllCodeModePrompts();
            if (tempUI != null)
            {
                tempUI.gameObject.SetActive(false);
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

            StartCoroutine(FinalizeOnlineRematchLobbyArrival(activeOnlineTransitionId));
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

            ProjectileManager.Instance.InitializeAllProjectiles();
            // SpawnPlayer's OnStart pass rebuilds The Jokah's derived spell/projectile copies.
            // Initialize the inventory pool first so it cannot immediately destroy those copies.
            ResetPlayers();
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
            ProjectileManager.Instance.InitializeAllProjectiles();
            // Keep the final pool topology (including Jokah copies) in the frame-zero snapshot.
            ResetPlayers();
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

    private void PrepareOnlineChaosShopState()
    {
        if (!isOnlineMatchActive || gamemode != Gamemode.Chaos)
        {
            return;
        }

        for (int i = 0; i < playerCount; i++)
        {
            PlayerController player = players[i];
            if (player == null)
            {
                continue;
            }

            player.roundRam = 0;
            player.storedKillBonus = 0;
            player.chosenSpell = false;

            // This method can run once from the host's RoundEnd and again while entering the scene.
            // Avoid repeated projectile-pool/UI rebuilds while keeping the reset idempotent.
            if (player.spellList != null && player.spellList.Count > 0)
            {
                player.ClearSpellList();
            }
        }
    }

    private IEnumerator FinalizeOnlineRematchLobbyArrival(int transitionId)
    {
        // sceneLoaded runs before Start on objects created with MainMenu. Wait one frame so the
        // new OnboardManager/GambaMachine Start methods finish, then reapply the same lobby reset
        // used by initial online setup and save that final state as rollback frame zero.
        yield return null;

        if (!isOnlineMatchActive
            || !isTransitioning
            || activeOnlineTransitionId != transitionId
            || SceneManager.GetActiveScene().name != "MainMenu")
        {
            yield break;
        }

        InitializeRematchLobbySceneState();
        FindAllFloppyDisks();
        ProjectileManager.Instance.InitializeAllProjectiles();
        if (RollbackManager.Instance != null)
        {
            RollbackManager.Instance.SaveState();
        }

        if (MainMenuScreen != null)
        {
            MainMenuScreen.SetActive(false);
        }
        if (playerWinText != null)
        {
            playerWinText.enabled = false;
        }
        if (roundEndedText != null)
        {
            roundEndedText.enabled = true;
        }
        SetNetworkInfoVisible(true);
        isRunning = true;
        localSceneTransitionReady = true;
        sceneReadyPeerSlots.Add(localPlayerIndex);

        // Announce before applying a peer-ready packet that may already be pending. Applying the
        // pending packet can complete this transition and clear activeOnlineTransitionId.
        MatchMessageManager.Instance?.SendSceneTransitionReadySignal(transitionId);
        ApplyPendingSceneTransitionReadyIfAvailable();
        CheckSceneTransitionReady();
    }

    private void InitializeRematchLobbySceneState()
    {
        onboardManager = FindFirstObjectByType<OnboardManager>();
        if (onboardManager != null)
        {
            onboardManager.ResetOnboarding();
        }

        foreach (GameObject gambaGO in GetValidGambaObjects(refreshIfNeeded: true))
        {
            if (gambaGO == null)
            {
                continue;
            }

            GambaMachine gamba = gambaGO.GetComponent<GambaMachine>();
            if (gamba == null)
            {
                continue;
            }

            int ownerSlot = gamba.ownerPID - 1;
            bool hasActiveOwner = ownerSlot >= 0
                && ownerSlot < playerCount
                && players[ownerSlot] != null
                && IsPlayerSlotConnected(ownerSlot);
            gamba.ownerPlayer = hasActiveOwner ? players[ownerSlot] : null;
            gamba.ResetLobbyState();
            if (!hasActiveOwner)
            {
                gamba.activatedCount = 3;
                gamba.isActive = false;
                gamba.ApplyVisualState();
            }
        }

        foreach (SpellCode_Gate gate in gates)
        {
            if (gate == null)
            {
                continue;
            }

            gate.isOpen = false;
            gate.SetOpen(false);
        }

        if (goDoorPrefab != null)
        {
            goDoorPrefab.isPrimed = true;
            goDoorPrefab.CheckOpenDoor();
        }
    }

    public void ClearStages()
    {
        for (int i = 0; i < tempMapGOs.Count; i++)
        {
            if (tempMapGOs[i] != null)
            {
                tempMapGOs[i].SetActive(false);
            }
        }
        if (lobbyMapGO != null) lobbyMapGO.SetActive(false);
        if (tutorialMapGO != null) tutorialMapGO.SetActive(false);
        if (trainingGroundsGO != null) trainingGroundsGO.SetActive(false);
        if (soloLobbyGO != null) soloLobbyGO.SetActive(false);
    }

    private void HidePersistentUiForEndScene()
    {
        if (tempUI != null)
        {
            // Close BEFORE deactivating. An online match keeps simulating at timeScale 1 with the
            // local pause menu open, so it can reach the End screen while paused — and the menu
            // panels live under pfb_GameManager/Pause, NOT under TempUI, so deactivating TempUI
            // only kills Pause.Update() (the sole way to close the menu) and strands the panels
            // on screen. See Pause.CanOpenPauseMenu, which blocks opening one here in the first place.
            Pause pauseMenu = tempUI.gameObject.GetComponent<Pause>();
            if (pauseMenu != null && pauseMenu.paused)
            {
                pauseMenu.Resume();
            }

            tempUI.gameObject.SetActive(false);
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
            bool hasActiveOwner = gamba.ownerPID > 0
                && gamba.ownerPID <= playerCount
                && players[gamba.ownerPID - 1] != null
                && IsPlayerSlotConnected(gamba.ownerPID - 1);
            bool ownerCanUseShop = hasActiveOwner
                && players[gamba.ownerPID - 1].spellList != null
                && players[gamba.ownerPID - 1].spellList.Count < 6
                && !players[gamba.ownerPID - 1].chosenSpell;

            gamba.ownerPlayer = hasActiveOwner ? players[gamba.ownerPID - 1] : null;
            gamba.chaosRollGeneration = 0;
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
            tempUI.OpenTutorialPromptMenu();
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
            }, StringComparer.Ordinal)
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
                // Mode changes deterministic Gamba, pickup, and round-transition rules. Keep it in
                // every managed snapshot so a late joiner cannot deserialize Chaos state while
                // still executing stale Normal rules.
                bw.Write((byte)gamemode);
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
                bw.Write(onlineRoundAdvanceApplied);

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
                            bw.Write(0);
                            bw.Write(false);
                            continue;
                        }
                        GambaMachine gamba = gambaGO.GetComponent<GambaMachine>();
                        // Write defaults if somehow null, so byte count stays consistent
                        bw.Write(gamba != null ? gamba.activatedCount : 0);
                        bw.Write(gamba != null ? gamba.resetTimer : (byte)0);
                        bw.Write(gamba != null ? gamba.GetStartingSpellPos() : 0);
                        bw.Write(gamba != null ? gamba.chaosRollGeneration : 0);
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
            bw.Write((byte)gamemode);
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
            bw.Write(CurrentTotalRoundsPlayed);
            bw.Write(onlineRoundAdvanceApplied);

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
            bw.Write((byte)gamemode);
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
            bw.Write(CurrentTotalRoundsPlayed);
            bw.Write(onlineRoundAdvanceApplied);

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

        bw.Write(playerCount);
        for (int i = 0; i < playerCount; i++)
        {
            bw.Write(players[i] != null && players[i].chosenSpell);
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
            bw.Write(gamba != null ? gamba.chaosRollGeneration : 0);
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

                byte savedGamemodeValue = br.ReadByte();
                if (!Enum.IsDefined(typeof(Gamemode), (int)savedGamemodeValue))
                {
                    throw new InvalidDataException($"Snapshot contains unknown game mode value {savedGamemodeValue}.");
                }

                Gamemode savedGamemode = (Gamemode)savedGamemodeValue;
                string savedGameModeId = GetOnlineGameModeId(savedGamemode);
                if (gamemode != savedGamemode
                    || !string.Equals(ActiveOnlineGameMode.Id, savedGameModeId, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyOnlineGameMode(savedGameModeId);
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
                if (SceneManager.GetActiveScene().name != "End" && savedStageIndex != currentStageIndex)
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
                onlineRoundAdvanceApplied = br.ReadBoolean();
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
                        int chaosRollGeneration = br.ReadInt32();
                        bool isActive = br.ReadBoolean();
                        if (i < gambas.Count)
                        {
                            GambaMachine gamba = gambas[i].GetComponent<GambaMachine>();
                            if (gamba != null)
                            {
                                gamba.activatedCount = activatedCount;
                                gamba.resetTimer = resetTimer;
                                gamba.SetStartingSpellPos(startingSpellPos);
                                gamba.chaosRollGeneration = chaosRollGeneration;
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

        // A sparse roster's playerCount is its serialized slot span (P1+P3 => 3), so stage rules
        // must use the actual number of peers rather than accidentally treating an empty slot as a
        // third fighter.
        int participantCount = activeOnlineRoster != null
            ? CountConnectedPlayers()
            : playerCount;
        switch (participantCount)
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

    private bool TryGetOnlineRosterSlotCount(OnlineMatchRoster roster, out int slotCount)
    {
        slotCount = 0;
        if (roster?.Peers == null
            || roster.PlayerCount < 2
            || players == null
            || roster.PlayerCount > players.Length
            || roster.LocalPlayerSlot < 0
            || !roster.HostSteamId.IsValid)
        {
            return false;
        }

        HashSet<int> usedSlots = new HashSet<int>();
        HashSet<ulong> usedSteamIds = new HashSet<ulong>();
        bool foundLocalSlot = false;
        bool foundHostInSlotZero = false;
        int highestSlot = -1;

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer == null
                || !peer.SteamId.IsValid
                || peer.PlayerSlot < 0
                || peer.PlayerSlot >= players.Length
                || !usedSlots.Add(peer.PlayerSlot)
                || !usedSteamIds.Add(peer.SteamId.Value))
            {
                return false;
            }

            highestSlot = Mathf.Max(highestSlot, peer.PlayerSlot);
            foundLocalSlot |= peer.PlayerSlot == roster.LocalPlayerSlot;
            foundHostInSlotZero |=
                peer.PlayerSlot == 0
                && peer.SteamId.Value == roster.HostSteamId.Value;
        }

        if (!foundLocalSlot || !foundHostInSlotZero)
        {
            return false;
        }

        slotCount = Mathf.Max(2, highestSlot + 1);
        return slotCount <= players.Length;
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

    private void ApplyOnlineRosterSlotOccupancy(
        OnlineMatchRoster roster,
        int slotCount,
        bool preserveExistingDisconnects,
        HashSet<int> newlyOccupiedSlots)
    {
        int boundedSlotCount = Mathf.Min(slotCount, players.Length);
        for (int slot = 0; slot < boundedSlotCount; slot++)
        {
            bool occupied = roster != null
                && roster.TryGetSteamIdForSlot(slot, out Steamworks.SteamId _);
            bool newlyOccupied = newlyOccupiedSlots != null && newlyOccupiedSlots.Contains(slot);
            bool alreadyDisconnected =
                onlineDisconnectedSlots.Contains(slot)
                || (players[slot] != null && !players[slot].isConnected);

            if (!occupied)
            {
                onlineDisconnectedSlots.Add(slot);
                ApplyDisconnectedPlayerSlot(slot, cleanupProjectiles: false);
                continue;
            }

            if (!preserveExistingDisconnects
                || newlyOccupied
                || !alreadyDisconnected)
            {
                onlineDisconnectedSlots.Remove(slot);
                if (players[slot] != null)
                {
                    players[slot].isConnected = true;
                }
            }
            else
            {
                // A peer that disconnected earlier stays eliminated when an unrelated player joins.
                onlineDisconnectedSlots.Add(slot);
                ApplyDisconnectedPlayerSlot(slot, cleanupProjectiles: false);
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

        int participantCount = activeOnlineRoster != null
            ? activeOnlineRoster.PlayerCount
            : playerCount;
        return participantCount < players.Length;
    }

    public bool CanStartOrRefreshOnlineLobby(OnlineMatchRoster roster)
    {
        if (!TryGetOnlineRosterSlotCount(roster, out int _))
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
