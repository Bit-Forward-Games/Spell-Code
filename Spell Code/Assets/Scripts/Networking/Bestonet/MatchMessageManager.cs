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
        [SerializeField] private P2PSend INPUT_SEND_TYPE = P2PSend.UnreliableNoDelay; // Best for fast input delivery in rollback
        [SerializeField] private P2PSend ACK_SEND_TYPE = P2PSend.Reliable;

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

            SteamNetworking.OnP2PSessionRequest = OnP2PSessionRequest;
            SteamNetworking.OnP2PConnectionFailed = OnP2PConnectionFailed;
        // --- End Singleton Setup ---
        }

        private void OnEnable()
        {
            // Subscribe to P2P session request callback
            SteamNetworking.OnP2PSessionRequest = OnP2PSessionRequest;
            SteamNetworking.OnP2PConnectionFailed = OnP2PConnectionFailed;
        }

        private void OnDisable()
        {
            // Unsubscribe to prevent memory leaks
            SteamNetworking.OnP2PSessionRequest = null;
            SteamNetworking.OnP2PConnectionFailed = null;
        }

        private void OnP2PSessionRequest(SteamId steamId)
        {
            Debug.Log($"P2P Session request from {steamId}");

            // Accept if it's from our opponent (or accept all for debugging)
            if (steamId == opponentSteamId || opponentSteamId == default)
            {
                Debug.Log($"Accepting P2P session from {steamId}");
                // In Facepunch, you accept by sending a packet back
                // The library handles acceptance automatically when you send

                // If we didn't know the opponent yet, set them now
                if (opponentSteamId == default)
                {
                    opponentSteamId = steamId;
                }
            }
            else
            {
                Debug.LogWarning($"Rejecting P2P session from unknown user {steamId}");
            }
        }

        private void OnP2PConnectionFailed(SteamId steamId, P2PSessionError error)
        {
            Debug.LogError($"P2P Connection failed with {steamId}: {error}");
        }

    void Update()
    {
        if (!isRunning || !SteamClient.IsValid) // Check if Steam is active
        {
            return;
        }

        // Check if callbacks are registered
        if (SteamNetworking.OnP2PSessionRequest == null)
        {
            Debug.LogWarning("OnP2PSessionRequest callback is NULL!");
        }

        // --- Receive Packets ---
        // Process all available packets on the channel
        while (SteamNetworking.IsP2PPacketAvailable(MATCH_MESSAGE_CHANNEL))
        {
            //Debug.Log("Packet received");
            P2Packet? packet = SteamNetworking.ReadP2PPacket(MATCH_MESSAGE_CHANNEL);
            //Debug.Log($"Received Packet from {packet.Value.SteamId}");
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
            else
            {
                Debug.LogWarning($"Received packet from unknown SteamId: {packet.Value.SteamId}");
            }
            // Discard packets not from the opponent if necessary
        }
        // --- End Receive Packets ---
    }

    /// <summary>
    /// Initializes the manager for a new match.
    /// </summary>
    /// <param name="opponentId">The SteamId of the opponent.</param>
        public void StartMatch(SteamId opponentId)
        {
            Debug.Log($"StartMatch called with opponent: {opponentId}");

            if (opponentId.IsValid)
            {
                this.opponentSteamId = opponentId;
                this.isRunning = true;
                Ping = 100;
                sentFrameTimes.Clear();

                // Allow P2P relay to increase connection success rate
                SteamNetworking.AllowP2PPacketRelay(true);

                Debug.Log($"MatchMessageManager started. Opponent: {opponentSteamId}");

                // Send initial handshake packet
                SendHandshake();
            }
            else
            {
                Debug.LogError("MatchMessageManager: Invalid opponent SteamId provided.");
                this.isRunning = false;
            }
        }

        private void SendHandshake()
        {
            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(memoryStream))
                    {
                        writer.Write((byte)0xFF); // Handshake packet type
                        writer.Write("HANDSHAKE"); // Handshake message

                        byte[] data = memoryStream.ToArray();

                        bool success = SteamNetworking.SendP2PPacket(
                            opponentSteamId,
                            data,
                            data.Length,
                            MATCH_MESSAGE_CHANNEL,
                            P2PSend.Reliable
                        );

                        //Debug.Log($"Sent handshake packet: {success}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending handshake: {e}");
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


        /// <summary>
        /// Processes the raw byte data received from the opponent.
        /// </summary>
        private void ProcessPacket(byte[] messageData)
        {
            //Debug.Log($"Processing Packet of size {messageData.Length} bytes");

            if (RollbackManager.Instance == null)
            {
                Debug.LogWarning("RollbackManager is null, cannot process packet");
                return;
            }

            try
            {
                using (MemoryStream memoryStream = new MemoryStream(messageData))
                {
                    using (BinaryReader reader = new BinaryReader(memoryStream))
                    {
                        byte packetType = reader.ReadByte();

                        // Handle handshake
                        if (packetType == 0xFF)
                        {
                            string message = reader.ReadString();
                            //Debug.Log($"Received handshake: {message}");
                            // Send handshake back
                            SendHandshake();
                            return;
                        }

                        if (packetType == 0) // Input Packet
                        {
                            int remoteFrameAdvantage = reader.ReadInt32();
                            int startFrame = reader.ReadInt32();
                            int inputCount = reader.ReadByte();

                            Debug.Log($"Received Input Packet: StartFrame={startFrame}, Count={inputCount}");

                            for (int i = 0; i < inputCount; i++)
                            {
                                int frame = startFrame + i;
                                ulong input = reader.ReadUInt64();

                                Debug.Log($"[ProcessPacket] Frame={frame}, Input={input}");

                                if (!RollbackManager.Instance.receivedInputs.ContainsKey(frame))
                                {
                                    RollbackManager.Instance.SetOpponentInput(frame, input);
                                    SendMessageACK(frame);
                                }

                                if (i == inputCount - 1)
                                {
                                    RollbackManager.Instance.SetRemoteFrameAdvantage(frame, remoteFrameAdvantage);
                                    RollbackManager.Instance.SetRemoteFrame(frame);
                                }
                            }
                        }
                        else if (packetType == 1) // ACK Packet
                        {
                            int ackFrame = reader.ReadInt32();
                            ProcessACK(ackFrame);
                        }
                        else
                        {
                            Debug.LogWarning($"Received unknown packet type: {packetType}");
                        }
                    }
                }
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
            // --- Check if a valid timestamp exists ---
            // Get the timestamp stored at the index for this frame.
            float sentTime = sentFrameTimes.Get(frame);

            // Check if the retrieved time is positive (meaning we likely stored a valid Time.unscaledTime)
            if (sentTime > 0f)
            // --- End Check ---
            {
                // Calculate Round Trip Time (RTT) in milliseconds
                int rttMs = Mathf.RoundToInt((Time.unscaledTime - sentTime) * 1000f);

                // Simple smoothing
                Ping = (int)Mathf.Lerp(Ping, rttMs, 0.1f);

                // Invalidate the timestamp in the array to prevent reprocessing
                // (Set it back to 0 or a negative value)
                sentFrameTimes.Insert(frame, 0f); // Overwrite with 0
            }
            else
            {
                // ACK received for a frame we didn't record sending or already processed.
                // Debug.LogWarning($"Received ACK for frame {frame} but no valid sent time recorded ({sentTime}).");
            }
        }

        /// <summary>
        /// Sends local inputs to the opponent. Bundles multiple frames if possible.
        /// </summary>
        /// <param name="targetFrame">The frame number the input corresponds to (current frame + input delay).</param>
        /// <param name="input">The input value for the target frame.</param>
        public void SendInputs() // Send a bundle starting from oldest unsent up to targetFrame
            {
                // Ensure RollbackManager and opponent ID are valid
                if (RollbackManager.Instance == null || !opponentSteamId.IsValid || !isRunning)
                {
                    Debug.LogError($"SendInputs BLOCKED: RBM={RollbackManager.Instance != null}, " +
                              $"OpponentValid={opponentSteamId.IsValid}, isRunning={isRunning}");
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

                Debug.Log($"[SendInputs] CurrentFrame={currentLocalFrame}, " +
                $"LatestTarget={latestTargetFrame}, OpponentID={opponentSteamId}");

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
                            int dataSize = data.Length;

                            //Debug.Log($"Sending Input Bundle: StartFrame={firstFrameToSend}, Count={inputCount}");
                        
                            //Debug.LogWarning($"Sending Packet of size {dataSize} bytes to {opponentSteamId}");
                        // Send packet via Steam P2P
                        bool success = SteamNetworking.SendP2PPacket( // Store return value as bool
                                    opponentSteamId,
                                    data,
                                    dataSize,
                                    MATCH_MESSAGE_CHANNEL,
                                    INPUT_SEND_TYPE
                                );
                            if (success)
                            {
                                // Packet sent successfully
                                //Debug.LogError($"P2P packet sent successfully to {opponentSteamId}.");
                            }
                            else
                            {
                                // Ignored result isn't directly available with bool,
                                // but false generally indicates a more serious failure.
                                Debug.LogError($"Failed to send P2P packet (returned false).");
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
                        int dataSize = data.Length;
                    bool success = SteamNetworking.SendP2PPacket( // Store return value as bool
                            opponentSteamId,
                            data,
                            dataSize,
                            MATCH_MESSAGE_CHANNEL,
                            ACK_SEND_TYPE
                        );
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