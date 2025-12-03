using System;
using System.Collections.Generic;
using UnityEngine;
using BestoNet.Collections; // Use BestoNet collections

// Note: Removed IdolShowdown namespaces

// Using alias for FrameMetadataArray if defined in your project, otherwise use full name
// using FrameMetadataArray = YourProject.FrameMetadataArray; // Example if you defined it elsewhere

    public class RollbackManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static RollbackManager Instance { get; private set; }
        // --- End Singleton ---

        // --- Core Data Structures ---
        // GameState struct remains internal or defined globally
        public struct GameState
        {
            public int frame;
            public byte[] state;
        }
        // FrameMetadata struct remains internal or defined globally
        public struct FrameMetadata
        {
            public int frame;
            public ulong input;
        }
        // Use FrameMetadataArray from BestoNet or your definition
        // Assuming FrameMetadataArray inherits from BestoNet.Collections.CircularArray<FrameMetadata>
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
        // --- End Runtime State ---

        // --- External References (Set via Inspector or Init) ---
        // Removed LobbyManager, MatchRunner references
        private MatchMessageManager matchManager; // Reference to your message manager
        // Store opponent's network ID (e.g., SteamID ulong) - Must be provided during Init!
        private ulong opponentNetworkId;
        // --- End External References ---

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
            Debug.Log("Initializing Rollback Connection...");
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
            Debug.Log("Rollback Connection Initialized.");
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
            // Initialize to avoid timeout on first frames
            localFrameAdvantage = 0;
            opponentLastAppliedInput = 5;
            totalConsecutiveFrameExtensions = FrameExtensionWindow; // Initialize based on config
            if (matchManager != null) matchManager.sentFrameTimes.Clear(); // Clear ping calculation times if manager exists

            // FPSLock integration removed

            // Initialize states array
            for (int i = 0; i < StateArraySize; i++)
            {
                states[i] = new GameState() { frame = -1, state = null }; // Use null instead of empty array
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
            Debug.Log("Rollback variables cleared.");
        }

        /// <summary>
        /// Checks for input mismatches and triggers a rollback if necessary.
        /// Should be called once per frame before simulation.
        /// </summary>
        public void RollbackEvent()
        {
            if (DelayBased || GameManager.Instance == null || !GameManager.Instance.isRunning) // Check if match is running
            {
                return;
            }

            int framesBeforeRollback = localFrame; // Cache frame before potential load
            bool foundDesyncedFrame = false;

            // --- Check for Mismatched Inputs ---
            // Iterate from the frame *after* the last known sync point up to the current frame
            for (int i = syncFrame + 1; i <= framesBeforeRollback; i++)
            {
                // We need both the received input and the input we *used* (predicted or received) for this frame
                bool haveReceived = receivedInputs.ContainsKey(i);
                bool haveUsed = opponentInputs.ContainsKey(i); // This stores what was actually simulated

                if (haveReceived && haveUsed)
                {
                    ulong received = receivedInputs.GetInput(i);
                    ulong used = opponentInputs.GetInput(i);

                    // If they match AND we have a saved state for this frame, this frame is now confirmed synced
                    if (received == used && states[i % StateArraySize].frame == i)
                    {
                        syncFrame = i; // Advance the sync point
                    }
                    // If they don't match, we found a desync!
                    else if (received != used)
                    {
                        foundDesyncedFrame = true;
                        // Don't advance syncFrame, rollback needed from the *previous* sync point (syncFrame)
                        break; // Exit loop, no need to check further frames
                    }
                    // If received == used but state is missing, we might have issues saving state? Log warning?
                }
                else if (haveReceived && !haveUsed)
                {
                    // Received input for a frame we simulated predictively. This is a potential desync.
                    // (Unless prediction was perfect, but we check that above)
                    foundDesyncedFrame = true;
                    break;
                }
                // If !haveReceived, we can't confirm sync yet, just continue assuming prediction was ok for now.
            }
            // --- End Mismatch Check ---


            if (!foundDesyncedFrame)
            {
                // No mismatch found up to the current frame, no rollback needed this tick.
                return; // Exit rollback logic
            }

            // --- Perform Rollback ---
            // Rollback to the last known fully synchronized frame 'syncFrame'
            Debug.Log($"Rollback Triggered: Mismatch detected after frame {syncFrame}. Rolling back from {framesBeforeRollback}.");
            SetRollbackStatus(true); // Signal that we are now resimulating
            RollbackFrames = framesBeforeRollback - syncFrame;

            LoadState(syncFrame); // Load state from the last sync point (calls GameManager.Deserialize...)

            // Resimulate frames from syncFrame + 1 up to the original frame number
            for (int i = syncFrame + 1; i <= framesBeforeRollback; i++)
            {
                // Get the correct inputs for the resimulation frame 'i'
                ulong[] inputsForResim = SynchronizeInput(i); // Use version that takes frame number

                // Run the simulation step using GameManager
                GameManager.Instance.UpdateGameState(inputsForResim);
                GameManager.Instance.ForceSetFrame(i); // Ensure GameManager frame number matches simulation

                // Save state during resimulation (optional, GGPO does speculative saving)
                // SaveState(); // Save state for every resimulated frame? Or only specific ones?
                // Simple approach: Only save the *final* frame state outside this loop.
                // Complex/GGPO approach: Save state at intervals or based on remote frame.
                // Let's stick to saving outside the loop for simplicity for now.
                ClearState(i); // Clear potentially incorrect states saved earlier predictively
            }

            SetRollbackStatus(false); // Finished resimulating
            Debug.Log($"Rollback Complete. Resimulated {RollbackFrames} frames.");
            // The main loop will now continue from 'framesBeforeRollback', running the simulation
            // for the current frame again with (hopefully) corrected opponent input.
            // The SaveState call in the main loop will save the final corrected state.
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

            // Check if match ended to prevent dropping frames post-match
            // bool matchEnded = Check if GameManager indicates match end state? (Needs implementation)

            if (tooFarAheadOfSync && aheadOfRemote && !isRollbackFrame /* && !matchEnded */)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"Frame Drop: Local {currentFrame}, Sync {syncFrame} (Diff > {MaxRollBackFrames}). Dropping frame.");
