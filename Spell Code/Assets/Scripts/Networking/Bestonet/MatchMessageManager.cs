using System;
using System.IO;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using BestoNet.Collections;

public class OnlineMatchPeerInfo
{
    public SteamId SteamId;
    public int PlayerSlot;
}

public class OnlineMatchRoster
{
    public SteamId HostSteamId;
    public int LocalPlayerSlot;
    public readonly List<OnlineMatchPeerInfo> Peers = new List<OnlineMatchPeerInfo>();

    public int PlayerCount => Peers.Count;
}

public class MatchMessageManager : MonoBehaviour
{
    public static MatchMessageManager Instance { get; private set; }

    [Header("Network Settings")]
    [SerializeField] private int MATCH_MESSAGE_CHANNEL = 0;
    [SerializeField] private P2PSend INPUT_SEND_TYPE = P2PSend.UnreliableNoDelay;
    [SerializeField] private P2PSend ACK_SEND_TYPE = P2PSend.Reliable;
    [SerializeField] private int EXTRA_RESEND_FRAMES = 10;
    [SerializeField] private int MAX_INPUTS_PER_PACKET = 25;
    private const byte PACKET_TYPE_READY = 2;
    private const byte PACKET_TYPE_MATCH_START = 3;
    private const byte PACKET_TYPE_LOBBY_READY = 10; // For lobby->gameplay transition
    private const byte PACKET_TYPE_SCENE_READY = 11; // For post-scene-load transition barrier
    private const byte PACKET_TYPE_SHOP_TRANSITION = 13;
    private const byte PACKET_TYPE_SHOP_READY = 14; // For shop->gameplay transition
    private const byte PACKET_TYPE_SEED = 12;
    private const byte PACKET_TYPE_STATE_HASH = 20;
    private const byte PACKET_TYPE_STAGE_SELECT = 30;
    private const byte PACKET_TYPE_SETTINGS = 40;

    [Header("Ping Calculation")]
    public CircularArray<float> sentFrameTimes = new CircularArray<float>(RollbackManager.InputArraySize);
    public int Ping { get; private set; } = 100;

    private sealed class PeerRuntimeState
    {
        public SteamId SteamId;
        public int PlayerSlot;
        public bool ReadyReceived;
        public int HighestRemoteFrameSeen = -1;
        public CircularArray<float> SentFrameTimes = new CircularArray<float>(RollbackManager.InputArraySize);
        public int Ping = 100;
    }

    // Legacy helper for 1v1 callers that still ask for "the opponent".
    private SteamId opponentSteamId;
    public SteamId GetOpponentSteamId() => opponentSteamId;
    private readonly Dictionary<SteamId, PeerRuntimeState> peerStates = new Dictionary<SteamId, PeerRuntimeState>();
    public int ConnectedPeerCount => peerStates.Count;

    private bool isRunning = false;
    private bool localReadySent = false;

    private struct PendingOutboundPacket
    {
        public SteamId peerId;
        public byte[] data;
        public P2PSend sendType;
        public float deliverTime;
    }

    private struct PendingInboundPacket
    {
        public SteamId peerId;
        public byte[] data;
        public float deliverTime;
    }

    private readonly List<PendingOutboundPacket> outboundQueue = new List<PendingOutboundPacket>();
    private readonly List<PendingInboundPacket> inboundQueue = new List<PendingInboundPacket>();

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

