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
using BestoNet.Types;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class GameManager : MonoBehaviour/*NonPersistantSingleton<GameManager>*/
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
    // public StageDataSO currentStage;
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

    //main menu stuff (we will likely remove all of this later, its just a rehash of shop manager stuff)
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

    [Header("Input Management")]
    public PlayerInputManager playerInputManager;

    // New variables for Online Match State
    public int frameNumber { get; private set; } = 0; // Master frame counter
    private bool isOnlineMatchActive = false;
    private ulong localPlayerInput = 0; // Stores local input for the current frame
    private ulong[] syncedInput = new ulong[2] { 0, 0 }; // Inputs for both players this frame
    public int localPlayerIndex = 0; // Set this before starting online match
    public int remotePlayerIndex = 1; // Set this before starting online match
    private int timeoutFrames = 0; // Timeout counter

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

        SetStage(-1);
        //StartCoroutine(End());
    }

    // Update is called once per frame
    void Update()
    {
        //// If current scene isn't the gameplay scene, ensure players are marked dead and the temp UI is disabled.
        //Scene activeScene = SceneManager.GetActiveScene();
        //if (activeScene.name != "DEMO" && activeScene.name != "Gameplay")
        //{
        //    // Set all known players to not alive
        //    if (players != null)
        //    {
        //        for (int i = 0; i < players.Length; i++)
        //        {
        //            if (players[i] != null)
        //            {
        //                //players[i].isAlive = false;
        //                players[i].gameObject.GetComponent<SpriteRenderer>().enabled = false;
        //            }
        //        }
        //    }

        //    // Attempt to find and disable a child named "tempUI" (case-insensitive common variants)
        //    //TempUIScript tempUI = transform.Find("tempUI") ?? transform.Find("TempUI") ?? transform.Find("TempSpellUI") ?? transform.Find("TempSpellDisplay");
        //    if (tempUI != null)
        //    {
        //        tempUI.gameObject.SetActive(false);
        //    }

        //}
        //else
        //{
        //    // Ensure temp UI is enabled during gameplay
        //    if (tempUI != null)
        //    {
        //        tempUI.gameObject.SetActive(true);
        //    }
        //    // Also ensure all players' sprites are enabled
        //    if (players != null)
        //    {
        //        for (int i = 0; i < players.Length; i++)
        //        {
        //            if (players[i] != null && players[i].isAlive)
        //            {
        //                players[i].gameObject.GetComponent<SpriteRenderer>().enabled = true;
        //            }
        //        }
        //    }
        //}

        //if ` is pressed, toggle box rendering
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
        }

        if (!isOnlineMatchActive)
        {
            if (Input.GetKeyDown(toggleOnlineMenuKey))
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


        if (!isOnlineMatchActive || (RollbackManager.Instance != null && !RollbackManager.Instance.isRollbackFrame))
        {
            AnimationManager.Instance.RenderGameState();
        }
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
        }
    }

    // Match Control Methods


    /// <summary>
    /// Initializes and starts an online match. Requires RollbackManager.
    /// </summary>
    /// <param name="localIndex">Player index (0 or 1) for this client.</param>
    /// <param name="remoteIndex">Player index (0 or 1) for the opponent.</param>
    public void StartOnlineMatch(int localIndex, int remoteIndex, Steamworks.SteamId opponentId)
    {
        Debug.Log("Starting Online Match...");
        if (RollbackManager.Instance == null)
        {
            Debug.LogError("Cannot start online match: RollbackManager not found!");
            return;
        }
        if (!opponentId.IsValid) // Add check for valid opponent ID
        {
            Debug.LogError("Cannot start online match: Invalid Opponent SteamId provided!");
            return;
        }
        isOnlineMatchActive = true;

        // Find and disable the PlayerInputManager to prevent it from interfering
        //var inputManager = FindFirstObjectByType<UnityEngine.InputSystem.PlayerInputManager>();
        if (playerInputManager != null)
        {
            playerInputManager.DisableJoining();
            playerInputManager.enabled = false;
        }

        ResetMatchState(); // Reset frame counter, player states etc.
        localPlayerIndex = localIndex;
        remotePlayerIndex = remoteIndex;

        ClearPlayerObjects(); // Remove old players

        if (playerPrefab == null)
        {
            Debug.LogError("Player Prefab is not assigned in GameManager Inspector!");
            return;
        }

        // Force Instantiate 2 Players
        for (int i = 0; i < 2; i++)
        {
            GameObject p = Instantiate(playerPrefab);
            players[i] = p.GetComponent<PlayerController>();
            AnimationManager.Instance.InitializePlayerVisuals(players[i], i);
            // Assign default input device to null or dummy initially
            // inputs will be handled by RunOnlineFrame overrides
            if (i == remotePlayerIndex)
            {
                var pInput = p.GetComponent<UnityEngine.InputSystem.PlayerInput>();
                if (pInput != null)
                {
                    pInput.DeactivateInput();
                    pInput.enabled = false;
                }
            }
        }

        this.playerCount = 2; // Assuming 2-player online match for now

        players[localPlayerIndex].CheckForInputs(true);
        players[remotePlayerIndex].CheckForInputs(false);

        // Ensure players are spawned/reset
        ResetPlayers();


        // Pass opponent ID to RollbackManager.Init 
        // Get the ulong value from the SteamId struct
        RollbackManager.Instance.Init(opponentId.Value);

        // Initialize local player input device (example)
        // players[localPlayerIndex]?.inputs.AssignInputDevice(...); // Needs your specific input setup

        // Also initialize MatchMessageManager
        if (MatchMessageManager.Instance != null)
        {
            MatchMessageManager.Instance.StartMatch(opponentId);
        }
        else
        {
            Debug.LogError("MatchMessageManager not found during StartOnlineMatch!");
        }

        isOnlineMatchActive = true;
        isRunning = true;
        Debug.Log("Online Match Started.");
    }

    /// <summary>
    /// Stops the currently running match (local or online).
    /// </summary>
    /// <param name="reason">Reason for stopping.</param>
    public void StopMatch(string reason = "Match Ended")
    {
        if (!isRunning) return;
        Debug.Log($"Stopping Match: {reason}");

        isRunning = false;
        if (isOnlineMatchActive)
        {
            RollbackManager.Instance?.Disconnect(); // Clean up rollback state
            isOnlineMatchActive = false;
        }

        // General cleanup
        ProjectileManager.Instance.DeleteAllProjectiles();
        // Maybe reset player states or positions
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
        // Reset game-specific things like round number if needed
        // round = 1;
    }


    // Simulation Loop Methods

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

        localPlayerInput = 0;
        if (players[localPlayerIndex] != null && players[localPlayerIndex].isAlive)
        {
            localPlayerInput = players[localPlayerIndex].GetInputs();
        }

        bool timeSynced = rbManager.CheckTimeSync(out float frameAdvantageDifference);
        if (!timeSynced)
        {
            timeoutFrames++;
            if (timeoutFrames > rbManager.TimeoutFrames)
            {
                rbManager.TriggerMatchTimeout();
            }
            return; // Skip frame
        }

        timeoutFrames = 0;
        rbManager.RollbackEvent();

        frameNumber++;
        rbManager.SendLocalInput(localPlayerInput);
        syncedInput = rbManager.SynchronizeInput();

        // Stall check
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

        if (activeScene.name == "Gameplay")
        {
            // Only check end conditions if NOT rolling back
            // Don't want to trigger a scene change during a resimulation
            if (!rbManager.isRollbackFrame)
            {
                if (CheckGameEnd(GetActivePlayerControllers()))
                {
                    // Tally wins
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

                    // Handle Transitions
                    // NOTE: Scene changes in Online Play can be risky if not synced. 
                    // Should send a "Ready" packet before loading ideally.
                    // For now, mirror offline logic:
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

    /// <summary>
    /// Forces the internal frame number to a specific value.
    /// Used by RollbackManager after loading a previous state.
    /// </summary>
    /// <param name="newFrame">The frame number to set.</param>
    public void ForceSetFrame(int newFrame) // Make sure this is PUBLIC
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
            playerInputManager.EnableJoining();
        }

        ulong[] inputs = new ulong[playerCount];
        for (int i = 0; i < inputs.Length; ++i)
        {
            inputs[i] = players[i].GetInputs();
        }

        Scene activeScene = SceneManager.GetActiveScene();

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


        //if the game is not running, skip the update (everything after this uses player controller updates)
        if (!isRunning)
            return;

       

        UpdateGameState(inputs);

        if(activeScene.name == "MainMenu")
        {
            //player 1 stuff
            if (players[0] != null)
            {
                if (players[0].chosenStartingSpell == false && players[0].isSpawned)
                {
                    //cycle spells (spellWeave button)
                    if (players[0].input.ButtonStates[0] == ButtonState.Pressed)
                    {
                        Debug.Log("p1 pressed cycle spell");
                        if (p1_index == 1)
                        {
                            p1_index = 0;
                        }
                        else
                        {
                            p1_index++;
                        }

                        p1_spellCard.sprite = SpellDictionary.Instance.spellDict[p1_choices[p1_index]].shopSprite;
                    }

                    //choose spell (jump button)
                    if (players[0].input.ButtonStates[1] == ButtonState.Pressed)
                    {
                        Debug.Log("p1 chose a spell");
                        players[0].AddSpellToSpellList(p1_choices[p1_index]);
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
            //player 2 stuff
            if (players[1] != null)
            {
                if (players[1].chosenStartingSpell == false && players[1].isSpawned)
                {
                    //cycle spells (spellWeave button)
                    if (players[1].input.ButtonStates[0] == ButtonState.Pressed)
                    {
                        Debug.Log("p2 pressed cycle spell");
                        if (p2_index == 1)
                        {
                            p2_index = 0;
                        }
                        else
                        {
                            p2_index++;
                        }

                        p2_spellCard.sprite = SpellDictionary.Instance.spellDict[p2_choices[p2_index]].shopSprite;
                    }

                    //choose spell (jump button)
                    if (players[1].input.ButtonStates[1] == ButtonState.Pressed)
                    {
                        Debug.Log("p2 chose a spell");
                        players[1].AddSpellToSpellList(p2_choices[p2_index]);
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
            //player 3 stuff
            if (players[2] != null)
            {
                if (players[2].chosenStartingSpell == false && players[2].isSpawned)
                {
                    //cycle spells (spellWeave button)
                    if (players[2].input.ButtonStates[2] == ButtonState.Pressed)
                    {
                        Debug.Log("p3 pressed cycle spell");
                        if (p3_index == 1)
                        {
                            p3_index = 0;
                        }
                        else
                        {
                            p3_index++;
                        }

                        p3_spellCard.sprite = SpellDictionary.Instance.spellDict[p3_choices[p3_index]].shopSprite;
                    }

                    //choose spell (jump button)
                    if (players[2].input.ButtonStates[1] == ButtonState.Pressed)
                    {
                        Debug.Log("p3 chose a spell");
                        players[2].AddSpellToSpellList(p3_choices[p3_index]);
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
            //player 4 stuff
            if (players[3] != null)
            {
                if (players[3].chosenStartingSpell == false && players[3].isSpawned)
                {
                    //cycle spells (spellWeave button)
                    if (players[3].input.ButtonStates[0] == ButtonState.Pressed)
                    {
                        Debug.Log("p4 pressed cycle spell");
                        if (p4_index == 1)
                        {
                            p4_index = 0;
                        }
                        else
                        {
                            p4_index++;
                        }

                        p4_spellCard.sprite = SpellDictionary.Instance.spellDict[p4_choices[p4_index]].shopSprite;
                    }

                    //choose spell (jump button)
                    if (players[3].input.ButtonStates[1] == ButtonState.Pressed)
                    {
                        Debug.Log("p4 chose a spell");
                        players[3].AddSpellToSpellList(p4_choices[p4_index]);
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
            
            if(goDoorPrefab.CheckAllPlayersReady())
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
                    if (players[i].isAlive)
                    {
                        playerWinText.enabled = true;
                        Debug.Log("Player " + (i + 1) + " wins the match!");
                        playerWinText.text = "Player " + (i + 1) + " wins the match!";
                        players[i].isAlive = false; //reset for next round
                        players[i].roundsWon++;

                        if (players[i].roundsWon >= 3) { gameOver = true; }
                        break;
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
            return;
        }
        players[playerCount] = playerInput.GetComponent<PlayerController>();
        players[playerCount].inputs.AssignInputDevice(playerInput.devices[0]);
        AnimationManager.Instance.InitializePlayerVisuals(players[playerCount], playerCount);
        playerCount++;

        for (int i = 0; i < playerCount; i++)
        {
            players[i].playerNum.text = "P" + (i + 1);
        }

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
            isTransitioning = true; // Stop RunOnlineFrame
                                    
        }
        SceneManager.LoadScene("Shop");
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
        //make the next stage random but different from the last stage
        int newStageIndex;
        do
        {
            newStageIndex = Random.Range(0, stages.Length);

        } while (currentStageIndex == newStageIndex);
        SetStage(newStageIndex);
        SceneManager.LoadScene("Gameplay");
        ResetPlayers();
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
        }
        if (index == 1)
        {
            p2_choices = new List<string>();
            p2_choices.Add("SkillshotSlash");
            p2_choices.Add("MightOfZeus");
        }
        if (index == 2)
        {
            p3_choices = new List<string>();
            p3_choices.Add("SkillshotSlash");
            p3_choices.Add("MightOfZeus");
        }
        if (index == 3)
        {
            p4_choices = new List<string>();
            p4_choices.Add("SkillshotSlash");
            p4_choices.Add("MightOfZeus");
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

    // Central State Serialization Methods

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
                        Debug.LogError($"Attempted to serialize null player at index {i}");
                        // Need placeholder data or ensure players array is always contiguous
                    }
                }

                // Projectile State
                List<BaseProjectile> activeProjectiles = ProjectileManager.Instance.activeProjectiles;
                bw.Write(activeProjectiles.Count); // Save number of active projectiles

                foreach (BaseProjectile projectile in activeProjectiles)
                {
                    // Save an identifier to find this projectile instance later during Deserialize
                    // Using its index in the *master* prefab list is generally reliable if that list never changes order after init.
                    int prefabIndex = ProjectileManager.Instance.projectilePrefabs.IndexOf(projectile);
                    if (prefabIndex == -1)
                    {
                        Debug.LogError($"Active projectile {projectile.projName} (Owner: {projectile.owner?.characterName}) not found in master prefab list during Serialize!");
                        // Handle error: Maybe skip this projectile, but it indicates a problem
                        bw.Write(-1); // Write invalid index
                    }
                    else
                    {
                        bw.Write(prefabIndex); // Write its index from the master list
                        projectile.Serialize(bw); // Call projectile's serialize method
                    }
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
                // Global State
                // currentGameTimer = br.ReadSingle(); // Load any global state saved first

                // Player State
                int savedPlayerCount = br.ReadInt32();
                if (savedPlayerCount != playerCount)
                {
                    Debug.LogWarning($"Player count mismatch during Deserialize! Saved: {savedPlayerCount}, Current: {playerCount}. State might be corrupted if players joined/left mid-match (not typical for rollback).");
                    // Adjust playerCount or handle error
                    // Assuming playerCount is stable for rollback segment:
                    // playerCount = savedPlayerCount;
                }
                for (int i = 0; i < playerCount; i++) // Use current (or updated) playerCount
                {
                    if (players[i] != null)
                    {
                        players[i].Deserialize(br);
                    }
                    else
                    {
                        Debug.LogError($"Attempting to deserialize state into null player at index {i}.");
                        // Need to skip the appropriate bytes if a player is unexpectedly null
                    }
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
                        Debug.LogError($"Invalid prefab index ({prefabIndex}) read during projectile Deserialize. Skipping projectile state.");
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
                        Debug.LogError("Cannot skip unknown projectile data.");
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
                        Debug.LogError($"State data for prefab index {prefabIndex} not found during load pass.");
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
}
