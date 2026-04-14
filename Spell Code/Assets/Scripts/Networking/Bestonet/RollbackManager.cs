using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using BestoNet.Collections; // Use BestoNet collections

    public class RollbackManager : MonoBehaviour
    {
        private static bool IsStableGameplayHashFrame()
        {
            if (GameManager.Instance == null || !GameManager.Instance.isOnlineMatchActive)
            {
                return false;
            }

            return SceneManager.GetActiveScene().name == "Gameplay"
                && !GameManager.Instance.roundOver
                && !GameManager.Instance.isTransitioning
                && GameManager.Instance.currentStageIndex >= 0;
        }

        // --- Singleton Instance ---
        public static RollbackManager Instance { get; private set; }
        // --- End Singleton ---

        // --- Core Data Structures ---
        // GameState struct remains internal or defined globally
        public struct GameState
        {
            public int frame;
            public byte[] state;
            public uint hash;
            public uint sharedHash;
            public uint projectileHash;
            public uint player0Hash;
            public uint player1Hash;
            public uint player0CoreHash;
            public uint player1CoreHash;
            public uint player0SpellHash;
            public uint player1SpellHash;
        }
        // FrameMetadata struct remains internal or defined globally
        public struct FrameMetadata
        {
            public int frame;
            public ulong input;
        }

        // FrameMetadataArray inherits from BestoNet.Collections.CircularArray<FrameMetadata>
        // Adjust namespace if needed.
        public FrameMetadataArray receivedInputs { get; private set; }
        public FrameMetadataArray opponentInputs { get; private set; }
        public FrameMetadataArray clientInputs { get; private set; }
        public CircularArray<int> remoteFrameAdvantages { get; private set; }
        public CircularArray<int> localFrameAdvantages { get; private set; }
        public GameState[] states;
        // --- End Core Data Structures ---


        // --- Configuration (Set these in Inspector or via code) ---
        [Header("Rollback Settings")]
        [SerializeField] public int InputDelay = 2; // Default input delay frames
        [SerializeField] public bool DelayBased = false; // Use delay-based netcode instead of rollback? (Usually false)
        [SerializeField] public int MaxRollBackFrames = 7; // Max frames to rollback
        [SerializeField] public int FrameAdvantageLimit = 4; // Threshold to start dropping frames locally

        [Header("Timing & Sync")]
        // [SerializeField] public int SleepTimeMicro = 1500; // Used by FPSLock, removed for now
        [SerializeField] public float FrameExtensionLimit = 1.5f; // Threshold to start extending frames locally
        [SerializeField] public int FrameExtensionWindow = 7; // Frames over which extensions are averaged/limited
        [SerializeField] public int TimeoutFrames = 60; // Frames without sync before timeout

        // --- Constants ---
        // Make array sizes configurable or keep as constants
        private const int StateArraySize = 60;
        public const int InputArraySize = 60; // Should match StateArraySize generally
        private const int FrameAdvantageArraySize = 32;
        // --- End Constants ---

        // --- Runtime State ---
        public int RollbackFrames { get; private set; } = 0; // How many frames rolled back last time
        // public int RollbackFramesUI { get; private set; } = 0; // Removed UI specific variable
        public bool isRollbackFrame { get; private set; } = false; // True if currently resimulating
        private ulong opponentLastAppliedInput = 0; // For prediction
        private int totalConsecutiveFrameExtensions = 0;
        public int remoteFrame { get; private set; } = 0; // Latest frame confirmed by remote client
        public int predictedRemoteFrame { get; private set; } = 0; // Estimated remote frame based on ping
        public int syncFrame { get; private set; } = 0; // Last frame where inputs matched
        public int localFrameAdvantage { get; private set; } = 0;
        // public int remoteFrameAdvantage { get; private set;} = 0; // Set via SetRemoteFrameAdvantage
        private int lastDroppedFrame = -1;
        private int consecutiveDrop = 0;
        private int lastHashSentFrame = -1;
        private int firstHashMismatchFrame = -1;
        private readonly Dictionary<int, PendingRemoteHash> pendingRemoteHashes = new Dictionary<int, PendingRemoteHash>();
        // --- End Runtime State ---

        // --- External References (Set via Inspector or Init) ---
        private MatchMessageManager matchManager; // Reference to the message manager
        // Store opponent's network ID (e.g., SteamID ulong) - Must be provided during Init!
        private ulong opponentNetworkId;
        // --- End External References ---

        private struct PendingRemoteHash
        {
            public int frame;
            public uint remoteHash;
            public uint remoteSharedHash;
            public uint remoteProjectileHash;
            public uint remotePlayer0Hash;
            public uint remotePlayer1Hash;
            public uint remotePlayer0CoreHash;
            public uint remotePlayer1CoreHash;
            public uint remotePlayer0SpellHash;
            public uint remotePlayer1SpellHash;
        }

        // --- Properties ---
        // Get local frame directly from GameManager
        public int localFrame => GameManager.Instance != null ? GameManager.Instance.frameNumber : 0;
        // --- End Properties ---


        private void Awake()
        {
            // --- Singleton Setup ---
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            // --- End Singleton Setup ---

            // --- Initialize Collections ---
            // Ensure FrameMetadataArray constructor is accessible or adjust instantiation
            receivedInputs = new FrameMetadataArray(InputArraySize);
            opponentInputs = new FrameMetadataArray(InputArraySize);
            clientInputs = new FrameMetadataArray(InputArraySize);
            remoteFrameAdvantages = new CircularArray<int>(FrameAdvantageArraySize);
            localFrameAdvantages = new CircularArray<int>(FrameAdvantageArraySize);
            states = new GameState[StateArraySize];
            // --- End Initialize ---
        }

        /// <summary>
        /// Initializes the RollbackManager for a new online match.
        /// </summary>
        /// <param name="opponentNetId">The network identifier of the opponent.</param>
        /// <param name="inputDelayFrames">Optional override for input delay.</param>
        public void Init(ulong opponentNetId, int? inputDelayFrames = null)
        {
            //Debug.Log("Initializing Rollback Connection...");
            this.opponentNetworkId = opponentNetId;

        // Find MatchMessageManager instance
        matchManager = FindFirstObjectByType<MatchMessageManager>(); // Or use singleton: MatchMessageManager.Instance;
        if (matchManager == null)
            {
                Debug.LogError("MatchMessageManager not found!");
                // Handle error appropriately
                return;
            }

            if (inputDelayFrames.HasValue)
            {
                InputDelay = inputDelayFrames.Value;
            }
            // AutosetDelay logic removed, handle delay negotiation elsewhere if needed

            ClearVars(); // Reset state variables
            //Debug.Log("Rollback Connection Initialized.");
        }

        public void ApplyOnlineSettings(
            int inputDelay,
            bool delayBased,
            int maxRollbackFrames,
            int frameAdvantageLimit,
            float frameExtensionLimit,
            int frameExtensionWindow,
            int timeoutFrames)
        {
            InputDelay = inputDelay;
            DelayBased = delayBased;
            MaxRollBackFrames = maxRollbackFrames;
            FrameAdvantageLimit = frameAdvantageLimit;
            FrameExtensionLimit = frameExtensionLimit;
            FrameExtensionWindow = frameExtensionWindow;
            TimeoutFrames = timeoutFrames;

            bool safeToResetHistory = GameManager.Instance == null || GameManager.Instance.frameNumber == 0;
            if (safeToResetHistory)
            {
                ClearVars();
            }
            else
            {
                Debug.LogWarning("Received online rollback settings after simulation started; preserved live rollback history.");
            }
        }

        /// <summary>
        /// Resets all runtime state variables for a new match or disconnect.
        /// </summary>
        public void ClearVars()
        {
            receivedInputs.Clear();
            opponentInputs.Clear();
            clientInputs.Clear();
            remoteFrameAdvantages.Clear();
            localFrameAdvantages.Clear();

            lastDroppedFrame = -1;
            consecutiveDrop = 0;
            syncFrame = 0;
            predictedRemoteFrame = 0;
            remoteFrame = 0;
            lastHashSentFrame = -1;
            firstHashMismatchFrame = -1;
            pendingRemoteHashes.Clear();
            // Initialize to avoid timeout on first frames
            localFrameAdvantage = 0;
            opponentLastAppliedInput = 5;
            totalConsecutiveFrameExtensions = FrameExtensionWindow; // Initialize based on config
            if (matchManager != null) matchManager.sentFrameTimes.Clear(); // Clear ping calculation times if manager exists

            // FPSLock integration removed

            // Initialize states array
            for (int i = 0; i < StateArraySize; i++)
            {
                states[i] = new GameState() { frame = -1, state = null, hash = 0 }; // Use null instead of empty array
            }

            // Pre-fill input buffers for the input delay period with neutral input
            for (int i = 0; i <= InputDelay; i++)
            {
                var neutralFrame = new FrameMetadata() { frame = i, input = 5 };
                clientInputs.Insert(i, neutralFrame);
                opponentInputs.Insert(i, neutralFrame);
                receivedInputs.Insert(i, neutralFrame); // Can also be empty, prediction handles missing keys
            }

            // Initialize frame advantage arrays
            for (int i = 0; i < FrameAdvantageArraySize; i++)
            {
                remoteFrameAdvantages.Insert(i, 0);
                localFrameAdvantages.Insert(i, 0);
            }

            isRollbackFrame = false; // Ensure rollback status is reset
            //Debug.Log("Rollback variables cleared.");
        }

    /// <summary>
    /// Checks for input mismatches and triggers a rollback if necessary.
    /// Should be called once per frame before simulation.
    /// </summary>
    public void RollbackEvent()
    {
        if (DelayBased || GameManager.Instance == null || !GameManager.Instance.isRunning)
        {
            return;
        }

        int framesBeforeRollback = localFrame;
        bool foundDesyncedFrame = false;

        // Check for Mismatched Inputs
        for (int i = syncFrame + 1; i <= framesBeforeRollback; i++)
        {
            bool haveReceived = receivedInputs.ContainsKey(i);
            bool haveUsed = opponentInputs.ContainsKey(i);

            // ONLY CHECK - DON'T RESIMULATE HERE
            if (haveReceived && haveUsed)
            {
                ulong received = receivedInputs.GetInput(i);
                ulong used = opponentInputs.GetInput(i);

                if (received == used && states[i % StateArraySize].frame == i)
                {
                    // THE FRAME IS VERIFIED
                    syncFrame = i;

                    // HASH SENDING 
                    if (matchManager != null)
                    {
                        int interval = (StressTestController.Instance != null && StressTestController.Instance.enableStateHashing)
                            ? Mathf.Max(1, StressTestController.Instance.hashSendIntervalFrames)
                            : 30; // Default: send hash every 30 frames

                        if (IsStableGameplayHashFrame() && syncFrame % interval == 0 && syncFrame != lastHashSentFrame)
                        {
                            lastHashSentFrame = syncFrame;

                            // Grab the hashes already calculated during SaveState()
                            GameState verifiedState = states[syncFrame % StateArraySize];

                            matchManager.SendStateHash(
                                syncFrame,
                                verifiedState.hash,
                                verifiedState.sharedHash,
                                verifiedState.projectileHash,
                                verifiedState.player0Hash,
                                verifiedState.player1Hash,
                                verifiedState.player0CoreHash,
                                verifiedState.player1CoreHash,
                                verifiedState.player0SpellHash,
                                verifiedState.player1SpellHash
                            );
                        }
                    }
                    // -----------------------------
                }
                else if (received != used)
                {
                    foundDesyncedFrame = true;
                    break;
                }
            }
            else if (haveReceived && !haveUsed)
            {
                foundDesyncedFrame = true;
                break;
            }
        }
        // --- End Mismatch Check ---

        if (!foundDesyncedFrame)
        {
            ProcessPendingRemoteHashes();
            return; // No rollback needed
        }

        // --- Perform Rollback ---
        Debug.Log($"Rollback Triggered: Mismatch detected after frame {syncFrame}. Rolling back from {framesBeforeRollback}.");
        SetRollbackStatus(true);
        RollbackFrames = framesBeforeRollback - syncFrame;

        if (!LoadState(syncFrame))
        {
            Debug.LogError($"Rollback ABORTED: Failed to load state for frame {syncFrame}. Game may be desynced.");
            SetRollbackStatus(false);
            return;
        }

        // RESIMULATE HERE - ONLY WHEN ROLLBACK IS ACTUALLY NEEDED
        for (int i = syncFrame + 1; i <= framesBeforeRollback; i++)
        {
            ulong[] inputsForResim = SynchronizeInput(i);

            // Run base game state update
            GameManager.Instance.UpdateGameState(inputsForResim);

            // Run scene-specific logic (lobby spell selection, shop, etc.)
            GameManager.Instance.UpdateSceneLogic(inputsForResim);

            GameManager.Instance.ForceSetFrame(i);

            // Keep corrected snapshots for resimulated frames so a later packet can rollback again
            // from the newly authoritative state instead of losing rollback history.
            SaveState();
        }

        SetRollbackStatus(false);
        ProcessPendingRemoteHashes();
        Debug.Log($"Rollback Complete. Resimulated {RollbackFrames} frames.");
    }

    /// <summary>
    /// Sends the local player's input for the current frame (plus delay) to the opponent.
    /// </summary>
    /// <param name="input">The local player's input for the current frame.</param>
    public bool SendLocalInput(ulong input)
    {
            // Check if opponent ID is set and we have a match manager
            if (opponentNetworkId == 0 || matchManager == null || GameManager.Instance == null) return false;

            // Don't send inputs during the initial frames before the input delay buffer is filled? Optional.
            // if (localFrame < InputDelay) return true;

            // Store the input locally for the frame it corresponds to (current frame + delay)
            int targetFrame = localFrame + InputDelay;
            SetClientInput(targetFrame, input);

            // Send input packet via MatchMessageManager
            matchManager.SendInputs(); // Send target frame and input
            return true;
    }


        /// <summary>
        /// Determines if the simulation should run this frame based on input availability (for delay-based)
        /// or frame advantage limits (for rollback).
        /// </summary>
        public bool AllowUpdate()
        {
            if (GameManager.Instance == null) return false;

            int currentFrame = localFrame; // Use cached frame number

            // Timeout Check (moved here for early exit)
            if (consecutiveDrop > TimeoutFrames) // Use TimeoutFrames config
            {
                TriggerMatchTimeout(); // Handle timeout disconnect
                return false; // Don't allow update if timed out
            }

            // --- Delay-Based Mode Logic (If enabled) ---
            if (DelayBased)
            {
                // In delay-based mode, wait until inputs for the *current* frame are received
                if (!receivedInputs.ContainsKey(currentFrame))
                {
                    // Debug.Log($"DelayBased: Waiting for input frame {currentFrame}");
                    consecutiveDrop++; // Increment drop counter while waiting
                    return false; // Stall the simulation
                }
                else
                {
                    consecutiveDrop = 0; // Reset counter once input arrives
                    return true; // Allow update
                }
            }
            // --- End Delay-Based ---


            // --- Rollback Mode Frame Dropping (Based on Frame Advantage) ---
            // If we are too far ahead of the last confirmed sync point AND ahead of the remote frame estimate,
            // drop a frame locally to let the opponent catch up / reduce rollback intensity.
            bool tooFarAheadOfSync = (currentFrame - syncFrame) >= MaxRollBackFrames; // Use >= for check
            bool aheadOfRemote = currentFrame > remoteFrame; // Simple check if local frame > last confirmed remote frame

            // After a scene transition, remoteFrame is intentionally reset back to 0. Give the
            // new-scene input stream a short grace window to arrive before frame dropping kicks in,
            // otherwise one client can spiral into a startup stall while the other is still warming up.
            if (remoteFrame == 0 && currentFrame < 60)
            {
                consecutiveDrop = 0;
                return true;
            }

            // Check if match ended to prevent dropping frames post-match
            // bool matchEnded = Check if GameManager indicates match end state? (Needs implementation)

            if (tooFarAheadOfSync && aheadOfRemote && !isRollbackFrame /* && !matchEnded */)
            {
                consecutiveDrop++; // Increment drop counter

                // Avoid getting stuck in a permanent local freeze if the remote frame estimate
                // stops advancing in a real network environment. After a short burst of drops,
                // allow one simulation frame through and let rollback correct later.
                if (consecutiveDrop > 2)
                {
                    Debug.LogWarning($"Frame Drop Recovery: Local {currentFrame}, Sync {syncFrame}, Remote {remoteFrame}, ConsecutiveDrops {consecutiveDrop}. Allowing update to prevent stall.");
                    consecutiveDrop = 0;
                    return true;
                }

                Debug.LogWarning($"Frame Drop: Local {currentFrame}, Sync {syncFrame}, Remote {remoteFrame}, ConsecutiveDrops {consecutiveDrop}. Dropping frame.");
                lastDroppedFrame = currentFrame; // Record dropped frame
                return false; // Skip simulation this tick
            }
            // --- End Rollback Frame Dropping ---

            // If no conditions met to drop/stall, allow the update
            consecutiveDrop = 0; // Reset drop counter if update is allowed
            return true;
        }

        /// <summary>
        /// Gets the synchronized inputs for the current simulation frame.
        /// Uses received remote input if available, otherwise predicts.
        /// </summary>
        /// <param name="frameToGet">Optional: Specify frame number (used during rollback resimulation).</param>
        /// <returns>A ulong[2] array with inputs for Player 0 and Player 1.</returns>
        public ulong[] SynchronizeInput(int? frameToGet = null)
        {
            int frame = frameToGet ?? localFrame; // Use specified frame or current local frame

            // Inputs are stored keyed by the simulation frame they should apply to.
            // Reading frame + InputDelay here cancels the local delay entirely and makes
            // the local player advance earlier than the remote simulation.
            ulong localInput = clientInputs.ContainsKey(frame)
                ? clientInputs.GetInput(frame)
                : 5UL; // Default to neutral (5) if missing

            // Get remote input (predict if necessary)
            ulong remoteInput = PredictOpponentInput(frame);

            // Arrange inputs based on local player index
            ulong[] inputs = new ulong[2]; // Assuming 2 players for online
            int localIdx = GameManager.Instance.localPlayerIndex; // Get local player's index (0 or 1)
            int remoteIdx = GameManager.Instance.remotePlayerIndex; // Get remote player's index (0 or 1)

            if (localIdx >= 0 && localIdx < 2 && remoteIdx >= 0 && remoteIdx < 2)
            {
                inputs[localIdx] = localInput;
                inputs[remoteIdx] = remoteInput;
            }
            else
            {
                Debug.LogError($"Invalid player indices during SynchronizeInput! Local: {localIdx}, Remote: {remoteIdx}");
                // Default assignment (might be wrong)
                inputs[0] = localInput;
                inputs[1] = remoteInput;
            }

            return inputs;
        }

        /// <summary>
        /// Predicts the opponent's input for a given frame if the actual input hasn't arrived yet.
        /// Stores the predicted/received input in opponentInputs history.
        /// </summary>
        /// <param name="frame">The frame to get opponent input for.</param>
        /// <returns>The received or predicted opponent input.</returns>
        private ulong PredictOpponentInput(int frame)
        {
            // If we have the actual received input for this frame, use it
            if (receivedInputs.ContainsKey(frame))
            {
                ulong actualInput = receivedInputs.GetInput(frame);
                opponentLastAppliedInput = actualInput; // Update last known input
                // Store the *actual* input we are using for this frame's simulation
                opponentInputs.Insert(frame, new FrameMetadata() { frame = frame, input = actualInput });
                return actualInput;
            }
            else
            {
                // Simple prediction: reuse the last known input
                // Store the *predicted* input we are using for this frame's simulation
                opponentInputs.Insert(frame, new FrameMetadata() { frame = frame, input = opponentLastAppliedInput });
                return opponentLastAppliedInput;
            }
        }

    //// <summary>
    /// Saves the current game state by calling GameManager's serialization.
    /// </summary>
    public void SaveState()
    {
        // Ensure GameManager instance is valid
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager instance is null during SaveState!");
            return;
        }

        // Call GameManager to get the serialized state
        byte[] currentStateBytes = GameManager.Instance.SerializeManagedState();
        uint sharedHash = ComputeFnv1a(GameManager.Instance.SerializeSharedGameplayHashState());
        uint projectileHash = ComputeFnv1a(GameManager.Instance.SerializeProjectileHashState());
        uint player0Hash = 0;
        uint player1Hash = 0;
        uint player0CoreHash = 0;
        uint player1CoreHash = 0;
        uint player0SpellHash = 0;
        uint player1SpellHash = 0;
        if (GameManager.Instance.playerCount > 0 && GameManager.Instance.players[0] != null)
        {
            player0CoreHash = ComputePlayerCoreHash(GameManager.Instance.players[0]);
            player0SpellHash = ComputePlayerSpellHash(GameManager.Instance.players[0]);
            player0Hash = ComputePlayerHash(GameManager.Instance.players[0]);
        }
        if (GameManager.Instance.playerCount > 1 && GameManager.Instance.players[1] != null)
        {
            player1CoreHash = ComputePlayerCoreHash(GameManager.Instance.players[1]);
            player1SpellHash = ComputePlayerSpellHash(GameManager.Instance.players[1]);
            player1Hash = ComputePlayerHash(GameManager.Instance.players[1]);
        }
        uint hash = ComputeCompositeHash(sharedHash, projectileHash, player0Hash, player1Hash);

        // Store the state in the circular buffer using the current local frame
        int frameIndex = localFrame % StateArraySize;
        states[frameIndex] = new GameState()
        {
            frame = localFrame,
            state = currentStateBytes, // Store the byte array from GameManager
            hash = hash,
            sharedHash = sharedHash,
            projectileHash = projectileHash,
            player0Hash = player0Hash,
            player1Hash = player1Hash,
            player0CoreHash = player0CoreHash,
            player1CoreHash = player1CoreHash,
            player0SpellHash = player0SpellHash,
            player1SpellHash = player1SpellHash
        };

        // Always send state hashes during online matches for desync detection
        //if (matchManager != null)
        //{
        //    int interval = (StressTestController.Instance != null && StressTestController.Instance.enableStateHashing)
        //        ? Mathf.Max(1, StressTestController.Instance.hashSendIntervalFrames)
        //        : 30; // Default: send hash every 30 frames (~0.5s) in production
        //    if (localFrame % interval == 0 && localFrame != lastHashSentFrame)
        //    {
        //        lastHashSentFrame = localFrame;
        //        matchManager.SendStateHash(localFrame, hash, sharedHash, projectileHash, player0Hash, player1Hash, player0CoreHash, player1CoreHash, player0SpellHash, player1SpellHash);
        //    }
        //}
    }

    /// <summary>
    /// Clears the saved state for a specific frame number.
    /// </summary>
    /// <param name="frame">The frame number to clear.</param>
    public void ClearState(int frame)
    {
        int index = frame % StateArraySize;
        // Only clear if the stored frame matches the requested frame
        if (states[index].frame == frame)
        {
            states[index].frame = -1; // Mark as invalid
            states[index].state = null; // Release byte array reference
        }
    }

    /// <summary>
    /// Loads a game state snapshot for the specified frame using GameManager.
    /// </summary>
    /// <param name="frame">The frame number to load.</param>
    public bool LoadState(int frame)
    {
        // Ensure GameManager instance is valid
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager instance is null during LoadState!");
            return false;
        }

        int index = frame % StateArraySize;

        // Check if the state for the requested frame exists and is valid
        if (states[index].frame != frame || states[index].state == null || states[index].state.Length == 0)
        {
            UnityEngine.Debug.LogError($"Missing or invalid state when attempting to load frame {frame} at index {index}. Possible desync or state saving issue.");
            return false;
        }

        // Get the saved byte array
        byte[] stateBytes = states[index].state;

        // Call GameManager to deserialize and apply the state
        GameManager.Instance.DeserializeManagedState(stateBytes);

        // Force the GameManager's frame number to match the loaded state
        GameManager.Instance.ForceSetFrame(frame);
        return true;
    }

    public void OnRemoteStateHash(int frame, uint remoteHash, uint remoteSharedHash, uint remoteProjectileHash, uint remotePlayer0Hash, uint remotePlayer1Hash, uint remotePlayer0CoreHash, uint remotePlayer1CoreHash, uint remotePlayer0SpellHash, uint remotePlayer1SpellHash)
    {
        if (frame > syncFrame)
        {
            pendingRemoteHashes[frame] = new PendingRemoteHash()
            {
                frame = frame,
                remoteHash = remoteHash,
                remoteSharedHash = remoteSharedHash,
                remoteProjectileHash = remoteProjectileHash,
                remotePlayer0Hash = remotePlayer0Hash,
                remotePlayer1Hash = remotePlayer1Hash,
                remotePlayer0CoreHash = remotePlayer0CoreHash,
                remotePlayer1CoreHash = remotePlayer1CoreHash,
                remotePlayer0SpellHash = remotePlayer0SpellHash,
                remotePlayer1SpellHash = remotePlayer1SpellHash
            };
            return;
        }

        EvaluateRemoteStateHash(frame, remoteHash, remoteSharedHash, remoteProjectileHash, remotePlayer0Hash, remotePlayer1Hash, remotePlayer0CoreHash, remotePlayer1CoreHash, remotePlayer0SpellHash, remotePlayer1SpellHash);
    }

    private void ProcessPendingRemoteHashes()
    {
        if (pendingRemoteHashes.Count == 0)
        {
            return;
        }

        List<int> readyFrames = pendingRemoteHashes.Keys
            .Where(frame => frame <= syncFrame)
            .OrderBy(frame => frame)
            .ToList();

        foreach (int frame in readyFrames)
        {
            PendingRemoteHash pending = pendingRemoteHashes[frame];
            pendingRemoteHashes.Remove(frame);
            EvaluateRemoteStateHash(
                pending.frame,
                pending.remoteHash,
                pending.remoteSharedHash,
                pending.remoteProjectileHash,
                pending.remotePlayer0Hash,
                pending.remotePlayer1Hash,
                pending.remotePlayer0CoreHash,
                pending.remotePlayer1CoreHash,
                pending.remotePlayer0SpellHash,
                pending.remotePlayer1SpellHash);
        }
    }

    private void EvaluateRemoteStateHash(int frame, uint remoteHash, uint remoteSharedHash, uint remoteProjectileHash, uint remotePlayer0Hash, uint remotePlayer1Hash, uint remotePlayer0CoreHash, uint remotePlayer1CoreHash, uint remotePlayer0SpellHash, uint remotePlayer1SpellHash)
    {
        if (!IsStableGameplayHashFrame())
        {
            return;
        }

        int index = frame % StateArraySize;
        if (states[index].frame != frame || states[index].state == null)
        {
            return;
        }

        uint localHash = states[index].hash;
        if (localHash != remoteHash)
        {
            Debug.LogError($"[DESYNC HASH] Frame {frame} local={localHash} remote={remoteHash}");
            Debug.LogError($"[DESYNC HASH] Components shared local={states[index].sharedHash} remote={remoteSharedHash} | projectile local={states[index].projectileHash} remote={remoteProjectileHash} | p0 local={states[index].player0Hash} remote={remotePlayer0Hash} | p1 local={states[index].player1Hash} remote={remotePlayer1Hash}");
            Debug.LogError($"[DESYNC HASH] PlayerComponents p0core local={states[index].player0CoreHash} remote={remotePlayer0CoreHash} | p0spell local={states[index].player0SpellHash} remote={remotePlayer0SpellHash} | p1core local={states[index].player1CoreHash} remote={remotePlayer1CoreHash} | p1spell local={states[index].player1SpellHash} remote={remotePlayer1SpellHash}");

            // Always dump state on first mismatch for diagnosis
            if (firstHashMismatchFrame < 0)
            {
                string diag = BuildDesyncDiagnostics(frame);
                DumpLocalState(frame, localHash, remoteHash, states[index].state);
                DumpLocalHashState(frame, localHash, remoteHash);
                WriteDesyncTextReport(
                    frame,
                    localHash,
                    remoteHash,
                    remoteSharedHash,
                    remoteProjectileHash,
                    remotePlayer0Hash,
                    remotePlayer1Hash,
                    remotePlayer0CoreHash,
                    remotePlayer1CoreHash,
                    remotePlayer0SpellHash,
                    remotePlayer1SpellHash,
                    diag);
                if (!string.IsNullOrEmpty(diag))
                {
                    Debug.LogError(diag);
                }
            }
            firstHashMismatchFrame = frame;
        }
    }

    private void DumpLocalState(int frame, uint localHash, uint remoteHash, byte[] stateBytes)
    {
        if (stateBytes == null || stateBytes.Length == 0) return;

        string dir = StressTestController.Instance != null
            ? StressTestController.Instance.GetDumpDirectory()
            : Application.persistentDataPath;

        string fileName = $"desync_frame_{frame}_{localHash}_vs_{remoteHash}_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, stateBytes);
        Debug.LogError($"[DESYNC HASH] Wrote local state dump: {path}");
    }

    private void DumpLocalHashState(int frame, uint localHash, uint remoteHash)
    {
        if (GameManager.Instance == null) return;

        byte[] hashBytes = GameManager.Instance.SerializeHashState();
        if (hashBytes == null || hashBytes.Length == 0) return;

        string dir = StressTestController.Instance != null
            ? StressTestController.Instance.GetDumpDirectory()
            : Application.persistentDataPath;

        string fileName = $"desync_hashstate_{frame}_{localHash}_vs_{remoteHash}_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, hashBytes);
        Debug.LogError($"[DESYNC HASH] Wrote local hash-state dump: {path}");
    }

    private void WriteDesyncTextReport(
        int frame,
        uint localHash,
        uint remoteHash,
        uint remoteSharedHash,
        uint remoteProjectileHash,
        uint remotePlayer0Hash,
        uint remotePlayer1Hash,
        uint remotePlayer0CoreHash,
        uint remotePlayer1CoreHash,
        uint remotePlayer0SpellHash,
        uint remotePlayer1SpellHash,
        string diag)
    {
        if (GameManager.Instance == null) return;

        int index = frame % StateArraySize;
        string dir = StressTestController.Instance != null
            ? StressTestController.Instance.GetDumpDirectory()
            : Application.persistentDataPath;

        string fileName = $"desync_report_{frame}_{localHash}_vs_{remoteHash}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string path = Path.Combine(dir, fileName);

        List<string> lines = new List<string>
        {
            $"[DESYNC HASH] Frame {frame} local={localHash} remote={remoteHash}",
            $"[DESYNC HASH] Components shared local={states[index].sharedHash} remote={remoteSharedHash} | projectile local={states[index].projectileHash} remote={remoteProjectileHash} | p0 local={states[index].player0Hash} remote={remotePlayer0Hash} | p1 local={states[index].player1Hash} remote={remotePlayer1Hash}",
            $"[DESYNC HASH] PlayerComponents p0core local={states[index].player0CoreHash} remote={remotePlayer0CoreHash} | p0spell local={states[index].player0SpellHash} remote={remotePlayer0SpellHash} | p1core local={states[index].player1CoreHash} remote={remotePlayer1CoreHash} | p1spell local={states[index].player1SpellHash} remote={remotePlayer1SpellHash}"
        };

        if (!string.IsNullOrEmpty(diag))
        {
            lines.Add(diag);
        }

        File.WriteAllLines(path, lines);
        Debug.LogError($"[DESYNC HASH] Wrote desync report: {path}");
    }

    private void LogDesyncDiagnostics(int frame)
    {
        string diag = BuildDesyncDiagnostics(frame);
        if (!string.IsNullOrEmpty(diag))
        {
            Debug.LogError(diag);
        }
    }

    private string BuildDesyncDiagnostics(int frame)
    {
        if (GameManager.Instance == null) return string.Empty;
        var gm = GameManager.Instance;
        string diag = $"[DESYNC DIAG] Frame {frame} | " +
            $"callCount={gm.randomCallCount} seed={gm.randomSeed} " +
            $"roundOver={gm.roundOver} gameOver={gm.gameOver} ramToWin={gm.ramNeededToWinRound} " +
            $"stageIndex={gm.currentStageIndex} stage={gm.currentStage} rngState={gm.CurrentRngState} stageRngState={gm.CurrentStageRngState} " +
            $"sharedHash={ComputeSharedGameplayHash(gm)} projectileHash={ComputeProjectileHash()}";
        diag += $"\n  DamageMatrix=[{FormatDamageMatrix(gm.damageMatrix)}]";

        for (int i = 0; i < gm.playerCount; i++)
        {
            PlayerController p = gm.players[i];
            if (p == null) continue;
            diag += $"\n  P{i}: pos=({p.position.X.RawValue},{p.position.Y.RawValue}) hp={p.currentPlayerHealth} " +
                    $"state={p.state} hSpd={p.hSpd.RawValue} vSpd={p.vSpd.RawValue} logicFrame={p.logicFrame} " +
                    $"flow={p.flowState} demon={p.demonAura} isHit={p.isHit} isAlive={p.isAlive} facingRight={p.facingRight} " +
                    $"roundRam={p.roundRam} totalRam={p.totalRam} hash={ComputePlayerHash(p)}";

            for (int s = 0; s < p.spellList.Count; s++)
            {
                SpellData spell = p.spellList[s];
                if (spell == null) continue;
                diag += $"\n    Spell{s}:{spell.spellName} hash={ComputeSpellHash(spell)}";
            }
        }

        var activeProjectiles = ProjectileManager.Instance.activeProjectiles
            .OrderBy(proj => ProjectileManager.Instance.projectilePrefabs.IndexOf(proj))
            .ToList();
        diag += $"\n  ActiveProjectiles={activeProjectiles.Count}";
        foreach (BaseProjectile projectile in activeProjectiles)
        {
            int ownerPid = projectile.owner != null ? projectile.owner.pID : -1;
            string ignoreText = projectile.playerIgnoreArr != null ? string.Join(",", projectile.playerIgnoreArr) : "null";
            diag += $"\n    {projectile.projName}: pos=({projectile.position.X.RawValue},{projectile.position.Y.RawValue}) frame={projectile.logicFrame} owner={ownerPid} ignore=[{ignoreText}]";
        }

        return diag;
    }

    private string FormatDamageMatrix(byte[,] matrix)
    {
        if (matrix == null)
        {
            return "null";
        }

        List<string> rows = new List<string>();
        int rowCount = matrix.GetLength(0);
        int colCount = matrix.GetLength(1);
        for (int i = 0; i < rowCount; i++)
        {
            List<string> cols = new List<string>();
            for (int j = 0; j < colCount; j++)
            {
                cols.Add(matrix[i, j].ToString());
            }
            rows.Add(string.Join(",", cols));
        }

        return string.Join(" | ", rows);
    }

    private uint ComputePlayerHash(PlayerController player)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(memoryStream))
        {
            player.SerializeGameplayHash(bw);
            return ComputeFnv1a(memoryStream.ToArray());
        }
    }

    private uint ComputePlayerCoreHash(PlayerController player)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(memoryStream))
        {
            player.SerializeGameplayCoreHash(bw);
            return ComputeFnv1a(memoryStream.ToArray());
        }
    }

    private uint ComputePlayerSpellHash(PlayerController player)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(memoryStream))
        {
            player.SerializeGameplaySpellHash(bw);
            return ComputeFnv1a(memoryStream.ToArray());
        }
    }

    private uint ComputeSpellHash(SpellData spell)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(memoryStream))
        {
            bw.Write(spell.spellName ?? string.Empty);
            spell.Serialize(bw);
            return ComputeFnv1a(memoryStream.ToArray());
        }
    }

    private uint ComputeSharedGameplayHash(GameManager gm)
    {
        return ComputeFnv1a(gm.SerializeSharedGameplayHashState());
    }

    private uint ComputeProjectileHash()
    {
        return ComputeFnv1a(GameManager.Instance.SerializeProjectileHashState());
    }

    private uint ComputeCompositeHash(uint sharedHash, uint projectileHash, uint player0Hash, uint player1Hash)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(memoryStream))
        {
            bw.Write(sharedHash);
            bw.Write(projectileHash);
            bw.Write(player0Hash);
            bw.Write(player1Hash);
            return ComputeFnv1a(memoryStream.ToArray());
        }
    }

    private static uint ComputeFnv1a(byte[] data)
    {
        if (data == null) return 0;
        const uint offset = 2166136261;
        const uint prime = 16777619;
        uint hash = offset;
        for (int i = 0; i < data.Length; i++)
        {
            hash ^= data[i];
            hash *= prime;
        }
        return hash;
    }


    // --- Frame Timing / Advantage Methods ---
    // NOTE: These methods depended on an 'FPSLock' component which was removed.
    // They are provided here for reference but will cause errors or have no effect
    // without reimplementing a similar frame rate/timing control mechanism.

    /// <summary>
    /// [Requires FPSLock] Manages frame extensions based on network conditions.
    /// </summary>
    public void ExtendFrame()
    {
        /* // Original logic requiring FPSLock:
        if (FPSLock.Instance == null || !FPSLock.Instance.EnableRateLock)
        {
            return;
        }

        if (totalConsecutiveFrameExtensions < FrameExtensionWindow)
        {
            totalConsecutiveFrameExtensions++;
        }
        else
        {
            FPSLock.Instance.SetFrameExtension(0); // Stop extending
        }
        */
        Debug.LogWarning("ExtendFrame called, but FPSLock dependency was removed. Frame timing will not be adjusted.");
    }

    /// <summary>
    /// [Requires FPSLock] Initiates frame extensions if frame advantage is too high.
    /// </summary>
    /// <param name="frameAdvantageDifference">The calculated frame advantage difference.</param>
    public void StartFrameExtensions(float frameAdvantageDifference)
    {
        /* // Original logic requiring FPSLock:
        if (FPSLock.Instance == null || !FPSLock.Instance.EnableRateLock)
        {
            return;
        }

        // Only start extending if we haven't done so recently (within the window)
        if (totalConsecutiveFrameExtensions >= FrameExtensionWindow) // Use >= for check
        {
            #if UNITY_EDITOR
            Debug.Log($"Starting Frame Extensions: Local frame {localFrame}, Frame Advantage Diff {frameAdvantageDifference}");
            #endif
            FPSLock.Instance.SetFrameExtension(SleepTimeMicro); // Start extending
            totalConsecutiveFrameExtensions = 0; // Reset window counter
        }
        */
        Debug.LogWarning("StartFrameExtensions called, but FPSLock dependency was removed. Frame timing will not be adjusted.");
    }

    /// <summary>
    /// Checks if the local simulation is too far ahead or behind the remote simulation.
    /// Used by AllowUpdate to potentially drop frames.
    /// </summary>
    /// <param name="frameAdvantageDifference">Output: Calculated average frame advantage difference.</param>
    /// <returns>True if simulation timing is acceptable, false if a frame drop is recommended.</returns>
    public bool CheckTimeSync(out float frameAdvantageDifference)
    {
            //if (localFrame < 600)
            //{
            //    frameAdvantageDifference = 0;
            //    localFrameAdvantage = 0;
            //    return true;
            //}
            // Safety: if we haven't received any remote frames yet, assume we're in sync
            if (remoteFrame == 0 && localFrame < 60)
            {
                frameAdvantageDifference = 0;
                return true;
            }
        localFrameAdvantage = localFrame - predictedRemoteFrame; // Calculate current advantage
            SetLocalFrameAdvantage(localFrameAdvantage); // Store it in history

            frameAdvantageDifference = GetAverageFrameAdvantage(); // Calculate average over window

            // Check if we just dropped a frame - if so, allow update temporarily
            if (localFrame == lastDroppedFrame)
            {
                return true;
            }

            // If average advantage exceeds limit and not currently rolling back, recommend dropping frame
            if (frameAdvantageDifference > FrameAdvantageLimit && !isRollbackFrame)
            {
                // Decision to drop frame is made in AllowUpdate based on this check's result potentially
                // This method now just reports the difference and status.
                // AllowUpdate uses this info.
                return false; // Recommend dropping
            }
            return true; // Timing is okay
        }

        /// <summary> Sets the rollback status flag (true if resimulating). </summary>
        public void SetRollbackStatus(bool status)
        {
            isRollbackFrame = status;
            // physicsRollbackFrame removed, handle physics sync within main state/loop
        }

        // --- Input Buffer Handling ---
        /// <summary> Stores locally gathered input for the correct future frame. </summary>
        public void SetClientInput(int frame, ulong input)
        {
            clientInputs.Insert(frame, new FrameMetadata() { frame = frame, input = input });
        }

        /// <summary> Stores received remote input for the correct frame. Called by MatchMessageManager. </summary>
        public void SetOpponentInput(int frame, ulong input)
        {
            // Optional: Add logging if input for a frame is received multiple times with different values (potential issue)
            // if (receivedInputs.ContainsKey(frame) && receivedInputs.GetInput(frame) != input) {
            //     Debug.LogWarning($"Received conflicting input for frame {frame}. Old: {receivedInputs.GetInput(frame)}, New: {input}");
            // }
            receivedInputs.Insert(frame, new FrameMetadata() { frame = frame, input = input });
            // Update syncFrame if this input confirms a previously predicted frame?
            // Handled within RollbackEvent check now.
        }

        /// <summary> Stores the frame advantage reported by the remote client. Called by MatchMessageManager. </summary>
        public void SetRemoteFrameAdvantage(int frame, int advantage)
        {
            remoteFrameAdvantages.Insert(frame, advantage);
        }

        /// <summary> Stores the calculated local frame advantage for the current frame. </summary>
        public void SetLocalFrameAdvantage(int advantage)
        {
            localFrameAdvantages.Insert(localFrame, advantage);
        }

        /// <summary> Updates the latest known frame from the opponent and estimates their current frame. Called by MatchMessageManager. </summary>
        public void SetRemoteFrame(int frame)
        {
            remoteFrame = frame; // Last frame opponent confirmed sending/receiving input for
            // Predict current remote frame based on ping (needs ping calculation from MatchMessageManager)
            int pingMs = matchManager?.Ping ?? 200; // Default ping if manager missing
            // Integer-only: one-way ping in frames = (pingMs / 2) * 60 / 1000, rounded up
            // Equivalent to CeilToInt((pingMs/2) / 16.667) but fully deterministic
            int pingFrames = (pingMs * 60 + 1999) / 2000; // ceiling division without floats
            predictedRemoteFrame = frame + pingFrames;
            // Optional: Clamp predictedRemoteFrame to reasonable bounds?
        }

        /// <summary> Calculates the average frame advantage difference over a window. </summary>
        public float GetAverageFrameAdvantage()
        {
            // This calculation remains the same, using local/remote advantage history
            long localSum = 0; // Use long for sum to avoid potential overflow
            long remoteSum = 0;
            int[] localValues = localFrameAdvantages.GetValues(); // Get underlying array
            int[] remoteValues = remoteFrameAdvantages.GetValues(); // Get underlying array

            // Sum directly from arrays (might be slightly faster than repeated Get)
            for (int i = 0; i < FrameAdvantageArraySize; i++)
            {
                localSum += localValues[i];
                remoteSum += remoteValues[i];
            }

            // Use floating point for average calculation
            float localAverage = (float)localSum / FrameAdvantageArraySize;
            float remoteAverage = (float)remoteSum / FrameAdvantageArraySize;

            // Calculate the difference, ensuring it's non-negative
            // This specific formula might need tuning based on desired behavior
            float difference = Mathf.Max(0f, localAverage - remoteAverage);

            // The original formula seemed complex, simplifying:
            // return difference / 2f; // Example: Average the difference
            return difference; // Or just return the raw positive difference
        }
        // --- End Frame Timing / Advantage Methods ---


        // --- Disconnect / Timeout Logic ---
        // Desync Detector integration removed, handle desyncs based on state hash comparison if needed elsewhere
        // public void DesyncCheck() { ... }
        // public void InitDesyncDetector() { ... }
        // public void TriggerDesyncedStatus() { ... }

        /// <summary> Triggers the match timeout sequence. </summary>
        public void TriggerMatchTimeout()
        {
            Debug.LogError("Match Timeout Triggered!");
            Disconnect(); // Clean up RollbackManager state

            // Notify GameManager or handle scene changes / UI externally
            GameManager.Instance?.StopMatch("Connection Timeout"); // Tell GameManager to stop
            // TerminateMatch logic (scene changes, UI notifications) should be handled
            // by GameManager or a dedicated UIManager/SceneManager now.
        }

        /// <summary> Cleans up RollbackManager state on disconnect. </summary>
        public void Disconnect()
        {
            Debug.Log("RollbackManager Disconnecting...");
            ClearVars(); // Reset internal state
            SetRollbackStatus(false);
            // Don't handle scene changes or GameManager state here, let GameManager manage itself
        }
        // --- End Disconnect / Timeout ---

    } // End RollbackManager Class