#endif
                lastDroppedFrame = currentFrame; // Record dropped frame
                consecutiveDrop++; // Increment drop counter
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

            int localInputFrame = frame + InputDelay;
            ulong localInput = clientInputs.ContainsKey(localInputFrame)
                ? clientInputs.GetInput(localInputFrame)
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

        // Store the state in the circular buffer using the current local frame
        int frameIndex = localFrame % StateArraySize;
        states[frameIndex] = new GameState()
        {
            frame = localFrame,
            state = currentStateBytes // Store the byte array from GameManager
        };
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
    public void LoadState(int frame)
    {
        // Ensure GameManager instance is valid
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager instance is null during LoadState!");
            return;
        }

        int index = frame % StateArraySize;

        // Check if the state for the requested frame exists and is valid
        if (states[index].frame != frame || states[index].state == null || states[index].state.Length == 0)
        {
            UnityEngine.Debug.LogError($"Missing or invalid state when attempting to load frame {frame} at index {index}. Possible desync or state saving issue.");
            // Cannot proceed without valid state. Consider more robust error handling.
            return;
        }

        // Get the saved byte array
        byte[] stateBytes = states[index].state;

        // Call GameManager to deserialize and apply the state
        GameManager.Instance.DeserializeManagedState(stateBytes);

        // Force the GameManager's frame number to match the loaded state
        GameManager.Instance.ForceSetFrame(frame);
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
            int pingFrames = Mathf.CeilToInt((pingMs / 2f) / (1000f / 60f)); // Calculate one-way ping in frames (assuming 60fps)
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