using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;
using BestoNet.Types;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using IdolShowdown.Managers;

public class GameManager : MonoBehaviour/*NonPersistantSingleton<GameManager>*/
{
    public static GameManager Instance { get; private set; }

    public PlayerController[] players = new PlayerController[4];
    public int playerCount = 0;

    public bool isRunning;
    public bool isSaved;
    private DataManager dataManager;
    public TempSpellDisplay[] tempSpellDisplays = new TempSpellDisplay[4];
    public TempUIScript tempUI;
    public StageDataSO currentStage;

    public int round = 1;
    public bool roundOver;

    public bool prevSceneWasShop;

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

        dataManager = DataManager.Instance;
        //StartCoroutine(End());
    }

    // Update is called once per frame
    void Update()
    {
        // If current scene isn't the gameplay scene, ensure players are marked dead and the temp UI is disabled.
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "DEMO" && activeScene.name != "Gameplay")
        {
            // Set all known players to not alive
            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null)
                    {
                        //players[i].isAlive = false;
                        players[i].gameObject.GetComponent<SpriteRenderer>().enabled = false;
                    }
                }
            }

            // Attempt to find and disable a child named "tempUI" (case-insensitive common variants)
            //TempUIScript tempUI = transform.Find("tempUI") ?? transform.Find("TempUI") ?? transform.Find("TempSpellUI") ?? transform.Find("TempSpellDisplay");
            if (tempUI != null)
            {
                tempUI.gameObject.SetActive(false);
            }

        }
        else
        {
                       // Ensure temp UI is enabled during gameplay
            if (tempUI != null)
            {
                tempUI.gameObject.SetActive(true);
            }
            // Also ensure all players' sprites are enabled
            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null && players[i].isAlive)
                    {
                        players[i].gameObject.GetComponent<SpriteRenderer>().enabled = true;
                    }
                }
            }
        }

        //This is just a shortcut for me to test stuff

        //if (Input.GetKeyDown(KeyCode.K))
        //{
        //    SaveMatch();
        //}

        //if ` is pressed, toggle box rendering
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
        }

        
    }

    private void FixedUpdate()
    {
        if (prevSceneWasShop)
        {
            ResetPlayers();
            prevSceneWasShop = false;
        }

        if (isOnlineMatchActive)
        {
            // Execute the online frame logic using RollbackManager
            RunOnlineFrame();
        }
        else
        {
            // Execute the simple offline frame logic
            RunLocalFrame();
        }

        RunFrame();

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
    /// Starts a local (offline) match.
    /// </summary>
    public void StartLocalMatch()
    {
        Debug.Log("Starting Local Match...");
        // Ensure playerCount is set correctly based on local players joined

        ResetMatchState(); // Reset frame counter, player states etc.
        isOnlineMatchActive = false;
        isRunning = true;
        // Call ResetPlayers or player spawning logic here
        ResetPlayers();
    }

    /// <summary>
    /// Initializes and starts an online match. Requires RollbackManager.
    /// </summary>
    /// <param name="localIndex">Player index (0 or 1) for this client.</param>
    /// <param name="remoteIndex">Player index (0 or 1) for the opponent.</param>
    public void StartOnlineMatch(int localIndex, int remoteIndex)
    {
        Debug.Log("Starting Online Match...");
        if (RollbackManager.Instance == null)
        {
            Debug.LogError("Cannot start online match: RollbackManager not found!");
            return;
        }

        ResetMatchState(); // Reset frame counter, player states etc.
        localPlayerIndex = localIndex;
        remotePlayerIndex = remoteIndex;
        isOnlineMatchActive = true;
        isRunning = true;

        // Ensure players are spawned/reset
        ResetPlayers();

        // Initialize Rollback Manager specific things
        RollbackManager.Instance.Init(); // Ensure RollbackManager knows player identities if needed
        RollbackManager.Instance.InitDesyncDetector(); // If using desync detection

        // Initialize local player input device (example)
        // players[localPlayerIndex]?.inputs.AssignInputDevice(...); // Needs the specific input setup

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
        RollbackManager rbManager = RollbackManager.Instance; // Cache for readability

        // Initial save state for frame 0 (or up to input delay)
        if (frameNumber <= rbManager.InputDelay)
        {
            rbManager.SaveState(); // Calls SerializeManagedState internally
        }

        // Read Local Input (for the *next* frame + input delay)
        localPlayerInput = 0; // Default to no input
        if (players[localPlayerIndex] != null && players[localPlayerIndex].isAlive)
        {
            localPlayerInput = players[localPlayerIndex].GetInputs(); // Assuming returns ulong
        }

        // Check Network Sync & Handle Rollback
        bool timeSynced = rbManager.CheckTimeSync(out float frameAdvantageDifference);
        if (timeSynced)
        {
            timeoutFrames = 0;
            rbManager.RollbackEvent(); // Checks for input mismatch and loads state if needed

            // Core Rollback Step
            frameNumber++; // Increment frame *before* simulating it
            rbManager.SendLocalInput(localPlayerInput); // Send input from *last* tick
            syncedInput = rbManager.SynchronizeInput(); // Get inputs for *this* frame

            // Check if RollbackManager wants to stall (e.g., waiting for input in delay-based mode)
            if (!rbManager.AllowUpdate())
            {
                frameNumber--; // Don't advance frame counter if stalled
                return; // Skip simulation this tick
            }

            // Run Deterministic Simulation
            RunDeterministicSimulationStep(syncedInput);

            // Save State (if not rolled back)
            if (!rbManager.isRollbackFrame && !rbManager.DelayBased)
            {
                rbManager.SaveState();
            }

            // Handle Frame Advantage Timing
            if (frameAdvantageDifference > rbManager.FrameExtensionLimit)
            {
                rbManager.StartFrameExtensions(frameAdvantageDifference);
            }
            rbManager.ExtendFrame(); // Apply timing adjustments
        }
        else // Not time synced (likely connection issue)
        {
            timeoutFrames++;
            if (timeoutFrames > rbManager.TimeoutFrameLimit)
            {
                rbManager.TriggerMatchTimeout(); // Let RollbackManager handle the disconnect
                // StopMatch("Connection Timeout"); // GameManager stops itself
            }
        }
    }

    /// <summary>
    /// Executes one frame of local (offline) match simulation.
    /// </summary>
    protected void RunLocalFrame()
    {
        frameNumber++; // Increment frame counter

        // Gather local inputs as ulong
        ulong[] localInputsULong = new ulong[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] != null && players[i].isAlive)
            {
                localInputsULong[i] = players[i].GetInputs(); // Assuming returns ulong
            }
        }

        // Run the deterministic simulation step
        RunDeterministicSimulationStep(localInputsULong);

        // Check local game end condition
        // if (CheckGameEnd(GetActivePlayerControllers())) { /* Handle local win/loss */ }
    }

    /// <summary>
    /// The core deterministic update sequence for one frame.
    /// Called by both online and local loops.
    /// </summary>
    /// <param name="currentInputs">The ulong input array for players this frame.</param>
    private void RunDeterministicSimulationStep(ulong[] currentInputs)
    {
        // Order of Operations
        ProjectileManager.Instance.UpdateProjectiles(); // 1. Update Projectiles
        HitboxManager.Instance.ProcessCollisions();   // 2. Process Collisions
        // 3. Update Players
        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] != null)
            {
                ulong inputForPlayer = (i < currentInputs.Length) ? currentInputs[i] : 0UL; // Get input or default
                players[i].PlayerUpdate(inputForPlayer); // Assumes PlayerUpdate takes ulong
            }
        }
        // 4. Update Player Effects (after main state update)
        for (int i = 0; i < playerCount; i++)
        {
            if (players[i] != null && players[i].isAlive)
            {
                players[i].ProcEffectUpdate();
            }
        }
    }

    /// <summary>
    /// Runs a single frame of the game.
    /// </summary>
    protected void RunFrame()
    {
        if (!isRunning)
            return;

        ulong[] inputs = new ulong[playerCount];
        for (int i = 0; i < inputs.Length; ++i)
        {
            inputs[i] = players[i].GetInputs();
        }

        UpdateGameState(inputs);
        if (CheckGameEnd(GetActivePlayerControllers()))
        {
            for (int i = 0; i < playerCount; i++)
            {
                if (players[i].isAlive)
                {
                    Debug.Log("Player " + (i + 1) + " wins the match!");
                    players[i].isAlive = false; //reset for next round
                    break;
                }
            }
            dataManager.totalRoundsPlayed += 1;

            //Game end logic here
            if (dataManager.totalRoundsPlayed == 3)
            {
                dataManager.totalRoundsPlayed = 0;
                GameEnd();
            }
            else
            {
                RoundEnd();
            }
        }
    }

    /// <summary>
    /// Updates the game state based on the provided inputs.
    /// </summary>
    /// <param name="inputs">Array of inputs for each player.</param>
    protected void UpdateGameState(ulong[] inputs)
    {
        ProjectileManager.Instance.UpdateProjectiles();

        HitboxManager.Instance.ProcessCollisions();

        //update each player update values
        for (int i = 0; i < playerCount; i++)
        {
            players[i].PlayerUpdate(inputs[i]);
        }

        for (int i = 0; i < playerCount; i++)
        {
            if (players[i].isAlive) {
                players[i].ProcEffectUpdate();
            }
        }
    }

    //gets called everytime a new player enters, recreates player array
    public void GetPlayerControllers(PlayerInput playerInput)
    {
        players[playerCount] = playerInput.GetComponent<PlayerController>();
        players[playerCount].inputs.AssignInputDevice(playerInput.devices[0]);
        AnimationManager.Instance.InitializePlayerVisuals(players[playerCount], playerCount);
        playerCount++;



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
        foreach (PlayerController player in players)
        {
            if (player != null)
            {
                player.SpawnPlayer(FixedVec2.Zero);
            }
        }

        isSaved = false;
    }

    public void SaveMatch()
    {
        //general game data
        MatchData matchData = new MatchData();

        //player data, looped for each player
        if (playerCount > 0)
        {
            matchData.playerData = new PlayerData[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                Fixed totalSpelltime = Fixed.FromInt(0);

                //raw stats
                matchData.playerData[i] = new PlayerData();
                matchData.playerData[i].basicsFired = players[i].basicsFired;
                matchData.playerData[i].codesFired = players[i].spellsFired;
                matchData.playerData[i].codesHit = players[i].spellsHit;
                matchData.playerData[i].synthesizer = players[i].characterName;
                matchData.playerData[i].times = new List<float>();
                foreach (Fixed timeValue in players[i].times)
                {
                    matchData.playerData[i].times.Add(timeValue.ToFloat()); // Convert Fixed to float
                }

                if (players[i].currrentPlayerHealth > 0)
                {
                    matchData.playerData[i].matchWon = true;
                }
                else
                {
                    matchData.playerData[i].matchWon = false;
                }

                //calculated accuracy
                if(players[i].basicsFired + players[i].spellsFired == 0)
                {
                    matchData.playerData[i].accuracy = 0;
                }
                else
                {
                    matchData.playerData[i].accuracy = players[i].spellsHit / (players[i].basicsFired + players[i].spellsFired);
                }

                //calculated avg time to cast a spell (totalTime / instances of times) 
                for (int k = 0; k < players[i].times.Count; k++)
                {
                    totalSpelltime += players[i].times[k];
                }

                if (players[i].times.Count > 0)
                {
                    Fixed averageTimeFixed = totalSpelltime / Fixed.FromInt(players[i].times.Count);
                    matchData.playerData[i].avgTimeToCast = averageTimeFixed.ToFloat();
                }
                else
                {
                    matchData.playerData[i].avgTimeToCast = 0;
                }

                    //save spell name to spellList provided it isn't null. If null, add 'no spell'
                    matchData.playerData[i].spellList = new string[players[i].spellList.Count];
                for (int j = 0; j < players[i].spellList.Count; j++)
                {
                    if (players[i].spellList[j] is null)
                    {
                        matchData.playerData[i].spellList[j] = "no spell";
                    }
                    else
                    {
                        matchData.playerData[i].spellList[j] = players[i].spellList[j].spellName;
                    }
                }
            }
        }

        //save match data to gameData object
        dataManager.gameData.matchData.Add(matchData);

        //players = new PlayerController[4];
        //playerCount = 0;
    }

    //A round is 1 match + spell acquisition phase
    public void RoundEnd()
    {
        if (!isSaved)
        {
            SaveMatch();
            isSaved = true;
        }
        ProjectileManager.Instance.DeleteAllProjectiles();
        isRunning = false;
        SceneManager.LoadScene("Shop");
    }

    //called when a game ends (game is a series of matches/rounds)
    public void GameEnd()
    {
        if (!isSaved)
        {
            SaveMatch();
            isSaved = true;
        }

        dataManager.SaveToFile();
        ProjectileManager.Instance.DeleteAllProjectiles();
        isRunning = false;
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