        SteamNetworking.OnP2PSessionRequest = OnP2PSessionRequest;
        SteamNetworking.OnP2PConnectionFailed = OnP2PConnectionFailed;
    }

    private void OnEnable()
    {
        SteamNetworking.OnP2PSessionRequest = OnP2PSessionRequest;
        SteamNetworking.OnP2PConnectionFailed = OnP2PConnectionFailed;
    }

    private void OnDisable()
    {
        SteamNetworking.OnP2PSessionRequest = null;
        SteamNetworking.OnP2PConnectionFailed = null;
    }

    private void OnP2PSessionRequest(SteamId steamId)
    {
        //Debug.Log($"P2P Session request from {steamId}");

        if (peerStates.ContainsKey(steamId) || opponentSteamId == default)
        {
            if (!peerStates.ContainsKey(steamId) && opponentSteamId == default)
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

        if (!isRunning || !peerStates.ContainsKey(steamId))
        {
            return;
        }

        GameManager.Instance?.StopMatch($"Connection lost to player {steamId.Value}");
    }

    void Update()
    {
        if (!isRunning || !SteamClient.IsValid)
        {
            return;
        }

        if (SteamNetworking.OnP2PSessionRequest == null)
        {
            Debug.LogWarning("OnP2PSessionRequest callback is NULL!");
        }

        while (SteamNetworking.IsP2PPacketAvailable(MATCH_MESSAGE_CHANNEL))
        {
            P2Packet? packet = SteamNetworking.ReadP2PPacket(MATCH_MESSAGE_CHANNEL);
            if (packet.HasValue && peerStates.ContainsKey(packet.Value.SteamId))
            {
                try
                {
                    if (IsChaosActive() && StressTestController.Instance.affectInbound && StressTestController.Instance.ShouldDropInbound())
                    {
                        continue;
                    }

                    if (IsChaosActive() && StressTestController.Instance.affectInbound)
                    {
                        EnqueueInbound(packet.Value.SteamId, packet.Value.Data, GetChaosDelaySeconds());
                    }
                    else
                    {
                        ProcessPacket(packet.Value.SteamId, packet.Value.Data);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing packet: {e}");
                }
            }
            else
            {
                if (packet.HasValue)
                {
                    Debug.LogWarning($"Received packet from unknown SteamId: {packet.Value.SteamId}");
                }
            }
        }

        ProcessOutboundQueue();
        ProcessInboundQueue();
    }

    public void SendSeed(int seed)
    {
        if (peerStates.Count == 0 || !isRunning) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write(PACKET_TYPE_SEED);
            writer.Write(seed);
            byte[] data = ms.ToArray();
            BroadcastPacket(data, P2PSend.Reliable);
            Debug.Log($"Sent seed: {seed}");
        }
    }

    public void SendRollbackSettings()
    {
        if (peerStates.Count == 0 || !isRunning) return;
        if (RollbackManager.Instance == null) return;

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_SETTINGS);
                writer.Write(RollbackManager.Instance.InputDelay);
                writer.Write(RollbackManager.Instance.DelayBased);
                writer.Write(RollbackManager.Instance.MaxRollBackFrames);
                writer.Write(RollbackManager.Instance.FrameAdvantageLimit);
                writer.Write(RollbackManager.Instance.FrameExtensionLimit);
                writer.Write(RollbackManager.Instance.FrameExtensionWindow);
                writer.Write(RollbackManager.Instance.TimeoutFrames);

                byte[] data = memoryStream.ToArray();
                BroadcastPacket(data, P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending rollback settings: {e}");
        }
    }

    public void SendReadySignal()
    {
        if (peerStates.Count == 0 || !isRunning)
        {
            Debug.LogWarning("Cannot send ready signal - not connected");
            return;
        }

        if (localReadySent)
        {
            //Debug.Log("Ready signal already sent - skipping");
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(PACKET_TYPE_READY);
                    writer.Write(SteamClient.SteamId.Value);

                    byte[] data = memoryStream.ToArray();

                    bool success = BroadcastPacket(data, P2PSend.Reliable);

                    if (success)
                    {
                        localReadySent = true;
                        //Debug.Log($"Sent READY signal to {opponentSteamId}");
                    }
                    else
                    {
                        Debug.LogError("Failed to send READY signal");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending ready signal: {e}");
        }
    }

    public void SendMatchStartConfirm()
    {
        if (peerStates.Count == 0 || !isRunning)
            return;

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(PACKET_TYPE_MATCH_START);

                    byte[] data = memoryStream.ToArray();

                    BroadcastPacket(data, P2PSend.Reliable);

                    //Debug.Log("Sent MATCH START confirmation");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending match start: {e}");
        }
    }

    // Send lobby ready for gameplay signal
    public void SendLobbyReadySignal(int transitionId)
    {
        if (peerStates.Count == 0 || !isRunning)
        {
            Debug.LogWarning("Cannot send lobby ready signal - not connected");
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(PACKET_TYPE_LOBBY_READY);
                    writer.Write(transitionId);

                    byte[] data = memoryStream.ToArray();

                    bool success = BroadcastPacket(data, P2PSend.Reliable);

                    if (success)
                    {
                        //Debug.Log($"Sent LOBBY_READY signal to {opponentSteamId}");
                    }
                    else
                    {
                        Debug.LogError("Failed to send LOBBY_READY signal");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending lobby ready signal: {e}");
        }
    }

    // Send shop ready for gameplay signal
    public void SendShopReadySignal(int transitionId)
    {
        if (peerStates.Count == 0 || !isRunning)
        {
            Debug.LogWarning("Cannot send shop ready signal - not connected");
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(PACKET_TYPE_SHOP_READY);
                    writer.Write(transitionId);
                    byte[] data = memoryStream.ToArray();

                    bool success = BroadcastPacket(data, P2PSend.Reliable);

                    if (success)
                    {
                        //Debug.Log($"Sent SHOP_READY signal to {opponentSteamId}");
                    }
                    else
                    {
                        Debug.LogError("Failed to send SHOP_READY signal");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending shop ready signal: {e}");
        }
    }

    public void SendSceneTransitionReadySignal(int transitionId)
    {
        if (peerStates.Count == 0 || !isRunning)
        {
            Debug.LogWarning("Cannot send scene transition ready signal - not connected");
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(PACKET_TYPE_SCENE_READY);
                    writer.Write(transitionId);
                    writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneTypeCode() : (byte)0);
                    writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneSignature() : 0);
                    byte[] data = memoryStream.ToArray();

                    bool success = BroadcastPacket(data, P2PSend.Reliable);

                    if (!success)
                    {
                        Debug.LogError("Failed to send SCENE_READY signal");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending scene transition ready signal: {e}");
        }
    }

    public void SendShopTransitionSignal(int transitionId)
    {
        if (peerStates.Count == 0 || !isRunning)
        {
            Debug.LogWarning("Cannot send shop transition signal - not connected");
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(PACKET_TYPE_SHOP_TRANSITION);
                    writer.Write(transitionId);
                    writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneTypeCode() : (byte)0);
                    writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneSignature() : 0);
                    byte[] data = memoryStream.ToArray();

                    bool success = BroadcastPacket(data, P2PSend.Reliable);
                    if (!success)
                    {
                        Debug.LogError("Failed to send SHOP_TRANSITION signal");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending shop transition signal: {e}");
        }
    }

    public void StartMatch(SteamId opponentId)
    {
        OnlineMatchRoster roster = new OnlineMatchRoster
        {
            HostSteamId = GameManager.Instance != null && GameManager.Instance.localPlayerIndex == 0 ? SteamClient.SteamId : opponentId,
            LocalPlayerSlot = GameManager.Instance != null ? GameManager.Instance.localPlayerIndex : 0
        };

        roster.Peers.Add(new OnlineMatchPeerInfo
        {
            SteamId = SteamClient.SteamId,
            PlayerSlot = roster.LocalPlayerSlot
        });

        int fallbackRemoteSlot = roster.LocalPlayerSlot == 0 ? 1 : 0;
        roster.Peers.Add(new OnlineMatchPeerInfo
        {
            SteamId = opponentId,
            PlayerSlot = fallbackRemoteSlot
        });

        StartMatch(roster);
    }

    public void StartMatch(OnlineMatchRoster roster)
    {
        if (roster != null && roster.PlayerCount > 0)
        {
            this.isRunning = true;
            Ping = 100;
            sentFrameTimes.Clear();
            outboundQueue.Clear();
            inboundQueue.Clear();
            peerStates.Clear();

            foreach (OnlineMatchPeerInfo peer in roster.Peers)
            {
                if (!peer.SteamId.IsValid || peer.SteamId == SteamClient.SteamId)
                {
                    continue;
                }

                PeerRuntimeState state = new PeerRuntimeState
                {
                    SteamId = peer.SteamId,
                    PlayerSlot = peer.PlayerSlot
                };
                peerStates[peer.SteamId] = state;

                if (!opponentSteamId.IsValid)
                {
                    opponentSteamId = peer.SteamId;
                }
            }

            ResetReadyFlags();
            SteamNetworking.AllowP2PPacketRelay(true);

            //Debug.Log($"MatchMessageManager started. Opponent: {opponentSteamId}");

            SendHandshake();
        }
        else
        {
            Debug.LogError("MatchMessageManager: Invalid online roster provided.");
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
                    writer.Write((byte)0xFF);
                    writer.Write("HANDSHAKE");

                    byte[] data = memoryStream.ToArray();

                    bool success = BroadcastPacket(data, P2PSend.Reliable);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending handshake: {e}");
        }
    }

    public void StopMatch()
    {
        this.isRunning = false;
        this.opponentSteamId = default;
        this.localReadySent = false;
        this.Ping = 100;
        this.sentFrameTimes.Clear();
        this.outboundQueue.Clear();
        this.inboundQueue.Clear();
        this.peerStates.Clear();
        //Debug.Log("MatchMessageManager stopped.");
    }

    public int GetAveragePeerPing()
    {
        if (peerStates.Count == 0)
        {
            return Ping;
        }

        int total = 0;
        foreach (PeerRuntimeState state in peerStates.Values)
        {
            total += state.Ping;
        }

        return Mathf.RoundToInt((float)total / peerStates.Count);
    }

    public int GetMaxPeerPing()
    {
        int maxPing = Ping;
        foreach (PeerRuntimeState state in peerStates.Values)
        {
            maxPing = Mathf.Max(maxPing, state.Ping);
        }

        return maxPing;
    }

    private void ProcessPacket(SteamId senderSteamId, byte[] messageData)
    {
        if (RollbackManager.Instance == null)
        {
            Debug.LogWarning("RollbackManager is null, cannot process packet");
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPacketReceived();
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream(messageData))
            {
                using (BinaryReader reader = new BinaryReader(memoryStream))
                {
                    byte packetType = reader.ReadByte();
                    if (!peerStates.TryGetValue(senderSteamId, out PeerRuntimeState senderState))
                    {
                        return;
                    }

                    // Handle handshake
                    if (packetType == 0xFF)
                    {
                        string message = reader.ReadString();
                        //Debug.Log($"Received handshake: {message}");
                        SendHandshake();
                        return;
                    }

                    // Handle ready signal
                    if (packetType == PACKET_TYPE_READY)
                    {
                        reader.ReadUInt64();

                        if (senderState.ReadyReceived)
                        {
                            return;
                        }

                        senderState.ReadyReceived = true;

                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.OnOpponentReady(senderSteamId);
                        }

                        if (!localReadySent)
                        {
                            SendReadySignal();
                        }

                        return;
                    }

                    // Handle match start confirmation
                    if (packetType == PACKET_TYPE_MATCH_START)
                    {
                        //Debug.Log("Received MATCH START confirmation");
                        return;
                    }

                    if (packetType == PACKET_TYPE_SEED)
                    {
                        int receivedSeed = reader.ReadInt32();
                        Debug.Log($"Received seed: {receivedSeed}");
                        GameManager.Instance.InitializeWithSeed(receivedSeed);
                        GameManager.Instance.StartLobbySimulation(); // Client triggers lobby start after receiving seed
                        return;
                    }

                    if (packetType == PACKET_TYPE_SETTINGS)
                    {
                        int inputDelay = reader.ReadInt32();
                        bool delayBased = reader.ReadBoolean();
                        int maxRollback = reader.ReadInt32();
                        int frameAdvLimit = reader.ReadInt32();
                        float frameExtensionLimit = reader.ReadSingle();
                        int frameExtensionWindow = reader.ReadInt32();
                        int timeoutFrames = reader.ReadInt32();

                        RollbackManager.Instance.ApplyOnlineSettings(
                            inputDelay,
                            delayBased,
                            maxRollback,
                            frameAdvLimit,
                            frameExtensionLimit,
                            frameExtensionWindow,
                            timeoutFrames);
                        return;
                    }

                    if (packetType == PACKET_TYPE_STATE_HASH)
                    {
                        int frame = reader.ReadInt32();
                        uint hash = reader.ReadUInt32();
                        uint sharedHash = reader.ReadUInt32();
                        uint projectileHash = reader.ReadUInt32();
                        int playerCount = reader.ReadInt32();
                        uint[] playerHashes = new uint[playerCount];
                        uint[] playerCoreHashes = new uint[playerCount];
                        uint[] playerSpellHashes = new uint[playerCount];
                        for (int i = 0; i < playerCount; i++)
                        {
                            playerHashes[i] = reader.ReadUInt32();
                        }
                        for (int i = 0; i < playerCount; i++)
                        {
                            playerCoreHashes[i] = reader.ReadUInt32();
                        }
                        for (int i = 0; i < playerCount; i++)
                        {
                            playerSpellHashes[i] = reader.ReadUInt32();
                        }
                        RollbackManager.Instance.OnRemoteStateHash(senderState.PlayerSlot, frame, hash, sharedHash, projectileHash, playerHashes, playerCoreHashes, playerSpellHashes);
                        return;
                    }
                    
                    if (packetType == PACKET_TYPE_STAGE_SELECT)
                    {
                        int transitionId = reader.ReadInt32();
                        byte packetSceneType = reader.ReadByte();
                        int packetSceneSignature = reader.ReadInt32();
                        int stageIndex = reader.ReadInt32();
                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.HandleOnlineStageSelect(transitionId, packetSceneType, packetSceneSignature, stageIndex);
                        }
                        return;
                    }

                    // Handle lobby ready for gameplay
                    if (packetType == PACKET_TYPE_LOBBY_READY)
                    {
                        if (GameManager.Instance != null)
                        {
                            int transitionId = reader.ReadInt32();
                            GameManager.Instance.OnOpponentReadyForGameplayFromLobby(senderSteamId, transitionId);
                        }
                        return;
                    }

                    if (packetType == PACKET_TYPE_SHOP_READY)
                    {
                        if (GameManager.Instance != null)
                        {
                            int transitionId = reader.ReadInt32();
                            GameManager.Instance.OnOpponentReadyForGameplayFromShop(senderSteamId, transitionId);
                        }
                        return;
                    }

                    if (packetType == PACKET_TYPE_SCENE_READY)
                    {
                        if (GameManager.Instance != null)
                        {
                            int transitionId = reader.ReadInt32();
                            byte sceneType = reader.ReadByte();
                            int sceneSignature = reader.ReadInt32();
                            GameManager.Instance.OnOpponentSceneTransitionReady(senderSteamId, transitionId, sceneType, sceneSignature);
                        }
                        return;
                    }

                    if (packetType == PACKET_TYPE_SHOP_TRANSITION)
                    {
                        if (GameManager.Instance != null)
                        {
                            int transitionId = reader.ReadInt32();
                            byte sceneType = reader.ReadByte();
                            int sceneSignature = reader.ReadInt32();
                            GameManager.Instance.OnOpponentShopTransition(senderSteamId, transitionId, sceneType, sceneSignature);
                        }
                        return;
                    }

                    // Handle input packets
                    if (packetType == 0)
                    {
                        if (GameManager.Instance != null && GameManager.Instance.isWaitingForOpponent)
                        {
                            Debug.LogWarning("Received input packet during wait state - ignoring");
                            return;
                        }

                        int packetSceneSignature = reader.ReadInt32();
                        int remoteFrameAdvantage = reader.ReadInt32();
                        int startFrame = reader.ReadInt32();
                        int inputCount = reader.ReadByte();

                        if (GameManager.Instance != null)
                        {
                            int currentSceneSignature = GameManager.Instance.GetNetworkSceneSignature();
                            if (packetSceneSignature != currentSceneSignature)
                            {
                                int currentLocalFrame = GameManager.Instance.frameNumber;
                                Debug.LogWarning($"Ignoring stale input packet after scene transition. LocalFrame={currentLocalFrame}, StartFrame={startFrame}, Count={inputCount}, PacketScene={packetSceneSignature}, LocalScene={currentSceneSignature}");
                                return;
                            }
                        }

                        //Debug.Log($"Received Input Packet: StartFrame={startFrame}, Count={inputCount}");

                        for (int i = 0; i < inputCount; i++)
                        {
                            int frame = startFrame + i;
                            ulong input = reader.ReadUInt64();

                            if (!RollbackManager.Instance.HasRemoteInput(senderState.PlayerSlot, frame))
                            {
                                RollbackManager.Instance.SetRemoteInput(senderState.PlayerSlot, frame, input);
                                SendMessageACK(frame, senderSteamId);
                            }

                            if (i == inputCount - 1)
                            {
                                if (frame > senderState.HighestRemoteFrameSeen)
                                {
                                    senderState.HighestRemoteFrameSeen = frame;
                                    RollbackManager.Instance.SetRemoteFrameAdvantage(senderState.PlayerSlot, frame, remoteFrameAdvantage);
                                    RollbackManager.Instance.SetRemoteFrame(senderState.PlayerSlot, frame);
                                }
                            }
                        }
                    }
                    else if (packetType == 1) // ACK
                    {
                        int ackFrame = reader.ReadInt32();
                        ProcessACK(senderSteamId, ackFrame);
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
            Debug.LogError($"Packet processing error: {e}");
        }
    }

    public void ResetReadyFlags()
    {
        localReadySent = false;
        foreach (PeerRuntimeState state in peerStates.Values)
        {
            state.ReadyReceived = false;
            state.HighestRemoteFrameSeen = -1;
        }
    }

    public void ResetFrameSyncForSceneTransition()
    {
        sentFrameTimes.Clear();
        foreach (PeerRuntimeState state in peerStates.Values)
        {
            state.HighestRemoteFrameSeen = -1;
            state.SentFrameTimes.Clear();
        }
        outboundQueue.Clear();
        inboundQueue.Clear();
    }

    private void ProcessACK(SteamId senderSteamId, int frame)
    {
        float sentTime = sentFrameTimes.Get(frame);
        if (peerStates.TryGetValue(senderSteamId, out PeerRuntimeState state))
        {
            sentTime = state.SentFrameTimes.Get(frame);
        }

        if (sentTime > 0f)
        {
            int rttMs = Mathf.RoundToInt((Time.unscaledTime - sentTime) * 1000f);
            Ping = (int)Mathf.Lerp(Ping, rttMs, 0.1f);
            sentFrameTimes.Insert(frame, 0f);
            if (peerStates.TryGetValue(senderSteamId, out state))
            {
                state.Ping = (int)Mathf.Lerp(state.Ping, rttMs, 0.1f);
                state.SentFrameTimes.Insert(frame, 0f);
            }
        }
    }

    public void SendInputs()
    {
        if (RollbackManager.Instance == null || peerStates.Count == 0 || !isRunning)
        {
            Debug.LogError($"SendInputs BLOCKED: RBM={RollbackManager.Instance != null}, " +
                      $"PeerCount={peerStates.Count}, isRunning={isRunning}");
            return;
        }

        int currentLocalFrame = GameManager.Instance.frameNumber;
        int latestTargetFrame = currentLocalFrame + RollbackManager.Instance.InputDelay;
        int resendWindow = RollbackManager.Instance.MaxRollBackFrames + RollbackManager.Instance.InputDelay + Mathf.Max(14, EXTRA_RESEND_FRAMES);
        int firstFrameToSend = Math.Max(0, latestTargetFrame - resendWindow);

        int inputCount = latestTargetFrame - firstFrameToSend + 1;
        if (inputCount <= 0) return;

        int maxInputsPerPacket = Mathf.Max(32, MAX_INPUTS_PER_PACKET);
        if (inputCount > maxInputsPerPacket)
        {
            firstFrameToSend = latestTargetFrame - maxInputsPerPacket + 1;
            inputCount = maxInputsPerPacket;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write((byte)0);
                    writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneSignature() : 0);
                    writer.Write(RollbackManager.Instance.localFrameAdvantage);
                    writer.Write(firstFrameToSend);
                    writer.Write((byte)inputCount);

                    sentFrameTimes.Insert(latestTargetFrame, Time.unscaledTime);
                    foreach (PeerRuntimeState state in peerStates.Values)
                    {
                        state.SentFrameTimes.Insert(latestTargetFrame, Time.unscaledTime);
                    }

                    for (int i = 0; i < inputCount; i++)
                    {
                        int frame = firstFrameToSend + i;
                        ulong inputToSend = RollbackManager.Instance.clientInputs.ContainsKey(frame)
                                            ? RollbackManager.Instance.clientInputs.GetInput(frame)
                                            : 5UL;
                        writer.Write(inputToSend);
                    }

                    byte[] data = memoryStream.ToArray();
                    int dataSize = data.Length;

                    bool success = BroadcastPacket(data, INPUT_SEND_TYPE);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending inputs: {e}");
        }
    }

    public void SendMessageACK(int frameToAck)
    {
        SendMessageACK(frameToAck, opponentSteamId);
    }

    public void SendMessageACK(int frameToAck, SteamId peerId)
    {
        if (!peerId.IsValid || !isRunning)
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write((byte)1);
                    writer.Write(frameToAck);

                    byte[] data = memoryStream.ToArray();
                    int dataSize = data.Length;
                    bool success = SendPacketToPeer(peerId, data, ACK_SEND_TYPE);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending ACK: {e}");
        }
    }

    public void SendStateHash(int frame, uint hash, uint sharedHash, uint projectileHash, uint[] playerHashes, uint[] playerCoreHashes, uint[] playerSpellHashes)
    {
        if (peerStates.Count == 0 || !isRunning)
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_STATE_HASH);
                writer.Write(frame);
                writer.Write(hash);
                writer.Write(sharedHash);
                writer.Write(projectileHash);
                int playerCount = playerHashes != null ? playerHashes.Length : 0;
                writer.Write(playerCount);
                for (int i = 0; i < playerCount; i++)
                {
                    writer.Write(playerHashes[i]);
                }
                for (int i = 0; i < playerCount; i++)
                {
                    writer.Write(playerCoreHashes != null && i < playerCoreHashes.Length ? playerCoreHashes[i] : 0);
                }
                for (int i = 0; i < playerCount; i++)
                {
                    writer.Write(playerSpellHashes != null && i < playerSpellHashes.Length ? playerSpellHashes[i] : 0);
                }

                byte[] data = memoryStream.ToArray();
                BroadcastPacket(data, P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending state hash: {e}");
        }
    }

    public void SendStageSelect(int transitionId, int stageIndex)
    {
        if (peerStates.Count == 0 || !isRunning)
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_STAGE_SELECT);
                writer.Write(transitionId);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneTypeCode() : (byte)0);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneSignature() : 0);
                writer.Write(stageIndex);

                byte[] data = memoryStream.ToArray();
                BroadcastPacket(data, P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending stage select: {e}");
        }
    }

    private bool BroadcastPacket(byte[] data, P2PSend sendType)
    {
        if (peerStates.Count == 0 || !isRunning)
        {
            return false;
        }

        bool success = true;
        foreach (SteamId peerId in peerStates.Keys)
        {
            success &= SendPacketToPeer(peerId, data, sendType);
        }
        return success;
    }

    private bool SendPacketToPeer(SteamId peerId, byte[] data, P2PSend sendType)
    {
        if (!peerId.IsValid || !isRunning)
        {
            return false;
        }

        if (IsChaosActive() && StressTestController.Instance.affectOutbound)
        {
            if (StressTestController.Instance.ShouldDropOutbound())
            {
                return false;
            }

            EnqueueOutbound(peerId, data, sendType, GetChaosDelaySeconds());
            return true;
        }

        return SteamNetworking.SendP2PPacket(
            peerId,
            data,
            data.Length,
            MATCH_MESSAGE_CHANNEL,
            sendType
        );
    }

    private bool IsChaosActive()
    {
        return StressTestController.Instance != null &&
               StressTestController.Instance.IsActiveOnline &&
               StressTestController.Instance.enableNetworkChaos;
    }

    private float GetChaosDelaySeconds()
    {
        int delayMs = StressTestController.Instance.GetNetworkDelayMs();
        return Mathf.Max(0f, delayMs / 1000f);
    }

    private void EnqueueOutbound(SteamId peerId, byte[] data, P2PSend sendType, float delaySeconds)
    {
        byte[] copy = new byte[data.Length];
        Buffer.BlockCopy(data, 0, copy, 0, data.Length);

        PendingOutboundPacket packet = new PendingOutboundPacket
        {
            peerId = peerId,
            data = copy,
            sendType = sendType,
            deliverTime = Time.unscaledTime + delaySeconds
        };

        if (StressTestController.Instance.ShouldReorder() && outboundQueue.Count > 0)
        {
            outboundQueue.Insert(0, packet);
        }
        else
        {
            outboundQueue.Add(packet);
        }
    }

    private void EnqueueInbound(SteamId peerId, byte[] data, float delaySeconds)
    {
        byte[] copy = new byte[data.Length];
        Buffer.BlockCopy(data, 0, copy, 0, data.Length);

        PendingInboundPacket packet = new PendingInboundPacket
        {
            peerId = peerId,
            data = copy,
            deliverTime = Time.unscaledTime + delaySeconds
        };

        if (StressTestController.Instance.ShouldReorder() && inboundQueue.Count > 0)
        {
            inboundQueue.Insert(0, packet);
        }
        else
        {
            inboundQueue.Add(packet);
        }
    }

    private void ProcessOutboundQueue()
    {
        if (outboundQueue.Count == 0) return;

        float now = Time.unscaledTime;
        for (int i = outboundQueue.Count - 1; i >= 0; i--)
        {
            if (outboundQueue[i].deliverTime <= now)
            {
                PendingOutboundPacket packet = outboundQueue[i];
                outboundQueue.RemoveAt(i);
                SteamNetworking.SendP2PPacket(
                    packet.peerId,
                    packet.data,
                    packet.data.Length,
                    MATCH_MESSAGE_CHANNEL,
                    packet.sendType
                );
            }
        }
    }

    private void ProcessInboundQueue()
    {
        if (inboundQueue.Count == 0) return;

        float now = Time.unscaledTime;
        for (int i = inboundQueue.Count - 1; i >= 0; i--)
        {
            if (inboundQueue[i].deliverTime <= now)
            {
                PendingInboundPacket packet = inboundQueue[i];
                inboundQueue.RemoveAt(i);
                ProcessPacket(packet.peerId, packet.data);
            }
        }
    }
}
