using System;
using System.IO;
using Steamworks; 
using Steamworks.Data;
using UnityEngine;
using BestoNet.Collections; // For CircularArray


    public class MatchMessageManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static MatchMessageManager Instance { get; private set; }
        // --- End Singleton ---

        [Header("Network Settings")]
        [SerializeField] private int MATCH_MESSAGE_CHANNEL = 0; // Steam recommends channel 0 for game data
        [SerializeField] private P2PSend SEND_TYPE = P2PSend.UnreliableNoDelay; // Best for fast input delivery in rollback

        [Header("Ping Calculation")]
        // Stores timestamp when a packet for a specific frame was sent
        public CircularArray<float> sentFrameTimes = new CircularArray<float>(RollbackManager.InputArraySize); // Match size with RollbackManager
        public int Ping { get; private set; } = 100; // Default/initial ping estimate in ms

        // Keep track of the opponent's Steam ID
        private SteamId opponentSteamId;

        // Keep track if the manager is active
        private bool isRunning = false;

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
        }

        /// <summary>
        /// Initializes the manager for a new match.
        /// </summary>
        /// <param name="opponentId">The SteamId of the opponent.</param>
        public void StartMatch(SteamId opponentId)
        {
            if (opponentId.IsValid)
            {
                this.opponentSteamId = opponentId;
                this.isRunning = true;
                Ping = 100; // Reset ping estimate
                sentFrameTimes.Clear(); // Clear old timestamps
                Debug.Log($"MatchMessageManager started. Opponent: {opponentSteamId}");
            }
            else
            {
                Debug.LogError("MatchMessageManager: Invalid opponent SteamId provided.");
                this.isRunning = false;
            }
        }

        /// <summary>
        /// Stops the manager at the end of a match.
        /// </summary>
        public void StopMatch()
        {
            this.isRunning = false;
            this.opponentSteamId = default; // Clear opponent ID
            Debug.Log("MatchMessageManager stopped.");
        }

        void Update()
        {
            if (!isRunning || !SteamClient.IsValid) // Check if Steam is active
            {
                return;
            }

            // --- Receive Packets ---
            // Process all available packets on the channel
            while (SteamNetworking.IsP2PPacketAvailable(MATCH_MESSAGE_CHANNEL))
            {
                P2Packet? packet = SteamNetworking.ReadP2PPacket(MATCH_MESSAGE_CHANNEL);
                if (packet.HasValue && packet.Value.SteamId == opponentSteamId) // Ensure packet is from the opponent
                {
                    try
                    {
                        ProcessPacket(packet.Value.Data);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error processing packet: {e}");
                    }
                }
                // Discard packets not from the opponent if necessary
            }
            // --- End Receive Packets ---
        }

        /// <summary>
        /// Processes the raw byte data received from the opponent.
        /// </summary>
        private void ProcessPacket(byte[] messageData)
        {
            // Ensure RollbackManager exists before processing
            if (RollbackManager.Instance == null) return;

            try
            {
                using (MemoryStream memoryStream = new MemoryStream(messageData))
                {
                    using (BinaryReader reader = new BinaryReader(memoryStream))
                    {
                        // Read packet type (byte: 0=Input, 1=ACK)
                        byte packetType = reader.ReadByte();

                        if (packetType == 0) // Input Packet
                        {
                            // --- Deserialize Input Data ---
                            int remoteFrameAdvantage = reader.ReadInt32();
                            int startFrame = reader.ReadInt32(); // First frame number in this bundle
                            int inputCount = reader.ReadByte(); // How many frames of input are included

                            // Debug.Log($"Received Input Packet: StartFrame={startFrame}, Count={inputCount}, RemoteAdv={remoteFrameAdvantage}");


                            for (int i = 0; i < inputCount; i++)
                            {
                                int frame = startFrame + i;
                                ulong input = reader.ReadUInt64();

                                // Send input to RollbackManager if it hasn't received it yet
                                if (!RollbackManager.Instance.receivedInputs.ContainsKey(frame))
                                {
                                    RollbackManager.Instance.SetOpponentInput(frame, input);
                                    // Send an ACK back immediately for this frame
                                    SendMessageACK(frame);
                                }

                                // Update RollbackManager with the latest frame info from the opponent
                                if (i == inputCount - 1) // Only use info from the last input in the bundle
                                {
                                    RollbackManager.Instance.SetRemoteFrameAdvantage(frame, remoteFrameAdvantage);
                                    RollbackManager.Instance.SetRemoteFrame(frame); // Update latest known remote frame
                                }
                            }
                            // --- End Deserialize Input ---
                        }
                        else if (packetType == 1) // ACK Packet
                        {
                            // --- Process ACK for Ping Calculation ---
                            int ackFrame = reader.ReadInt32();
                            ProcessACK(ackFrame);
                            // --- End Process ACK ---
                        }
                        else
                        {
                            Debug.LogWarning($"Received unknown packet type: {packetType}");
                        }
                    }
                }
            }
            catch (EndOfStreamException e)
            {
                Debug.LogError($"Packet Read Error (End of Stream): Likely corrupted or mismatched packet structure. {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"General Packet Read Error: {e}");
            }
        }

        /// <summary>
        /// Processes an ACK message to calculate ping.
        /// </summary>
        private void ProcessACK(int frame)
        {
            if (sentFrameTimes.ContainsKey(frame)) // Use ContainsKey if added to CircularArray, otherwise check validity
            {
                float sentTime = sentFrameTimes.Get(frame);
                if (sentTime > 0) // Basic validity check
                {
                    // Calculate Round Trip Time (RTT) in milliseconds
                    int rttMs = Mathf.RoundToInt((Time.unscaledTime - sentTime) * 1000f);
                    // Simple smoothing (e.g., lerp or moving average) is often good here
                    Ping = (int)Mathf.Lerp(Ping, rttMs, 0.1f); // Smooth ping value
                    // Optional: Remove frame from sentFrameTimes to prevent reprocessing? Depends on CircularArray behavior.
                    // sentFrameTimes.Remove(frame); // Or set value to 0/negative
                }
            }
            else
            {
                // Debug.LogWarning($"Received ACK for frame {frame} but no sent time recorded.");
            }
        }

        /// <summary>
        /// Sends local inputs to the opponent. Bundles multiple frames if possible.
        /// </summary>
        /// <param name="targetFrame">The frame number the input corresponds to (current frame + input delay).</param>
        /// <param name="input">The input value for the target frame.</param>
        public void SendInputs(ulong targetFrameInput) // Simplified: Send a bundle starting from oldest unsent up to targetFrame
        {
            // Ensure RollbackManager and opponent ID are valid
            if (RollbackManager.Instance == null || !opponentSteamId.IsValid || !isRunning)
            {
                return;
            }

            // Determine the range of frames to send
            // Send inputs from the last acknowledged frame (or slightly before) up to the current target frame.
            // Simple approach: Send the last N frames (e.g., MaxRollBackFrames + InputDelay).
            int currentLocalFrame = GameManager.Instance.frameNumber; // Get current frame
            int latestTargetFrame = currentLocalFrame + RollbackManager.Instance.InputDelay;
            // Determine the first frame to send in the bundle (e.g., up to MaxRollbackFrames back from latest target)
            int firstFrameToSend = Math.Max(0, latestTargetFrame - RollbackManager.Instance.MaxRollBackFrames - RollbackManager.Instance.InputDelay); // Heuristic range

            int inputCount = latestTargetFrame - firstFrameToSend + 1;
            if (inputCount <= 0) return; // Nothing to send yet

            // Limit bundle size if necessary (e.g., MTU limit, though unlikely for small inputs)
            const int MaxInputsPerPacket = 15; // Example limit
            if (inputCount > MaxInputsPerPacket)
            {
                firstFrameToSend = latestTargetFrame - MaxInputsPerPacket + 1;
                inputCount = MaxInputsPerPacket;
            }


            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(memoryStream))
                    {
                        // --- Serialize Input Data ---
                        writer.Write((byte)0); // Packet Type: 0 = Input
                        writer.Write(RollbackManager.Instance.localFrameAdvantage); // Include local frame advantage
                        writer.Write(firstFrameToSend); // Starting frame number of the bundle
                        writer.Write((byte)inputCount); // Number of inputs included

                        // Record send time for the *latest* frame in the bundle for ping calculation
                        sentFrameTimes.Insert(latestTargetFrame, Time.unscaledTime);

                        // Write input for each frame in the range
                        for (int i = 0; i < inputCount; i++)
                        {
                            int frame = firstFrameToSend + i;
                            // Get input from RollbackManager's client buffer
                            ulong inputToSend = RollbackManager.Instance.clientInputs.ContainsKey(frame)
                                                ? RollbackManager.Instance.clientInputs.GetInput(frame)
                                                : 0UL; // Send neutral if missing (shouldn't happen often)
                            writer.Write(inputToSend);
                        }
                        // --- End Serialize Input ---

                        byte[] data = memoryStream.ToArray();

                        // Send packet via Steam P2P
                        Result result = SteamNetworking.SendP2PPacket(opponentSteamId, data, data.Length, channel: MATCH_MESSAGE_CHANNEL, sendType: SEND_TYPE);
                        if (result != Result.OK && result != Result.Ignored)
                        { // Ignored is okay if sending too fast
                            Debug.LogWarning($"Failed to send P2P packet: {result}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending inputs: {e}");
            }
        }


        /// <summary>
        /// Sends an acknowledgment message for a received input frame.
        /// </summary>
        /// <param name="frameToAck">The frame number being acknowledged.</param>
        public void SendMessageACK(int frameToAck)
        {
            if (!opponentSteamId.IsValid || !isRunning)
            {
                return;
            }

            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(memoryStream))
                    {
                        writer.Write((byte)1); // Packet Type: 1 = ACK
                        writer.Write(frameToAck); // Frame being acknowledged

                        byte[] data = memoryStream.ToArray();
                        SteamNetworking.SendP2PPacket(opponentSteamId, data, data.Length, channel: MATCH_MESSAGE_CHANNEL, sendType: SEND_TYPE);
                        // No need to check result as much for ACKs, they are less critical than inputs
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending ACK: {e}");
            }
        }

    } 