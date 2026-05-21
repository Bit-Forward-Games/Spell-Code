using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using BestoNet.Collections; // Use BestoNet collections
using DiagnosticsStopwatch = System.Diagnostics.Stopwatch;

    public class RollbackManager : MonoBehaviour
    {
        private static bool IsStableGameplayHashFrame()
        {
            if (GameManager.Instance == null || !GameManager.Instance.isOnlineMatchActive)
            {
                return false;
            }

            if (Instance != null && Instance.IsWaitingForInitialRemoteInputStreams())
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
            public uint[] playerHashes;
            public uint[] playerCoreHashes;
            public uint[] playerSpellHashes;
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
        private readonly Dictionary<int, FrameMetadataArray> receivedInputsBySlot = new Dictionary<int, FrameMetadataArray>();
        private readonly Dictionary<int, FrameMetadataArray> usedInputsBySlot = new Dictionary<int, FrameMetadataArray>();
        private readonly Dictionary<int, CircularArray<int>> remoteFrameAdvantagesBySlot = new Dictionary<int, CircularArray<int>>();
        private readonly Dictionary<int, ulong> remoteLastAppliedInputBySlot = new Dictionary<int, ulong>();
        private readonly Dictionary<int, int> remoteFrameBySlot = new Dictionary<int, int>();
        private readonly Dictionary<int, int> predictedRemoteFrameBySlot = new Dictionary<int, int>();
        private readonly Dictionary<int, int> highestRemoteInputFrameSeenBySlot = new Dictionary<int, int>();
        private readonly Dictionary<int, int> remoteFrameOffsetBySlot = new Dictionary<int, int>();
        private readonly Dictionary<int, int> remotePredictedInputStreakBySlot = new Dictionary<int, int>();
        private readonly HashSet<int> pendingRemoteInputSlots = new HashSet<int>();
        private int? pendingRosterFrameOffset = null;
        private readonly List<int> remotePlayerSlots = new List<int>();
        private bool usePeerRoster = false;
        private OnlineMatchRoster activeRoster;
        // --- End Core Data Structures ---


        // --- Configuration (Set these in Inspector or via code) ---
        [Header("Rollback Settings")]
        [SerializeField] public int InputDelay = 0; // Default input delay frames
        [SerializeField] public bool DelayBased = false; // Use delay-based netcode instead of rollback? (Usually false)
        [SerializeField] public int MaxRollBackFrames = 4; // BestoNet default: keep rollback corrections tight
        [SerializeField] public int FrameAdvantageLimit = 3; // BestoNet default: start pacing before rollback gets large
        [SerializeField] public int SoftFramePacingThreshold = 3; // Start gently pacing before the hard rollback limit
        [SerializeField] public int MaxConsecutiveFrameDrops = 1; // Pulse holds to avoid transition deadlocks
        [SerializeField] public int MaxPredictionAheadFrames = 18; // Cap visible remote input latency before pacing
        [SerializeField] public int DirectionPredictionHoldFrames = 6; // Stop predicting held movement after short packet gaps
        [SerializeField] public int CodeButtonPredictionHoldFrames = 8; // Synthesize release if Code packets stall

        [Header("Packet Loss Smoothing")]
        // Optional adaptive layer that briefly holds the local sim when packet loss is detected,
        // shrinking the prediction window so visible rollback corrections (teleports) stay small.
        // Disable to revert to baseline rollback behavior.
        [SerializeField] public bool EnablePacketLossSmoothing = true;
        // Sticky weight added to the loss signal each time a gap in remote inputs is observed.
        [SerializeField] public int PacketLossEventWeight = 4;
        // Loss signal must reach this level before any holds happen. Filters single late packets.
        [SerializeField] public int PacketLossHoldThreshold = 6;
        // Hard cap on consecutive holds caused by packet loss, to bound added latency.
        [SerializeField] public int MaxLossAwareHolds = 2;
        // How quickly the loss signal decays each tick when no new losses arrive.
        [SerializeField] public int PacketLossDecayPerTick = 1;

        [Header("Timing & Sync")]
        [SerializeField] public bool EnableFrameExtension = true;
        [SerializeField] public int SleepTimeMicro = 1500; // BestoNet FPSLock-style local frame extension
        [SerializeField] public float FrameExtensionLimit = 1.5f; // Threshold to start extending frames locally
        [SerializeField] public int FrameExtensionWindow = 7; // Frames over which extensions are averaged/limited
        [SerializeField] public int TimeoutFrames = 60; // Frames without sync before timeout
        [SerializeField] public float TransitionStartupTimeoutGraceSeconds = 10f; // Grace after scene loads/focus stalls

        // --- Constants ---
        // Make array sizes configurable or keep as constants
        private const int StateArraySize = 180;
        public const int InputArraySize = 180; // Should match StateArraySize generally
        private const int FrameAdvantageArraySize = 32;
        // --- End Constants ---

        // --- Runtime State ---
        public int RollbackFrames { get; private set; } = 0; // How many frames rolled back last time
        // public int RollbackFramesUI { get; private set; } = 0; // Removed UI specific variable
        public bool isRollbackFrame { get; private set; } = false; // True if currently resimulating
        private ulong opponentLastAppliedInput = 0; // For prediction
        private int opponentPredictedInputStreak = 0;
        private int totalConsecutiveFrameExtensions = 0;
        public int remoteFrame { get; private set; } = 0; // Latest frame confirmed by remote client
        public int predictedRemoteFrame { get; private set; } = 0; // Estimated remote frame based on ping
        public int syncFrame { get; private set; } = 0; // Last frame where inputs matched
        public int localFrameAdvantage { get; private set; } = 0;
        // public int remoteFrameAdvantage { get; private set;} = 0; // Set via SetRemoteFrameAdvantage
        private int lastDroppedFrame = -1;
        private int consecutiveDrop = 0;
        private int lastRemoteFrameForTimeout = 0;
        private int remoteFrameStallTicks = 0;
        private float timeoutGraceUntilRealtime = 0f;
        // Packet-loss smoothing runtime state. All zero/-1 when no loss is happening,
        // so the AllowUpdate fast path stays free of overhead.
        private int packetLossSignal = 0;            // Decaying score; rises on detected gaps
        private int highestRemoteInputFrameSeen = -1; // Highest frame number ever inserted into receivedInputs
        private int lossAwareHoldsThisStreak = 0;     // Bounded by MaxLossAwareHolds
        private int lastLossAwareHoldFrame = -1;      // Last local frame we held due to loss
        private int currentFrameExtensionMicro = 0;
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
            usePeerRoster = false;
            activeRoster = null;
            remotePlayerSlots.Clear();

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

        public void Init(OnlineMatchRoster roster, int? inputDelayFrames = null)
        {
            activeRoster = roster;
            usePeerRoster = roster != null;
            opponentNetworkId = 0;
            remotePlayerSlots.Clear();

            matchManager = FindFirstObjectByType<MatchMessageManager>();
            if (matchManager == null)
            {
                Debug.LogError("MatchMessageManager not found!");
                return;
            }

            if (inputDelayFrames.HasValue)
            {
                InputDelay = inputDelayFrames.Value;
            }

            if (roster != null)
            {
                for (int i = 0; i < roster.Peers.Count; i++)
                {
                    OnlineMatchPeerInfo peer = roster.Peers[i];
                    if (peer != null && peer.PlayerSlot != roster.LocalPlayerSlot)
                    {
                        remotePlayerSlots.Add(peer.PlayerSlot);
                    }
                }
            }

            ClearVars();
        }

        public void UpdateRoster(OnlineMatchRoster roster)
        {
            if (roster == null)
            {
                return;
            }

            activeRoster = roster;
            usePeerRoster = true;
            List<int> previousRemoteSlots = new List<int>(remotePlayerSlots);
            remotePlayerSlots.Clear();

            HashSet<int> validRemoteSlots = new HashSet<int>();
            for (int i = 0; i < roster.Peers.Count; i++)
            {
                OnlineMatchPeerInfo peer = roster.Peers[i];
                if (peer != null && peer.PlayerSlot != roster.LocalPlayerSlot)
                {
                    remotePlayerSlots.Add(peer.PlayerSlot);
                    validRemoteSlots.Add(peer.PlayerSlot);
                }
            }

            PruneRemoteSlotTracking(validRemoteSlots);
            EnsureRemoteCollectionsInitialized();
            PrimeRosterInputHistory();
            if (HasRemoteSlotSetChanged(previousRemoteSlots, remotePlayerSlots))
            {
                bool addedPendingSlot = false;
                for (int i = 0; i < remotePlayerSlots.Count; i++)
                {
                    int slot = remotePlayerSlots[i];
                    if (!previousRemoteSlots.Contains(slot))
                    {
                        pendingRemoteInputSlots.Add(slot);
                        addedPendingSlot = true;
                    }
                }

                if (addedPendingSlot)
                {
                    pendingRosterFrameOffset = null;
                }

                ResetRollbackHistoryForRosterChange();
            }
        }

        private bool HasRemoteSlotSetChanged(List<int> previousSlots, List<int> currentSlots)
        {
            if (previousSlots.Count != currentSlots.Count)
            {
                return true;
            }

            for (int i = 0; i < currentSlots.Count; i++)
            {
                if (!previousSlots.Contains(currentSlots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetRollbackHistoryForRosterChange()
        {
            ResetRollbackBaseline(localFrame);
        }

        public void ResetRollbackBaseline(int frame)
        {
            int baselineFrame = Mathf.Max(0, frame);
            for (int i = 0; i < StateArraySize; i++)
            {
                states[i] = new GameState() { frame = -1, state = null, hash = 0 };
            }

            syncFrame = baselineFrame;
            RollbackFrames = 0;
            isRollbackFrame = false;
            lastHashSentFrame = -1;
            pendingRemoteHashes.Clear();
            remoteFrameStallTicks = 0;
            lastRemoteFrameForTimeout = remoteFrame;
            ResetTimeoutGrace(TransitionStartupTimeoutGraceSeconds);
        }

        private void PruneRemoteSlotTracking(HashSet<int> validRemoteSlots)
        {
            PruneSlotDictionary(receivedInputsBySlot, validRemoteSlots);
            PruneSlotDictionary(usedInputsBySlot, validRemoteSlots);
            PruneSlotDictionary(remoteFrameAdvantagesBySlot, validRemoteSlots);
            PruneSlotDictionary(remoteLastAppliedInputBySlot, validRemoteSlots);
            PruneSlotDictionary(remoteFrameBySlot, validRemoteSlots);
            PruneSlotDictionary(predictedRemoteFrameBySlot, validRemoteSlots);
            PruneSlotDictionary(highestRemoteInputFrameSeenBySlot, validRemoteSlots);
            PruneSlotDictionary(remoteFrameOffsetBySlot, validRemoteSlots);
            PruneSlotDictionary(remotePredictedInputStreakBySlot, validRemoteSlots);
            pendingRemoteInputSlots.RemoveWhere(slot => !validRemoteSlots.Contains(slot));
        }

        private void PruneSlotDictionary<T>(Dictionary<int, T> valuesBySlot, HashSet<int> validRemoteSlots)
        {
            List<int> staleSlots = new List<int>();
            foreach (int slot in valuesBySlot.Keys)
            {
                if (!validRemoteSlots.Contains(slot))
                {
                    staleSlots.Add(slot);
                }
            }

            for (int i = 0; i < staleSlots.Count; i++)
            {
                valuesBySlot.Remove(staleSlots[i]);
            }
        }

        public void ApplyOnlineSettings(
            int inputDelay,
            bool delayBased,
            int maxRollbackFrames,
            int frameAdvantageLimit,
            bool enableFrameExtension,
            int sleepTimeMicro,
            float frameExtensionLimit,
            int frameExtensionWindow,
            int timeoutFrames,
            int softFramePacingThreshold,
            int maxConsecutiveFrameDrops)
        {
            InputDelay = inputDelay;
            DelayBased = delayBased;
            MaxRollBackFrames = maxRollbackFrames;
            FrameAdvantageLimit = frameAdvantageLimit;
            EnableFrameExtension = enableFrameExtension;
            SleepTimeMicro = sleepTimeMicro;
            FrameExtensionLimit = frameExtensionLimit;
            FrameExtensionWindow = frameExtensionWindow;
            TimeoutFrames = timeoutFrames;
            SoftFramePacingThreshold = softFramePacingThreshold;
            MaxConsecutiveFrameDrops = maxConsecutiveFrameDrops;

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
            foreach (FrameMetadataArray inputs in receivedInputsBySlot.Values)
            {
                inputs.Clear();
            }
            foreach (FrameMetadataArray inputs in usedInputsBySlot.Values)
            {
                inputs.Clear();
            }
            foreach (CircularArray<int> frameAdvantages in remoteFrameAdvantagesBySlot.Values)
            {
                frameAdvantages.Clear();
            }
            remoteLastAppliedInputBySlot.Clear();
            remoteFrameBySlot.Clear();
            predictedRemoteFrameBySlot.Clear();
            highestRemoteInputFrameSeenBySlot.Clear();
            remoteFrameOffsetBySlot.Clear();
            remotePredictedInputStreakBySlot.Clear();
            pendingRemoteInputSlots.Clear();
            pendingRosterFrameOffset = null;

            lastDroppedFrame = -1;
            consecutiveDrop = 0;
            syncFrame = 0;
            predictedRemoteFrame = 0;
            remoteFrame = 0;
            lastRemoteFrameForTimeout = 0;
            remoteFrameStallTicks = 0;
            ResetTimeoutGrace(TransitionStartupTimeoutGraceSeconds);
            packetLossSignal = 0;
            highestRemoteInputFrameSeen = -1;
            lossAwareHoldsThisStreak = 0;
            lastLossAwareHoldFrame = -1;
            lastHashSentFrame = -1;
            firstHashMismatchFrame = -1;
            pendingRemoteHashes.Clear();
            // Initialize to avoid timeout on first frames
            localFrameAdvantage = 0;
            opponentLastAppliedInput = 5;
            opponentPredictedInputStreak = 0;
            totalConsecutiveFrameExtensions = FrameExtensionWindow; // Initialize based on config
            currentFrameExtensionMicro = 0;
            if (matchManager != null) matchManager.sentFrameTimes.Clear(); // Clear ping calculation times if manager exists

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

            EnsureRemoteCollectionsInitialized();
            foreach (int slot in remotePlayerSlots)
            {
                FrameMetadata neutralFrame = new FrameMetadata() { frame = 0, input = 5 };
                for (int i = 0; i <= InputDelay; i++)
                {
                    neutralFrame.frame = i;
                    receivedInputsBySlot[slot].Insert(i, neutralFrame);
                    usedInputsBySlot[slot].Insert(i, neutralFrame);
                }

                for (int i = 0; i < FrameAdvantageArraySize; i++)
                {
                    remoteFrameAdvantagesBySlot[slot].Insert(i, 0);
                }

                remoteLastAppliedInputBySlot[slot] = 5;
                remoteFrameBySlot[slot] = 0;
                predictedRemoteFrameBySlot[slot] = 0;
                highestRemoteInputFrameSeenBySlot[slot] = -1;
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

        private void EnsureRemoteCollectionsInitialized()
        {
            for (int i = 0; i < remotePlayerSlots.Count; i++)
            {
                int slot = remotePlayerSlots[i];
                if (!receivedInputsBySlot.ContainsKey(slot))
                {
                    receivedInputsBySlot[slot] = new FrameMetadataArray(InputArraySize);
                }

                if (!usedInputsBySlot.ContainsKey(slot))
                {
                    usedInputsBySlot[slot] = new FrameMetadataArray(InputArraySize);
                }

                if (!remoteFrameAdvantagesBySlot.ContainsKey(slot))
                {
                    remoteFrameAdvantagesBySlot[slot] = new CircularArray<int>(FrameAdvantageArraySize);
                }

                if (!remoteFrameOffsetBySlot.ContainsKey(slot))
                {
                    remoteFrameOffsetBySlot[slot] = 0;
                }

                if (!remotePredictedInputStreakBySlot.ContainsKey(slot))
                {
                    remotePredictedInputStreakBySlot[slot] = 0;
                }
            }
        }

        private void PrimeRosterInputHistory()
        {
            int currentFrame = GameManager.Instance != null ? GameManager.Instance.frameNumber : 0;
            int startFrame = Mathf.Max(0, currentFrame - InputArraySize + 1);
            int endFrame = currentFrame + InputDelay;

            for (int slotIndex = 0; slotIndex < remotePlayerSlots.Count; slotIndex++)
            {
                int slot = remotePlayerSlots[slotIndex];
                if (!receivedInputsBySlot.TryGetValue(slot, out FrameMetadataArray receivedBySlot)
                    || !usedInputsBySlot.TryGetValue(slot, out FrameMetadataArray usedBySlot))
                {
                    continue;
                }

                for (int frame = startFrame; frame <= endFrame; frame++)
                {
                    FrameMetadata neutralFrame = new FrameMetadata() { frame = frame, input = 5 };
                    if (!receivedBySlot.ContainsKey(frame))
                    {
                        receivedBySlot.Insert(frame, neutralFrame);
                    }

                    if (!usedBySlot.ContainsKey(frame))
                    {
                        usedBySlot.Insert(frame, neutralFrame);
                    }
                }

                if (!remoteLastAppliedInputBySlot.ContainsKey(slot))
                {
                    remoteLastAppliedInputBySlot[slot] = 5;
                }

                remoteFrameBySlot[slot] = Mathf.Max(currentFrame, remoteFrameBySlot.TryGetValue(slot, out int remote) ? remote : 0);
                predictedRemoteFrameBySlot[slot] = Mathf.Max(currentFrame, predictedRemoteFrameBySlot.TryGetValue(slot, out int predicted) ? predicted : 0);

                if (!highestRemoteInputFrameSeenBySlot.ContainsKey(slot))
                {
                    highestRemoteInputFrameSeenBySlot[slot] = -1;
                }
            }

            remoteFrame = remoteFrameBySlot.Count > 0 ? remoteFrameBySlot.Values.Min() : currentFrame;
            predictedRemoteFrame = predictedRemoteFrameBySlot.Count > 0 ? predictedRemoteFrameBySlot.Values.Min() : currentFrame;
        }

        public void MarkAllRemoteSlotsPendingUntilInput()
        {
            EnsureRemoteCollectionsInitialized();
            pendingRemoteInputSlots.Clear();
            pendingRosterFrameOffset = null;
            for (int i = 0; i < remotePlayerSlots.Count; i++)
            {
                pendingRemoteInputSlots.Add(remotePlayerSlots[i]);
            }

            if (pendingRemoteInputSlots.Count > 0)
            {
                ResetTimeoutGrace(TransitionStartupTimeoutGraceSeconds);
            }
        }

        public void RebaseActiveRemoteStreamsForLobbySnapshot(int previousFrame, int snapshotFrame)
        {
            EnsureRemoteCollectionsInitialized();
            int frameDelta = snapshotFrame - previousFrame;
            if (frameDelta <= 0)
            {
                return;
            }

            if (pendingRosterFrameOffset.HasValue)
            {
                pendingRosterFrameOffset += frameDelta;
            }

            for (int i = 0; i < remotePlayerSlots.Count; i++)
            {
                int slot = remotePlayerSlots[i];
                if (pendingRemoteInputSlots.Contains(slot))
                {
                    continue;
                }

                remoteFrameOffsetBySlot[slot] = (remoteFrameOffsetBySlot.TryGetValue(slot, out int offset) ? offset : 0) + frameDelta;
                remoteFrameBySlot[slot] = (remoteFrameBySlot.TryGetValue(slot, out int remote) ? remote : previousFrame) + frameDelta;
                predictedRemoteFrameBySlot[slot] = (predictedRemoteFrameBySlot.TryGetValue(slot, out int predicted) ? predicted : previousFrame) + frameDelta;
            }

            remoteFrame = GetEffectiveRemoteFrame(snapshotFrame);
            predictedRemoteFrame = GetEffectivePredictedRemoteFrame(snapshotFrame);
            ResetTimeoutGrace(TransitionStartupTimeoutGraceSeconds);
            Debug.Log($"[Rollback] Rebased active remote streams for lobby snapshot. PreviousFrame={previousFrame} SnapshotFrame={snapshotFrame} Delta={frameDelta} PendingStreams={pendingRemoteInputSlots.Count}.");
        }

        public bool IsWaitingForInitialRemoteInputStreams()
        {
            EnsureRemoteCollectionsInitialized();
            return usePeerRoster
                && remotePlayerSlots.Count > 0
                && pendingRemoteInputSlots.Count > 0;
        }

        private int GetEffectiveRemoteFrame(int fallbackFrame)
        {
            if (!usePeerRoster)
            {
                return remoteFrame;
            }

            int minFrame = int.MaxValue;
            bool foundActiveSlot = false;
            foreach (KeyValuePair<int, int> entry in remoteFrameBySlot)
            {
                if (pendingRemoteInputSlots.Contains(entry.Key))
                {
                    continue;
                }

                minFrame = Mathf.Min(minFrame, entry.Value);
                foundActiveSlot = true;
            }

            return foundActiveSlot ? minFrame : fallbackFrame;
        }

        private int GetEffectivePredictedRemoteFrame(int fallbackFrame)
        {
            if (!usePeerRoster)
            {
                return predictedRemoteFrame;
            }

            int minFrame = int.MaxValue;
            bool foundActiveSlot = false;
            foreach (KeyValuePair<int, int> entry in predictedRemoteFrameBySlot)
            {
                if (pendingRemoteInputSlots.Contains(entry.Key))
                {
                    continue;
                }

                minFrame = Mathf.Min(minFrame, entry.Value);
                foundActiveSlot = true;
            }

            return foundActiveSlot ? minFrame : fallbackFrame;
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
        RollbackFrames = 0;
        bool foundDesyncedFrame = false;

        // Check for Mismatched Inputs
        for (int i = syncFrame + 1; i <= framesBeforeRollback; i++)
        {
            bool frameVerified = true;

            if (usePeerRoster)
            {
                bool checkedActiveRemoteSlot = false;
                for (int slotIndex = 0; slotIndex < remotePlayerSlots.Count; slotIndex++)
                {
                    int slot = remotePlayerSlots[slotIndex];
                    if (pendingRemoteInputSlots.Contains(slot))
                    {
                        continue;
                    }

                    checkedActiveRemoteSlot = true;
                    FrameMetadataArray receivedBySlot = receivedInputsBySlot[slot];
                    FrameMetadataArray usedBySlot = usedInputsBySlot[slot];
                    bool haveReceived = receivedBySlot.ContainsKey(i);
                    bool haveUsed = usedBySlot.ContainsKey(i);

                    if (haveReceived && haveUsed)
                    {
                        ulong received = receivedBySlot.GetInput(i);
                        ulong used = usedBySlot.GetInput(i);
                        if (received != used)
                        {
                            foundDesyncedFrame = true;
                            frameVerified = false;
                            break;
                        }
                    }
                    else if (haveReceived && !haveUsed)
                    {
                        foundDesyncedFrame = true;
                        frameVerified = false;
                        break;
                    }
                    else
                    {
                        frameVerified = false;
                    }
                }

                if (!checkedActiveRemoteSlot)
                {
                    frameVerified = false;
                }
            }
            else
            {
                bool haveReceived = receivedInputs.ContainsKey(i);
                bool haveUsed = opponentInputs.ContainsKey(i);

                if (haveReceived && haveUsed)
                {
                    ulong received = receivedInputs.GetInput(i);
                    ulong used = opponentInputs.GetInput(i);

                    if (received != used)
                    {
                        foundDesyncedFrame = true;
                        frameVerified = false;
                    }
                }
                else if (haveReceived && !haveUsed)
                {
                    foundDesyncedFrame = true;
                    frameVerified = false;
                }
                else
                {
                    frameVerified = false;
                }
            }

            if (foundDesyncedFrame)
            {
                break;
            }

            if (frameVerified && states[i % StateArraySize].frame == i)
            {
                syncFrame = i;
                if (matchManager != null)
                {
                    int interval = (StressTestController.Instance != null && StressTestController.Instance.enableStateHashing)
                        ? Mathf.Max(1, StressTestController.Instance.hashSendIntervalFrames)
                        : 30;

                    if (IsStableGameplayHashFrame() && syncFrame % interval == 0 && syncFrame != lastHashSentFrame)
                    {
                        lastHashSentFrame = syncFrame;
                        GameState verifiedState = states[syncFrame % StateArraySize];
                        matchManager.SendStateHash(
                            syncFrame,
                            verifiedState.hash,
                            verifiedState.sharedHash,
                            verifiedState.projectileHash,
                            verifiedState.playerHashes ?? new uint[0],
                            verifiedState.playerCoreHashes ?? new uint[0],
                            verifiedState.playerSpellHashes ?? new uint[0]);
                    }
                }
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
        if (!HasStateForFrame(syncFrame))
        {
            Debug.LogWarning($"Rollback skipped because baseline frame {syncFrame} is no longer in history at frame {framesBeforeRollback}. Re-basing rollback history.");
            ResetRollbackBaseline(framesBeforeRollback);
            SaveState();
            return;
        }

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
            if ((!usePeerRoster && opponentNetworkId == 0) || matchManager == null || GameManager.Instance == null) return false;

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
        public bool AllowUpdate(int? frameOverride = null)
        {
            if (GameManager.Instance == null) return false;

            int currentFrame = frameOverride ?? localFrame; // Use cached frame number unless the caller is testing the next simulation frame
            int effectiveRemoteFrame = GetEffectiveRemoteFrame(currentFrame);
            bool timeoutGraceActive = UnityEngine.Time.unscaledTime < timeoutGraceUntilRealtime
                || GameManager.Instance.isTransitioning;

            if (effectiveRemoteFrame != lastRemoteFrameForTimeout)
            {
                lastRemoteFrameForTimeout = effectiveRemoteFrame;
                remoteFrameStallTicks = 0;
            }
            else if (currentFrame > InputDelay + 1 && !timeoutGraceActive)
            {
                remoteFrameStallTicks++;
            }
            else if (timeoutGraceActive)
            {
                remoteFrameStallTicks = 0;
            }

            // Timeout only when remote input packets stop advancing. Recovery pulses should
            // not hide a real network stall, and syncFrame lag is normal rollback behavior.
            if (remoteFrameStallTicks > TimeoutFrames)
            {
                TriggerMatchTimeout(); // Handle timeout disconnect
                return false; // Don't allow update if timed out
            }

            // --- Delay-Based Mode Logic (If enabled) ---
            if (DelayBased)
            {
                // In delay-based mode, wait until inputs for the *current* frame are received
                bool hasAllCurrentFrameInputs = !usePeerRoster
                    ? receivedInputs.ContainsKey(currentFrame)
                    : RemoteSlotsHaveInputForFrame(currentFrame);
                if (!hasAllCurrentFrameInputs)
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


            int maxDropPulse = Mathf.Max(1, MaxConsecutiveFrameDrops);

            // After a scene transition, both clients reset frame sync. Briefly hold for a
            // current-scene remote input, but pulse forward if it has not arrived yet.
            // A hard startup hold can deadlock both sides immediately after scene load.
            if (effectiveRemoteFrame == 0 && currentFrame > InputDelay + 1)
            {
                consecutiveDrop++;
                if (consecutiveDrop <= maxDropPulse)
                {
                    if (lastDroppedFrame != currentFrame)
                    {
                        Debug.LogWarning($"Frame Pace Startup Hold: Local {currentFrame}, Sync {syncFrame}, Remote {effectiveRemoteFrame}. Waiting for current-scene remote input.");
                        lastDroppedFrame = currentFrame;
                    }
                    return false;
                }

                Debug.LogWarning($"Frame Pace Startup Recovery: Local {currentFrame}, Sync {syncFrame}, Remote {effectiveRemoteFrame}. Allowing one startup frame.");
                consecutiveDrop = 0;
                lastDroppedFrame = currentFrame;
                return true;
            }

            int maxPredictionAhead = Mathf.Min(
                StateArraySize - 32,
                Mathf.Max(InputDelay + MaxRollBackFrames + 6, InputDelay + MaxPredictionAheadFrames));
            if (!isRollbackFrame && effectiveRemoteFrame > 0 && currentFrame - effectiveRemoteFrame > maxPredictionAhead)
            {
                if (lastDroppedFrame != currentFrame)
                {
                    Debug.LogWarning($"Frame Pace Prediction Hold: Local {currentFrame}, Remote {effectiveRemoteFrame}, Sync {syncFrame}. Waiting for remote input.");
                    lastDroppedFrame = currentFrame;
                }

                consecutiveDrop++;
                return false;
            }

            // --- Packet-Loss-Aware Soft Hold ---
            // Decay the loss signal each tick. Costs a single int subtract when no loss is happening.
            if (packetLossSignal > 0)
            {
                packetLossSignal = Mathf.Max(0, packetLossSignal - PacketLossDecayPerTick);
            }

            // Reset the per-streak hold counter once we move past the held frame, so repeated
            // loss spikes can each get their own bounded hold.
            if (lastLossAwareHoldFrame >= 0 && currentFrame > lastLossAwareHoldFrame + 1)
            {
                lossAwareHoldsThisStreak = 0;
            }

            if (EnablePacketLossSmoothing
                && packetLossSignal >= PacketLossHoldThreshold
                && lossAwareHoldsThisStreak < MaxLossAwareHolds
                && !isRollbackFrame)
            {
                // We only hold when the frame we are about to simulate would actually have to be
                // predicted (its remote input is missing) AND we are already deep into the
                // prediction window. This keeps the hold from firing when packets arrive in time.
                int frameToSimulate = currentFrame;
                int predictionDepth = frameToSimulate - syncFrame;
                int holdCeiling = Mathf.Max(1, MaxRollBackFrames - 1);
                bool predictingThisFrame = !usePeerRoster
                    ? !receivedInputs.ContainsKey(frameToSimulate)
                    : !RemoteSlotsHaveInputForFrame(frameToSimulate);

                if (predictingThisFrame && predictionDepth >= holdCeiling)
                {
                    lossAwareHoldsThisStreak++;
                    lastLossAwareHoldFrame = currentFrame;
                    // Pay a small fraction of the hold against the signal so it cannot loop forever.
                    packetLossSignal = Mathf.Max(0, packetLossSignal - PacketLossHoldThreshold);
                    return false; // One frame of hold; bounded by MaxLossAwareHolds.
                }
            }
            // --- End Packet-Loss-Aware Soft Hold ---

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

            int inputCount = Mathf.Max(2, GameManager.Instance.playerCount);
            ulong[] inputs = new ulong[inputCount];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = 5UL;
            }

            int localIdx = GameManager.Instance.localPlayerIndex;
            if (localIdx >= 0 && localIdx < inputs.Length)
            {
                inputs[localIdx] = localInput;
            }

            if (usePeerRoster)
            {
                for (int i = 0; i < remotePlayerSlots.Count; i++)
                {
                    int slot = remotePlayerSlots[i];
                    if (slot >= 0 && slot < inputs.Length)
                    {
                        inputs[slot] = PredictRemoteInput(slot, frame);
                    }
                }
            }
            else
            {
                ulong remoteInput = PredictOpponentInput(frame);
                int remoteIdx = GameManager.Instance.remotePlayerIndex;
                if (remoteIdx >= 0 && remoteIdx < inputs.Length)
                {
                    inputs[remoteIdx] = remoteInput;
                }
                else
                {
                    Debug.LogError($"Invalid player indices during SynchronizeInput! Local: {localIdx}, Remote: {remoteIdx}");
                }
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
                opponentPredictedInputStreak = 0;
                // Store the *actual* input we are using for this frame's simulation
                opponentInputs.Insert(frame, new FrameMetadata() { frame = frame, input = actualInput });
                return actualInput;
            }
            else
            {
                // Simple prediction: reuse the last known input
                // Store the *predicted* input we are using for this frame's simulation
                opponentPredictedInputStreak++;
                ulong predicted = DecayPredictedInput(opponentLastAppliedInput, opponentPredictedInputStreak);
                opponentInputs.Insert(frame, new FrameMetadata() { frame = frame, input = predicted });
                return predicted;
            }
        }

        private ulong PredictRemoteInput(int slot, int frame)
        {
            FrameMetadataArray receivedBySlot = receivedInputsBySlot[slot];
            FrameMetadataArray usedBySlot = usedInputsBySlot[slot];

            if (receivedBySlot.ContainsKey(frame))
            {
                ulong actualInput = receivedBySlot.GetInput(frame);
                remoteLastAppliedInputBySlot[slot] = actualInput;
                remotePredictedInputStreakBySlot[slot] = 0;
                usedBySlot.Insert(frame, new FrameMetadata() { frame = frame, input = actualInput });
                return actualInput;
            }

            int predictedStreak = remotePredictedInputStreakBySlot.TryGetValue(slot, out int streak) ? streak + 1 : 1;
            remotePredictedInputStreakBySlot[slot] = predictedStreak;
            ulong predicted = remoteLastAppliedInputBySlot.TryGetValue(slot, out ulong lastInput) ? lastInput : 5UL;
            predicted = DecayPredictedInput(predicted, predictedStreak);
            usedBySlot.Insert(frame, new FrameMetadata() { frame = frame, input = predicted });
            return predicted;
        }

        private ulong DecayPredictedInput(ulong input, int predictedFrameStreak)
        {
            ulong decayedInput = DecayPredictedCodeButton(input, predictedFrameStreak);
            int codeButtonState = (int)((input >> 8) & 0b11UL);
            if (codeButtonState is (int)ButtonState.Pressed or (int)ButtonState.Held)
            {
                return decayedInput;
            }

            if (predictedFrameStreak <= Mathf.Max(0, DirectionPredictionHoldFrames))
            {
                return decayedInput;
            }

            // Direction is stored in the low byte. Keep buttons intact, but stop
            // predicting movement so short taps don't turn into long remote runs.
            return (decayedInput & ~0xFFUL) | 5UL;
        }

        private ulong DecayPredictedCodeButton(ulong input, int predictedFrameStreak)
        {
            if (predictedFrameStreak <= Mathf.Max(0, CodeButtonPredictionHoldFrames))
            {
                return input;
            }

            ButtonState codeState = (ButtonState)((input >> 8) & 0b11UL);
            if (codeState is ButtonState.Pressed or ButtonState.Held)
            {
                return SetButtonState(input, 0, ButtonState.Released);
            }

            if (codeState == ButtonState.Released)
            {
                return SetButtonState(input, 0, ButtonState.None);
            }

            return input;
        }

        private ulong SetButtonState(ulong input, int buttonIndex, ButtonState state)
        {
            int shift = 8 + buttonIndex * 2;
            ulong mask = 0b11UL << shift;
            return (input & ~mask) | (((ulong)state & 0b11UL) << shift);
        }

        private bool RemoteSlotsHaveInputForFrame(int frame)
        {
            for (int i = 0; i < remotePlayerSlots.Count; i++)
            {
                int slot = remotePlayerSlots[i];
                if (pendingRemoteInputSlots.Contains(slot))
                {
                    continue;
                }

                if (!receivedInputsBySlot.ContainsKey(slot) || !receivedInputsBySlot[slot].ContainsKey(frame))
                {
                    return false;
                }
            }

            return true;
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
        int playerCount = Mathf.Max(0, GameManager.Instance.playerCount);
        uint[] playerHashes = new uint[playerCount];
        uint[] playerCoreHashes = new uint[playerCount];
        uint[] playerSpellHashes = new uint[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            if (GameManager.Instance.players[i] != null)
            {
                playerCoreHashes[i] = ComputePlayerCoreHash(GameManager.Instance.players[i]);
                playerSpellHashes[i] = ComputePlayerSpellHash(GameManager.Instance.players[i]);
                playerHashes[i] = ComputePlayerHash(GameManager.Instance.players[i]);
            }
        }
        uint player0Hash = playerCount > 0 ? playerHashes[0] : 0;
        uint player1Hash = playerCount > 1 ? playerHashes[1] : 0;
        uint player0CoreHash = playerCount > 0 ? playerCoreHashes[0] : 0;
        uint player1CoreHash = playerCount > 1 ? playerCoreHashes[1] : 0;
        uint player0SpellHash = playerCount > 0 ? playerSpellHashes[0] : 0;
        uint player1SpellHash = playerCount > 1 ? playerSpellHashes[1] : 0;
        uint hash = ComputeCompositeHash(sharedHash, projectileHash, playerHashes);

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
            player1SpellHash = player1SpellHash,
            playerHashes = playerHashes,
            playerCoreHashes = playerCoreHashes,
            playerSpellHashes = playerSpellHashes
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

    private bool HasStateForFrame(int frame)
    {
        int index = frame % StateArraySize;
        return states[index].frame == frame && states[index].state != null && states[index].state.Length > 0;
    }

    public void OnRemoteStateHash(int frame, uint remoteHash, uint remoteSharedHash, uint remoteProjectileHash, uint remotePlayer0Hash, uint remotePlayer1Hash, uint remotePlayer0CoreHash, uint remotePlayer1CoreHash, uint remotePlayer0SpellHash, uint remotePlayer1SpellHash)
    {
        OnRemoteStateHash(frame, remoteHash, remoteSharedHash, remoteProjectileHash,
            new uint[] { remotePlayer0Hash, remotePlayer1Hash },
            new uint[] { remotePlayer0CoreHash, remotePlayer1CoreHash },
            new uint[] { remotePlayer0SpellHash, remotePlayer1SpellHash });
    }

    public void OnRemoteStateHash(int frame, uint remoteHash, uint remoteSharedHash, uint remoteProjectileHash, uint[] remotePlayerHashes, uint[] remotePlayerCoreHashes, uint[] remotePlayerSpellHashes)
    {
        if (frame > syncFrame)
        {
            pendingRemoteHashes[frame] = new PendingRemoteHash()
            {
                frame = frame,
                remoteHash = remoteHash,
                remoteSharedHash = remoteSharedHash,
                remoteProjectileHash = remoteProjectileHash,
                remotePlayer0Hash = remotePlayerHashes != null && remotePlayerHashes.Length > 0 ? remotePlayerHashes[0] : 0,
                remotePlayer1Hash = remotePlayerHashes != null && remotePlayerHashes.Length > 1 ? remotePlayerHashes[1] : 0,
                remotePlayer0CoreHash = remotePlayerCoreHashes != null && remotePlayerCoreHashes.Length > 0 ? remotePlayerCoreHashes[0] : 0,
                remotePlayer1CoreHash = remotePlayerCoreHashes != null && remotePlayerCoreHashes.Length > 1 ? remotePlayerCoreHashes[1] : 0,
                remotePlayer0SpellHash = remotePlayerSpellHashes != null && remotePlayerSpellHashes.Length > 0 ? remotePlayerSpellHashes[0] : 0,
                remotePlayer1SpellHash = remotePlayerSpellHashes != null && remotePlayerSpellHashes.Length > 1 ? remotePlayerSpellHashes[1] : 0
            };
            return;
        }

        EvaluateRemoteStateHash(frame, remoteHash, remoteSharedHash, remoteProjectileHash, remotePlayerHashes, remotePlayerCoreHashes, remotePlayerSpellHashes);
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
                new uint[] { pending.remotePlayer0Hash, pending.remotePlayer1Hash },
                new uint[] { pending.remotePlayer0CoreHash, pending.remotePlayer1CoreHash },
                new uint[] { pending.remotePlayer0SpellHash, pending.remotePlayer1SpellHash });
        }
    }

    private void EvaluateRemoteStateHash(int frame, uint remoteHash, uint remoteSharedHash, uint remoteProjectileHash, uint[] remotePlayerHashes, uint[] remotePlayerCoreHashes, uint[] remotePlayerSpellHashes)
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
            uint remotePlayer0Hash = remotePlayerHashes != null && remotePlayerHashes.Length > 0 ? remotePlayerHashes[0] : 0;
            uint remotePlayer1Hash = remotePlayerHashes != null && remotePlayerHashes.Length > 1 ? remotePlayerHashes[1] : 0;
            uint remotePlayer0CoreHash = remotePlayerCoreHashes != null && remotePlayerCoreHashes.Length > 0 ? remotePlayerCoreHashes[0] : 0;
            uint remotePlayer1CoreHash = remotePlayerCoreHashes != null && remotePlayerCoreHashes.Length > 1 ? remotePlayerCoreHashes[1] : 0;
            uint remotePlayer0SpellHash = remotePlayerSpellHashes != null && remotePlayerSpellHashes.Length > 0 ? remotePlayerSpellHashes[0] : 0;
            uint remotePlayer1SpellHash = remotePlayerSpellHashes != null && remotePlayerSpellHashes.Length > 1 ? remotePlayerSpellHashes[1] : 0;
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

    private uint ComputeCompositeHash(uint sharedHash, uint projectileHash, uint[] playerHashes)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(memoryStream))
        {
            bw.Write(sharedHash);
            bw.Write(projectileHash);
            bw.Write(playerHashes?.Length ?? 0);
            if (playerHashes != null)
            {
                for (int i = 0; i < playerHashes.Length; i++)
                {
                    bw.Write(playerHashes[i]);
                }
            }
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
    // BestoNet's original FPSLock extends local frame time when a client is ahead.
    // This local wait is intentionally outside serialized simulation state.

    /// <summary>
    /// Applies the active BestoNet FPSLock-style local frame extension.
    /// </summary>
    public void ExtendFrame()
    {
        if (!EnableFrameExtension || currentFrameExtensionMicro <= 0)
        {
            return;
        }

        int extensionWindow = Mathf.Max(1, FrameExtensionWindow);
        if (totalConsecutiveFrameExtensions < extensionWindow)
        {
            totalConsecutiveFrameExtensions++;
            WaitMicroseconds(currentFrameExtensionMicro);
            return;
        }

        currentFrameExtensionMicro = 0;
    }

    /// <summary>
    /// Starts a short local frame extension window when this client is running ahead.
    /// This mirrors BestoNet's FPSLock.SetFrameExtension behavior without touching simulation state.
    /// </summary>
    /// <param name="frameAdvantageDifference">The calculated frame advantage difference.</param>
    public void StartFrameExtensions(float frameAdvantageDifference)
    {
        if (!EnableFrameExtension || frameAdvantageDifference <= FrameExtensionLimit)
        {
            return;
        }

        int extensionWindow = Mathf.Max(1, FrameExtensionWindow);
        if (totalConsecutiveFrameExtensions < extensionWindow)
        {
            return;
        }

        currentFrameExtensionMicro = Mathf.Max(0, SleepTimeMicro);
        totalConsecutiveFrameExtensions = 0;
    }

    private static void WaitMicroseconds(int microseconds)
    {
        if (microseconds <= 0)
        {
            return;
        }

        int milliseconds = microseconds / 1000;
        if (milliseconds > 0)
        {
            Thread.Sleep(milliseconds);
        }

        int remainingMicroseconds = microseconds - (milliseconds * 1000);
        if (remainingMicroseconds <= 0)
        {
            return;
        }

        long targetTicks = (DiagnosticsStopwatch.Frequency * remainingMicroseconds) / 1000000L;
        long startTicks = DiagnosticsStopwatch.GetTimestamp();
        while (DiagnosticsStopwatch.GetTimestamp() - startTicks < targetTicks)
        {
            Thread.SpinWait(10);
        }
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
            int effectiveRemoteFrame = GetEffectiveRemoteFrame(localFrame);
            int effectivePredictedRemoteFrame = GetEffectivePredictedRemoteFrame(localFrame);
            if (effectiveRemoteFrame == 0 && localFrame < 60)
            {
                frameAdvantageDifference = 0;
                return true;
            }
        localFrameAdvantage = localFrame - effectivePredictedRemoteFrame; // Calculate current advantage
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
            if (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive && frame <= syncFrame)
            {
                return;
            }

            // Optional: Add logging if input for a frame is received multiple times with different values (potential issue)
            // if (receivedInputs.ContainsKey(frame) && receivedInputs.GetInput(frame) != input) {
            //     Debug.LogWarning($"Received conflicting input for frame {frame}. Old: {receivedInputs.GetInput(frame)}, New: {input}");
            // }
            bool isNewFrame = !receivedInputs.ContainsKey(frame);
            receivedInputs.Insert(frame, new FrameMetadata() { frame = frame, input = input });

            if (EnablePacketLossSmoothing && isNewFrame)
            {
                // The Bestonet input transport (UnreliableNoDelay + resend window) means a frame
                // arriving "fresh" after a higher one has already arrived is a recovered loss,
                // and a brand-new highest frame that skips frames over the previous high-water
                // mark indicates one or more dropped earlier packets we haven't recovered yet.
                if (highestRemoteInputFrameSeen < 0)
                {
                    highestRemoteInputFrameSeen = frame;
                }
                else if (frame > highestRemoteInputFrameSeen)
                {
                    int skipped = frame - highestRemoteInputFrameSeen - 1;
                    highestRemoteInputFrameSeen = frame;
                    if (skipped > 0)
                    {
                        // Multiple gaps in a single forward jump weight more heavily.
                        packetLossSignal = Mathf.Min(packetLossSignal + skipped * PacketLossEventWeight, PacketLossHoldThreshold * 4);
                    }
                }
                else
                {
                    // A late packet that filled an earlier gap. Treat as a recovered loss event.
                    packetLossSignal = Mathf.Min(packetLossSignal + PacketLossEventWeight, PacketLossHoldThreshold * 4);
                }
            }
            // Update syncFrame if this input confirms a previously predicted frame?
            // Handled within RollbackEvent check now.
        }

        public void SetRemoteInput(int slot, int frame, ulong input)
        {
            SetRemoteInput(slot, frame, input, frame);
        }

        public void SetRemoteInput(int slot, int frame, ulong input, int alignmentFrame)
        {
            if (usePeerRoster
                && pendingRemoteInputSlots.Contains(slot)
                && GameManager.Instance != null
                && GameManager.Instance.isOnlineMatchActive
                && alignmentFrame <= syncFrame)
            {
                return;
            }

            int frameOffset = AlignRemoteFrameForSlot(slot, alignmentFrame);
            int adjustedFrame = frame + frameOffset;

            if (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive && adjustedFrame <= syncFrame)
            {
                return;
            }

            EnsureRemoteCollectionsInitialized();
            if (!receivedInputsBySlot.ContainsKey(slot))
            {
                receivedInputsBySlot[slot] = new FrameMetadataArray(InputArraySize);
                usedInputsBySlot[slot] = new FrameMetadataArray(InputArraySize);
                remoteFrameAdvantagesBySlot[slot] = new CircularArray<int>(FrameAdvantageArraySize);
                remoteLastAppliedInputBySlot[slot] = 5;
                remoteFrameBySlot[slot] = 0;
                predictedRemoteFrameBySlot[slot] = 0;
                highestRemoteInputFrameSeenBySlot[slot] = -1;
                remoteFrameOffsetBySlot[slot] = 0;
            }

            bool isNewFrame = !receivedInputsBySlot[slot].ContainsKey(adjustedFrame);
            receivedInputsBySlot[slot].Insert(adjustedFrame, new FrameMetadata() { frame = adjustedFrame, input = input });

            if (EnablePacketLossSmoothing && isNewFrame)
            {
                int highestSeen = highestRemoteInputFrameSeenBySlot.TryGetValue(slot, out int seen) ? seen : -1;
                if (highestSeen < 0)
                {
                    highestRemoteInputFrameSeenBySlot[slot] = adjustedFrame;
                }
                else if (adjustedFrame > highestSeen)
                {
                    int skipped = adjustedFrame - highestSeen - 1;
                    highestRemoteInputFrameSeenBySlot[slot] = adjustedFrame;
                    if (skipped > 0)
                    {
                        packetLossSignal = Mathf.Min(packetLossSignal + skipped * PacketLossEventWeight, PacketLossHoldThreshold * 4);
                    }
                }
                else
                {
                    packetLossSignal = Mathf.Min(packetLossSignal + PacketLossEventWeight, PacketLossHoldThreshold * 4);
                }
            }
        }

        private int AlignRemoteFrameForSlot(int slot, int frame)
        {
            if (!pendingRemoteInputSlots.Remove(slot))
            {
                return remoteFrameOffsetBySlot.TryGetValue(slot, out int existingOffset) ? existingOffset : 0;
            }

            int currentFrame = localFrame;
            int frameOffset;
            if (pendingRosterFrameOffset.HasValue)
            {
                frameOffset = pendingRosterFrameOffset.Value;
            }
            else
            {
                frameOffset = currentFrame - frame;
                if (pendingRemoteInputSlots.Count > 0)
                {
                    pendingRosterFrameOffset = frameOffset;
                }
            }

            remoteFrameOffsetBySlot[slot] = frameOffset;
            remoteFrameBySlot[slot] = Mathf.Max(currentFrame, remoteFrameBySlot.TryGetValue(slot, out int remote) ? remote : 0);
            predictedRemoteFrameBySlot[slot] = Mathf.Max(currentFrame, predictedRemoteFrameBySlot.TryGetValue(slot, out int predicted) ? predicted : 0);
            remoteFrame = GetEffectiveRemoteFrame(currentFrame);
            predictedRemoteFrame = GetEffectivePredictedRemoteFrame(currentFrame);
            ResetRollbackBaseline(currentFrame);
            PrimeRosterInputHistory();
            SaveState();
            if (pendingRemoteInputSlots.Count == 0)
            {
                pendingRosterFrameOffset = null;
            }
            Debug.Log($"[Rollback] Remote slot {slot} input stream active at frame {frame}. FrameOffset={frameOffset}. Rebased lobby rollback baseline at {currentFrame}.");
            return frameOffset;
        }

        /// <summary> Stores the frame advantage reported by the remote client. Called by MatchMessageManager. </summary>
        public void SetRemoteFrameAdvantage(int frame, int advantage)
        {
            remoteFrameAdvantages.Insert(frame, advantage);
        }

        public void SetRemoteFrameAdvantage(int slot, int frame, int advantage)
        {
            EnsureRemoteCollectionsInitialized();
            if (!remoteFrameAdvantagesBySlot.ContainsKey(slot))
            {
                remoteFrameAdvantagesBySlot[slot] = new CircularArray<int>(FrameAdvantageArraySize);
            }

            int adjustedFrame = frame + (remoteFrameOffsetBySlot.TryGetValue(slot, out int frameOffset) ? frameOffset : 0);
            remoteFrameAdvantagesBySlot[slot].Insert(adjustedFrame, advantage);
            remoteFrameAdvantages.Insert(adjustedFrame, advantage);
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

        public void SetRemoteFrame(int slot, int frame)
        {
            int adjustedFrame = frame + (remoteFrameOffsetBySlot.TryGetValue(slot, out int frameOffset) ? frameOffset : 0);
            remoteFrameBySlot[slot] = adjustedFrame;
            int pingMs = matchManager != null ? matchManager.GetPingForSlot(slot) : 200;
            int pingFrames = (pingMs * 60 + 1999) / 2000;
            predictedRemoteFrameBySlot[slot] = adjustedFrame + pingFrames;
            remoteFrame = GetEffectiveRemoteFrame(adjustedFrame);
            predictedRemoteFrame = GetEffectivePredictedRemoteFrame(adjustedFrame + pingFrames);
        }

        public void ResetTimeoutGrace(float graceSeconds)
        {
            remoteFrameStallTicks = 0;
            lastRemoteFrameForTimeout = remoteFrame;
            timeoutGraceUntilRealtime = Mathf.Max(
                timeoutGraceUntilRealtime,
                UnityEngine.Time.unscaledTime + Mathf.Max(0f, graceSeconds)
            );
        }

        /// <summary> Calculates the average frame advantage difference over a window. </summary>
        public float GetAverageFrameAdvantage()
        {
            if (usePeerRoster && remoteFrameAdvantagesBySlot.Count > 0)
            {
                long rosterLocalSum = 0;
                for (int i = 0; i < FrameAdvantageArraySize; i++)
                {
                    rosterLocalSum += localFrameAdvantages.GetValues()[i];
                }

                float rosterLocalAverage = (float)rosterLocalSum / FrameAdvantageArraySize;
                float worstDifference = 0f;
                bool checkedActiveSlot = false;
                foreach (KeyValuePair<int, CircularArray<int>> entry in remoteFrameAdvantagesBySlot)
                {
                    int slot = entry.Key;
                    if (pendingRemoteInputSlots.Contains(slot))
                    {
                        continue;
                    }

                    if (highestRemoteInputFrameSeenBySlot.TryGetValue(slot, out int highestSeen) && highestSeen < 0)
                    {
                        continue;
                    }

                    long rosterRemoteSum = 0;
                    CircularArray<int> remoteAdvantages = entry.Value;
                    int[] rosterRemoteValues = remoteAdvantages.GetValues();
                    for (int i = 0; i < FrameAdvantageArraySize; i++)
                    {
                        rosterRemoteSum += rosterRemoteValues[i];
                    }

                    float rosterRemoteAverage = (float)rosterRemoteSum / FrameAdvantageArraySize;
                    worstDifference = Mathf.Max(worstDifference, Mathf.Max(0f, rosterLocalAverage - rosterRemoteAverage));
                    checkedActiveSlot = true;
                }

                if (checkedActiveSlot)
                {
                    return worstDifference;
                }
            }

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
