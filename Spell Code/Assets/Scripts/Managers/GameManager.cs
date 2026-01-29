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

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameObject MainMenuScreen;

    public GameObject playerPrefab;
    public PlayerController[] players = new PlayerController[4];
    public int playerCount = 0;

    public bool isRunning;
    public bool isSaved;
    private DataManager dataManager;
    public TempSpellDisplay[] tempSpellDisplays = new TempSpellDisplay[4];
    public TempUIScript tempUI;
    public StageDataSO[] stages;
    public StageDataSO lobbySO;
    public int currentStageIndex = 0;

    public List<GameObject> tempMapGOs = new List<GameObject>();
    public GameObject lobbyMapGO;

    [HideInInspector]
    public ShopManager shopManager;

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

    //main menu stuff
    public bool playersChosenSpell;
    public Image p1_spellCard;
    public Image p2_spellCard;
    public Image p3_spellCard;
    public Image p4_spellCard;

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

    // Online Match State
    public int frameNumber { get; private set; } = 0;
    public bool isOnlineMatchActive = false;
    private ulong localPlayerInput = 0;
    private ulong[] syncedInput = new ulong[2] { 0, 0 };
    public int localPlayerIndex = 0;
    public int remotePlayerIndex = 1;
    private int timeoutFrames = 0;

    // Online lobby state tracking
    private bool localPlayerReadyForGameplay = false;
    private bool remotePlayerReadyForGameplay = false;
    [HideInInspector]
    public int p1_shopIndex = 0;
    [HideInInspector]
    public int p2_shopIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

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

        if (onlineMenuUI != null)
        {
            onlineMenuUI.SetActive(false);
        }

        SetStage(-1);
    }

    void Update()
    {
        if (isOnlineMatchActive)
        {
            cachedLocalInput = GatherInputForOnline();
        }

        if (!isOnlineMatchActive)
        {
            gameObject.GetComponent<PlayerInputManager>().enabled = (SceneManager.GetActiveScene().name == "MainMenu");
        }
        else
        {
            if (playerInputManager != null && playerInputManager.enabled)
            {
                playerInputManager.enabled = false;
            }
        }

        if (UnityEngine.Input.GetKeyDown(KeyCode.BackQuote))
        {
            BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
        }

        if (!isOnlineMatchActive)
        {
            if (UnityEngine.Input.GetKeyDown(toggleOnlineMenuKey))
            {
                if (onlineMenuUI != null)
                {
                    bool isOnlineMenuVisible = !onlineMenuUI.activeSelf;
                    onlineMenuUI.SetActive(isOnlineMenuVisible);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (isTransitioning) return;

        // ONLINE LOBBY WAIT STATE
        if (isOnlineMatchActive && isWaitingForOpponent)
        {
            float waitTime = UnityEngine.Time.unscaledTime - lobbyWaitStartTime;
            if (waitTime > LOBBY_TIMEOUT)
            {
                Debug.LogError("Lobby timeout - opponent didn't join in time");
                StopMatch("Opponent failed to connect");
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
            RunOnlineFrame();
        }
        else
        {
            RunFrame();
        }

        if (!isOnlineMatchActive || (RollbackManager.Instance != null && !RollbackManager.Instance.isRollbackFrame))
        {
            AnimationManager.Instance.RenderGameState();
        }
    }

    private ulong GatherInputForOnline()
    {
        if (players[localPlayerIndex] != null && players[localPlayerIndex].inputs.IsActive)
        {
            var upVal = players[localPlayerIndex].inputs.UpAction?.ReadValue<float>() ?? 0f;
            var downVal = players[localPlayerIndex].inputs.DownAction?.ReadValue<float>() ?? 0f;
            var leftVal = players[localPlayerIndex].inputs.LeftAction?.ReadValue<float>() ?? 0f;
            var rightVal = players[localPlayerIndex].inputs.RightAction?.ReadValue<float>() ?? 0f;

            if (upVal > 0.1f || downVal > 0.1f || leftVal > 0.1f || rightVal > 0.1f)
            {
                return players[localPlayerIndex].GetInputs();
            }
        }
        return GatherRawInput();
    }

    private ulong GatherRawInput()
    {
        bool up = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow);
        bool down = UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow);
        bool left = UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow);
        bool right = UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow);

        bool codeNow = UnityEngine.Input.GetKey(KeyCode.R);
        bool jumpNow = UnityEngine.Input.GetKey(KeyCode.T);

        ButtonState codeState = GetButtonStateHelper(codePrevFrame, codeNow);
        ButtonState jumpState = GetButtonStateHelper(jumpPrevFrame, jumpNow);

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

    public void StartOnlineMatch(int localIndex, int remoteIndex, Steamworks.SteamId opponentId)
    {
        Debug.Log("Starting Online Match...");
        if (RollbackManager.Instance == null)
        {
            Debug.LogError("Cannot start online match: RollbackManager not found!");
            return;
        }
        if (!opponentId.IsValid)
        {
            Debug.LogError("Cannot start online match: Invalid Opponent SteamId provided!");
            return;
        }

        // Hide online menu immediately
        if (onlineMenuUI != null)
        {
            onlineMenuUI.SetActive(false);
            Debug.Log("Online menu UI hidden");
        }

        isOnlineMatchActive = false;
        isWaitingForOpponent = false;
        opponentIsReady = false;
        isRunning = false;
        isTransitioning = false;
        localPlayerReadyForGameplay = false;
        remotePlayerReadyForGameplay = false;

        if (playerInputManager != null)
        {
            playerInputManager.DisableJoining();
            playerInputManager.enabled = false;
            Debug.Log("PlayerInputManager disabled");
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
            Debug.LogError("Player Prefab is not assigned in GameManager Inspector!");
            return;
        }

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

        RollbackManager.Instance.Init(opponentId.Value);

        if (MatchMessageManager.Instance != null)
        {
            MatchMessageManager.Instance.StartMatch(opponentId);
            MatchMessageManager.Instance.SendReadySignal();
        }
        else
        {
            Debug.LogError("MatchMessageManager not found during StartOnlineMatch!");
        }

        isOnlineMatchActive = true;
        isWaitingForOpponent = true;

        ProjectileManager.Instance.InitializeAllProjectiles();

        SetStage(-1); // Lobby stage
        ResetPlayers();

        isRunning = true;

        Debug.Log($"Entered Online Lobby - Waiting for opponent... LocalPlayer={localPlayerIndex}");
    }

    public void OnPacketReceived()
    {
        lastPacketReceivedTime = UnityEngine.Time.unscaledTime;
    }

    private bool CheckNetworkHealth()
    {
        if (isWaitingForOpponent)
            return true;

        if (lastPacketReceivedTime == 0f)
        {
            if (UnityEngine.Time.unscaledTime - lobbyWaitStartTime > 15f)
            {
                Debug.LogError("Network timeout - no packets received after 15 seconds");
                return false;
            }
            return true;
        }

        float timeSinceLastPacket = UnityEngine.Time.unscaledTime - lastPacketReceivedTime;

        if (timeSinceLastPacket > NETWORK_TIMEOUT)
        {
            Debug.LogError($"Network timeout - no packets for {timeSinceLastPacket:F1} seconds");
            return false;
        }

        if (timeSinceLastPacket > 3f && Mathf.FloorToInt(timeSinceLastPacket) % 1 == 0)
        {
            Debug.LogWarning($"Network lag - no packets for {timeSinceLastPacket:F1} seconds");
        }

        return true;
    }

    public void OnOpponentReady()
    {
        Debug.Log("Received opponent ready signal");

        if (!isOnlineMatchActive)
        {
            Debug.LogWarning("Received ready signal but not in online match state - ignoring");
            return;
        }

        if (!isWaitingForOpponent)
        {
            Debug.LogWarning("Received ready signal but already started - ignoring");
            return;
        }

        opponentIsReady = true;
        StartLobbySimulation();
    }

    private void StartLobbySimulation()
    {
        Debug.Log("Starting Lobby Simulation!");

        if (!isWaitingForOpponent)
        {
            Debug.LogWarning("StartLobbySimulation called but not waiting - aborting");
            return;
        }

        isWaitingForOpponent = false;
        lastPacketReceivedTime = UnityEngine.Time.unscaledTime;

        if (MatchMessageManager.Instance != null)
        {
            MatchMessageManager.Instance.SendMatchStartConfirm();
        }

        ProjectileManager.Instance.InitializeAllProjectiles();
        frameNumber = 0;
        isRunning = true;

        Debug.Log("Lobby simulation started - both players in MainMenu lobby");
    }

    // Send lobby ready signal
    public void SendLobbyReadyForGameplay()
    {
        if (!isOnlineMatchActive || MatchMessageManager.Instance == null)
            return;

        localPlayerReadyForGameplay = true;
        Debug.Log("Local player ready for gameplay transition - sending signal");

        // Send via MatchMessageManager
        MatchMessageManager.Instance.SendLobbyReadySignal();

        CheckBothPlayersReadyForGameplay();
    }

    // Receive lobby ready signal
    public void OnOpponentReadyForGameplay()
    {
        Debug.Log("Opponent is ready for gameplay transition");
        remotePlayerReadyForGameplay = true;
        CheckBothPlayersReadyForGameplay();
    }

    // Check if both players are ready to transition
    private void CheckBothPlayersReadyForGameplay()
    {
        if (localPlayerReadyForGameplay && remotePlayerReadyForGameplay)
        {
            Debug.Log("Both players ready - transitioning to Gameplay");
            isTransitioning = true;
            LoadRandomGameplayStage();
        }
    }

    public void StopMatch(string reason = "Match Ended")
    {
        Debug.Log($"Stopping Match: {reason}");

        isRunning = false;

        if (isOnlineMatchActive)
        {
            Debug.Log("Cleaning up online match state...");

            if (RollbackManager.Instance != null)
            {
                RollbackManager.Instance.Disconnect();
            }

            if (MatchMessageManager.Instance != null)
            {
                MatchMessageManager.Instance.StopMatch();
            }

            isOnlineMatchActive = false;
            isWaitingForOpponent = false;
            opponentIsReady = false;
            isTransitioning = false;
            localPlayerReadyForGameplay = false;
            remotePlayerReadyForGameplay = false;

            frameNumber = 0;

            ClearPlayerObjects();

            if (playerInputManager != null)
            {
                playerInputManager.enabled = true;
                playerInputManager.EnableJoining();
                Debug.Log("PlayerInputManager re-enabled for offline play");
            }
        }

        ProjectileManager.Instance.DeleteAllProjectiles();

        Debug.Log("Match stopped and state reset");
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

    private void ResetMatchState()
    {
        frameNumber = 0;
        localPlayerInput = 0;
        syncedInput = new ulong[2] { 0, 0 };
        timeoutFrames = 0;
    }

    private void RunOnlineFrame()
    {
        RollbackManager rbManager = RollbackManager.Instance;
        if (rbManager == null) return;

        if (frameNumber <= rbManager.InputDelay)
        {
            rbManager.SaveState();
        }

        localPlayerInput = cachedLocalInput;
        codePrevFrame = codeCurrentFrame;
        jumpPrevFrame = jumpCurrentFrame;

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
            if (shopManager == null)
            {
                shopManager = FindAnyObjectByType<ShopManager>();
            }

            if (shopManager != null)
            {
                ulong[] shopInputs = new ulong[playerCount];
                for (int i = 0; i < playerCount; i++) shopInputs[i] = syncedInput[i];
                shopManager.ShopUpdate(shopInputs);
            }
        }
        else
        {
            shopManager = null;
        }

        UpdateGameState(syncedInput);

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
            if (!rbManager.isRollbackFrame)
            {
                if (CheckGameEnd(GetActivePlayerControllers()))
                {
                    for (int i = 0; i < playerCount; i++)
                    {
                        if (players[i].isAlive)
                        {
                            Debug.Log("Player " + (i + 1) + " wins the match!");
                            players[i].isAlive = false;
                            players[i].roundsWon++;

                            if (players[i].roundsWon >= 3) { gameOver = true; }
                            break;
                        }
                    }

                    ClearStages();

                    if (gameOver)
                    {
                        GameEnd();
                    }
                    else
                    {
                        RoundEnd();
                    }
                }
            }
        }

        if (!rbManager.isRollbackFrame && !rbManager.DelayBased)
        {
            rbManager.SaveState();
        }
    }

    // Handle spell selection for online players
    private void HandleOnlineSpellSelection()
    {
        // Use SYNCED inputs to make spell selection deterministic
        // Both players will see the same spell selections because they're using
        // synchronized inputs from the rollback system

        for (int i = 0; i < 2; i++)
        {
            if (players[i] == null) continue;

            // Generate starting spells if not spawned yet
            if (!players[i].isSpawned)
            {
                GenerateStartingSpells(i);

                // Only show spell card UI for local player
                if (i == localPlayerIndex)
                {
                    if (i == 0) p1_spellCard.enabled = true;
                    else if (i == 1) p2_spellCard.enabled = true;
                }

                players[i].isSpawned = true;
            }

            // Handle spell cycling and selection using the player's INPUT
            // This input is already synchronized from syncedInput[]
            if (!players[i].chosenStartingSpell && players[i].isSpawned)
            {
                List<string> choices = i == 0 ? p1_choices : p2_choices;
                int currentIndex = i == 0 ? p1_index : p2_index;
                Image spellCard = i == 0 ? p1_spellCard : p2_spellCard;

                // Use the synchronized input for THIS player
                InputSnapshot snapshot = InputConverter.ConvertFromLong(syncedInput[i]);

                // Cycle spells - check button 0 for PRESSED state
                if (snapshot.ButtonStates[0] == ButtonState.Pressed)
                {
                    Debug.Log($"[SYNCED] p{i + 1} pressed cycle spell (current index: {currentIndex})");

                    // Cycle through all 3 choices properly
                    currentIndex = (currentIndex + 1) % choices.Count;

                    if (i == 0) p1_index = currentIndex;
                    else p2_index = currentIndex;

                    Debug.Log($"[SYNCED] p{i + 1} new index: {currentIndex}, spell: {choices[currentIndex]}");

                    // Only update UI for local player
                    if (i == localPlayerIndex && spellCard != null)
                    {
                        spellCard.sprite = SpellDictionary.Instance.spellDict[choices[currentIndex]].shopSprite;
                    }
                }

                // Choose spell - check button 1 for PRESSED state
                if (snapshot.ButtonStates[1] == ButtonState.Pressed)
                {
                    Debug.Log($"[SYNCED] p{i + 1} chose spell: {choices[currentIndex]}");
                    players[i].AddSpellToSpellList(choices[currentIndex]);
                    players[i].startingSpell = choices[currentIndex];
                    players[i].chosenStartingSpell = true;

                    // Only hide UI for local player
                    if (i == localPlayerIndex && spellCard != null)
                    {
                        spellCard.enabled = false;
                    }
                }
            }
        }
    }

    public void ForceSetFrame(int newFrame)
    {
        this.frameNumber = newFrame;
    }

    protected void RunFrame()
    {
        if (playerInputManager != null)
        {
            playerInputManager.enabled = true;
            playerInputManager.EnableJoining();
        }

        ulong[] inputs = new ulong[playerCount];
        for (int i = 0; i < inputs.Length; ++i)
        {
            inputs[i] = players[i].GetInputs();
        }

        Scene activeScene = SceneManager.GetActiveScene();

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

            //player 1 stuff
            if (players[0] != null)
            {
                if (players[0].chosenStartingSpell == false && players[0].isSpawned)
                {
                    if (players[0].input.ButtonStates[0] == ButtonState.Pressed)
                    {
                        Debug.Log("p1 pressed cycle spell");
                        if (p1_index == p1_choices.Count - 1)
                        {
                            p1_index = 0;
                        }
                        else
                        {
                            p1_index++;
                        }

                        p1_spellCard.sprite = SpellDictionary.Instance.spellDict[p1_choices[p1_index]].shopSprite;
                    }

                    if (players[0].input.ButtonStates[1] == ButtonState.Pressed)
                    {
                        Debug.Log("p1 chose a spell");
                        players[0].AddSpellToSpellList(p1_choices[p1_index]);
                        players[0].startingSpell = p1_choices[p1_index];
                        players[0].chosenStartingSpell = true;
                        p1_spellCard.enabled = false;
                    }
                }

                if (players[0].isSpawned == false)
                {
                    GenerateStartingSpells(0);
                    p1_spellCard.enabled = true;
                    players[0].isSpawned = true;
                }
            }

            if (players[1] != null)
            {
                if (players[1].chosenStartingSpell == false && players[1].isSpawned)
                {
                    if (players[1].input.ButtonStates[0] == ButtonState.Pressed)
                    {
                        Debug.Log("p2 pressed cycle spell");
                        if (p2_index == p2_choices.Count - 1)
                        {
                            p2_index = 0;
                        }
                        else
                        {
                            p2_index++;
                        }

                        p2_spellCard.sprite = SpellDictionary.Instance.spellDict[p2_choices[p2_index]].shopSprite;
                    }

                    if (players[1].input.ButtonStates[1] == ButtonState.Pressed)
                    {
                        Debug.Log("p2 chose a spell");
                        players[1].AddSpellToSpellList(p2_choices[p2_index]);
                        players[1].startingSpell = p2_choices[p2_index];
                        players[1].chosenStartingSpell = true;
                        p2_spellCard.enabled = false;
                    }
                }

                if (players[1].isSpawned == false)
                {
                    GenerateStartingSpells(1);
                    p2_spellCard.enabled = true;
                    players[1].isSpawned = true;
                }
            }

            if (players[2] != null)
            {
                if (players[2].chosenStartingSpell == false && players[2].isSpawned)
                {
                    if (players[2].input.ButtonStates[2] == ButtonState.Pressed)
                    {
                        Debug.Log("p3 pressed cycle spell");
                        if (p3_index == p3_choices.Count - 1)
                        {
                            p3_index = 0;
                        }
                        else
                        {
                            p3_index++;
                        }

                        p3_spellCard.sprite = SpellDictionary.Instance.spellDict[p3_choices[p3_index]].shopSprite;
                    }

                    if (players[2].input.ButtonStates[1] == ButtonState.Pressed)
                    {
                        Debug.Log("p3 chose a spell");
                        players[2].AddSpellToSpellList(p3_choices[p3_index]);
                        players[2].startingSpell = p3_choices[p3_index];
                        players[2].chosenStartingSpell = true;
                        p3_spellCard.enabled = false;
                    }
                }

                if (players[2].isSpawned == false)
                {
                    GenerateStartingSpells(2);
                    p3_spellCard.enabled = true;
                    players[2].isSpawned = true;
                }
            }

            if (players[3] != null)
            {
                if (players[3].chosenStartingSpell == false && players[3].isSpawned)
                {
                    if (players[3].input.ButtonStates[0] == ButtonState.Pressed)
                    {
                        Debug.Log("p4 pressed cycle spell");
                        if (p4_index == p4_choices.Count - 1)
                        {
                            p4_index = 0;
                        }
                        else
                        {
                            p4_index++;
                        }

                        p4_spellCard.sprite = SpellDictionary.Instance.spellDict[p4_choices[p4_index]].shopSprite;
                    }

                    if (players[3].input.ButtonStates[1] == ButtonState.Pressed)
                    {
                        Debug.Log("p4 chose a spell");
                        players[3].AddSpellToSpellList(p4_choices[p4_index]);
                        players[3].startingSpell = p3_choices[p3_index];
                        players[3].chosenStartingSpell = true;
                        p4_spellCard.enabled = false;
                    }
                }

                if (players[3].isSpawned == false)
                {
                    GenerateStartingSpells(0);
                    p4_spellCard.enabled = true;
                    players[3].isSpawned = true;
                }
            }

            goDoorPrefab.CheckOpenDoor();

            if (goDoorPrefab.CheckAllPlayersReady())
            {
                LoadRandomGameplayStage();
            }
        }

        else if (activeScene.name == "Gameplay")
        {
            if (CheckGameEnd(GetActivePlayerControllers()))
            {
                for (int i = 0; i < playerCount; i++)
                {
                    players[i].playerNum.enabled = false;
                    players[i].inputDisplay.enabled = false;
                    if (players[i].isAlive)
                    {
                        playerWinText.enabled = true;
                        Debug.Log("Player " + (i + 1) + " wins the match!");
                        playerWinText.text = "Player " + (i + 1) + " wins the match!";
                        players[i].isAlive = false;
                        players[i].roundsWon++;

                        if (players[i].roundsWon >= 3) { gameOver = true; }
                        break;
                    }
                }

                if (roundEndTransitionTime >= roundEndTimer)
                {
                    roundEndTimer += Time.deltaTime;
                }

                if (roundEndTransitionTime <= roundEndTimer)
                {
                    ClearStages();
                    if (gameOver)
                    {
                        playerWinText.enabled = false;
                        GameEnd();
                        Debug.Log(roundEndTimer);
                        roundEndTimer = 0;
                    }
                    else
                    {
                        playerWinText.enabled = false;
                        RoundEnd();
                        Debug.Log(roundEndTimer);
                        roundEndTimer = 0;
                    }
                }
            }
        }
    }

    public void UpdateGameState(ulong[] inputs)
    {
        ProjectileManager.Instance.UpdateProjectiles();
        HitboxManager.Instance.ProcessCollisions();

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

    public void GetPlayerControllers(PlayerInput playerInput)
    {
        if (isOnlineMatchActive)
        {
            Debug.Log("GetPlayerControllers called but online match active - ignoring");
            return;
        }

        PlayerController existingPlayer = playerInput.GetComponent<PlayerController>();
        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] == existingPlayer)
            {
                Debug.LogWarning($"Player {existingPlayer.name} already registered at index {i} - ignoring duplicate registration");
                return;
            }
        }

        Debug.Log($"[GetPlayerControllers] Adding new player. Current playerCount={playerCount}");

        players[playerCount] = existingPlayer;
        players[playerCount].inputs.AssignInputDevice(playerInput.devices[0]);
        AnimationManager.Instance.InitializePlayerVisuals(players[playerCount], playerCount);

        playerCount++;

        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] != null && players[i].playerNum != null)
            {
                players[i].playerNum.text = "P" + (i + 1);
            }
        }

        Debug.Log($"[GetPlayerControllers] Player added. New playerCount={playerCount}");
    }

    public bool CheckGameEnd(PlayerController[] playerControllers)
    {
        int alivePlayers = 0;
        foreach (PlayerController player in playerControllers)
        {
            if (player.isAlive) alivePlayers++;
        }
        if (alivePlayers <= 1 && playerCount > 1)
        {
            return true;
        }
        return false;
    }

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

    public void RestartGame()
    {
        gameOver = false;
        Vector2[] spawnPositions = GetSpawnPositions();
        FixedVec2[] fixedSpawnPositions = spawnPositions
            .Select(v => FixedVec2.FromFloat(v.x, v.y))
            .ToArray();
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                players[i].ResetPlayer();
                players[i].SpawnPlayer(fixedSpawnPositions[i]);
            }
        }
    }

    /// <summary>
    /// Restarts the game from the lobby, not just a rematch
    /// </summary>
    public void RestartLobby()
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
    }

    public void GameEnd()
    {
        if (!isSaved)
        {
            dataManager.SaveMatch();
            isSaved = true;
        }

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
        if (currentStageIndex == -1)
        {
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
        if (playerInputManager != null)
        {
            playerInputManager.DisableJoining();
            playerInputManager.enabled = false;
            Debug.Log("Disabled PlayerInputManager before scene load");
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
                newStageIndex = Random.Range(0, stages.Length);
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
        Debug.Log($"Scene loaded: {scene.name}");

        if (!isOnlineMatchActive && scene.name == "Gameplay")
        {
            Debug.Log("Gameplay loaded (offline) - resetting players");

            if (playerInputManager != null)
            {
                playerInputManager.enabled = false;
            }

            ResetPlayers();
        }

        if (isOnlineMatchActive && scene.name == "Gameplay" && isTransitioning)
        {
            Debug.Log("Gameplay Scene Loaded - Resuming Online Match");
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
            Debug.Log("Shop Scene Loaded - Resuming Online Match in Shop");
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
        }
        if (index == 1)
        {
            p2_choices = new List<string>();
            p2_choices.Add("SkillshotSlash");
            p2_choices.Add("MightOfZeus");
            p2_choices.Add("AmonSlash");
        }
        if (index == 2)
        {
            p3_choices = new List<string>();
            p3_choices.Add("SkillshotSlash");
            p3_choices.Add("MightOfZeus");
            p3_choices.Add("AmonSlash");
        }
        if (index == 3)
        {
            p4_choices = new List<string>();
            p4_choices.Add("SkillshotSlash");
            p4_choices.Add("MightOfZeus");
            p4_choices.Add("AmonSlash");
        }
    }

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

    public byte[] SerializeManagedState()
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (BinaryWriter bw = new BinaryWriter(memoryStream))
            {
                bw.Write(playerCount);
                for (int i = 0; i < playerCount; i++)
                {
                    if (players[i] != null)
                    {
                        players[i].Serialize(bw);
                    }
                    else
                    {
                        Debug.LogError($"Attempted to serialize null player at index {i}");
                    }
                }

                // Serialize spell selection indices for lobby state
                bw.Write(p1_index);
                bw.Write(p2_index);
                bw.Write(p3_index);
                bw.Write(p4_index);

                bw.Write(p1_shopIndex);
                bw.Write(p2_shopIndex);

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
                    int prefabIndex = ProjectileManager.Instance.projectilePrefabs.IndexOf(projectile);
                    if (prefabIndex == -1)
                    {
                        Debug.LogError($"Active projectile {projectile.projName} (Owner: {projectile.owner?.characterName}) not found in master prefab list during Serialize!");
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

    public void DeserializeManagedState(byte[] stateData)
    {
        using (MemoryStream memoryStream = new MemoryStream(stateData))
        {
            using (BinaryReader br = new BinaryReader(memoryStream))
            {
                int savedPlayerCount = br.ReadInt32();
                if (savedPlayerCount != playerCount)
                {
                    Debug.LogWarning($"Player count mismatch during Deserialize! Saved: {savedPlayerCount}, Current: {playerCount}.");
                }
                for (int i = 0; i < playerCount; i++)
                {
                    if (players[i] != null)
                    {
                        players[i].Deserialize(br);
                    }
                    else
                    {
                        Debug.LogError($"Attempting to deserialize state into null player at index {i}.");
                    }
                }

                // Deserialize spell selection indices
                p1_index = br.ReadInt32();
                p2_index = br.ReadInt32();
                p3_index = br.ReadInt32();
                p4_index = br.ReadInt32();
                p1_shopIndex = br.ReadInt32();
                p2_shopIndex = br.ReadInt32();

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

                int savedProjectileCount = br.ReadInt32();
                List<BaseProjectile> masterList = ProjectileManager.Instance.projectilePrefabs;
                List<BaseProjectile> currentlyActive = ProjectileManager.Instance.activeProjectiles.ToList();
                List<BaseProjectile> shouldBeActive = new List<BaseProjectile>();

                Dictionary<int, byte[]> projectileStateData = new Dictionary<int, byte[]>();
                List<int> activePrefabIndices = new List<int>();

                for (int i = 0; i < savedProjectileCount; i++)
                {
                    int prefabIndex = br.ReadInt32();
                    if (prefabIndex == -1 || prefabIndex >= masterList.Count)
                    {
                        Debug.LogError($"Invalid prefab index ({prefabIndex}) read during projectile Deserialize. Skipping projectile state.");
                        continue;
                    }
                    activePrefabIndices.Add(prefabIndex);

                    long currentPos = br.BaseStream.Position;
                    if (prefabIndex >= 0 && prefabIndex < masterList.Count && masterList[prefabIndex] != null)
                    {
                        masterList[prefabIndex].Deserialize(br);
                    }
                    else
                    {
                        Debug.LogError("Cannot skip unknown projectile data.");
                    }
                    long nextPos = br.BaseStream.Position;
                    long dataSize = nextPos - currentPos;
                    br.BaseStream.Position = currentPos;
                    byte[] projData = br.ReadBytes((int)dataSize);
                    projectileStateData[prefabIndex] = projData;
                }

                foreach (BaseProjectile activeProj in currentlyActive)
                {
                    int currentPrefabIndex = masterList.IndexOf(activeProj);
                    if (!activePrefabIndices.Contains(currentPrefabIndex))
                    {
                        ProjectileManager.Instance.DeleteProjectile(activeProj);
                    }
                }

                foreach (int prefabIndex in activePrefabIndices)
                {
                    BaseProjectile projectileInstance = masterList[prefabIndex];
                    if (!projectileInstance.gameObject.activeSelf)
                    {
                        projectileInstance.ResetValues();
                        projectileInstance.gameObject.SetActive(true);
                        if (!ProjectileManager.Instance.activeProjectiles.Contains(projectileInstance))
                        {
                            ProjectileManager.Instance.activeProjectiles.Add(projectileInstance);
                        }
                    }
                    shouldBeActive.Add(projectileInstance);
                }

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
                        Debug.LogError($"State data for prefab index {prefabIndex} not found during load pass.");
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

                foreach (BaseProjectile projectile in ProjectileManager.Instance.activeProjectiles)
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