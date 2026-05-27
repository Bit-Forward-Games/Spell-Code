using System;
using System.Collections.Generic;
using System.IO;
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
    public List<OnlineMatchPeerInfo> Peers = new List<OnlineMatchPeerInfo>();

    public int PlayerCount => Peers?.Count ?? 0;

    public bool TryGetSteamIdForSlot(int slot, out SteamId steamId)
    {
        if (Peers != null)
        {
            for (int i = 0; i < Peers.Count; i++)
            {
                if (Peers[i] != null && Peers[i].PlayerSlot == slot)
                {
                    steamId = Peers[i].SteamId;
                    return true;
                }
            }
        }

        steamId = default;
        return false;
    }

    public bool TryGetSlotForSteamId(SteamId steamId, out int slot)
    {
        if (Peers != null)
        {
            for (int i = 0; i < Peers.Count; i++)
            {
                if (Peers[i] != null && SameSteamId(Peers[i].SteamId, steamId))
                {
                    slot = Peers[i].PlayerSlot;
                    return true;
                }
            }
        }

        slot = -1;
        return false;
    }

    private static bool SameSteamId(SteamId a, SteamId b)
    {
        return a.IsValid && b.IsValid && a.Value == b.Value;
    }
}

public class MatchMessageManager : MonoBehaviour
{
    public static MatchMessageManager Instance { get; private set; }

    [Header("Network Settings")]
    [SerializeField] private int MATCH_MESSAGE_CHANNEL = 0;
    [SerializeField] private P2PSend INPUT_SEND_TYPE = P2PSend.UnreliableNoDelay;
    [SerializeField] private P2PSend ACK_SEND_TYPE = P2PSend.Reliable;
    [SerializeField] private int EXTRA_RESEND_FRAMES = 30;
    [SerializeField] private int MAX_INPUTS_PER_PACKET = 64;

    private const byte PACKET_TYPE_READY = 2;
    private const byte PACKET_TYPE_MATCH_START = 3;
    private const byte PACKET_TYPE_LOBBY_READY = 10;
    private const byte PACKET_TYPE_SCENE_READY = 11;
    private const byte PACKET_TYPE_SEED = 12;
    private const byte PACKET_TYPE_SHOP_TRANSITION = 13;
    private const byte PACKET_TYPE_SHOP_READY = 14;
    private const byte PACKET_TYPE_END_TRANSITION = 15;
    private const byte PACKET_TYPE_STATE_HASH = 20;
    private const byte PACKET_TYPE_STAGE_SELECT = 30;
    private const byte PACKET_TYPE_SETTINGS = 40;
    private const byte PACKET_TYPE_LOBBY_ROSTER_SNAPSHOT = 41;
    private const byte PACKET_TYPE_LOBBY_ROSTER_SNAPSHOT_ACK = 42;
    private const byte PACKET_TYPE_LOBBY_ROSTER_UPDATE = 43;
    private const float PEER_HANDSHAKE_RESEND_SECONDS = 0.75f;

    [Header("Ping Calculation")]
    public CircularArray<float> sentFrameTimes = new CircularArray<float>(RollbackManager.InputArraySize);
    public int Ping { get; private set; } = 100;

    private SteamId opponentSteamId;
    public SteamId GetOpponentSteamId() => opponentSteamId;

    private bool isRunning;
    private bool localReadySent;
    private int highestRemoteFrameSeen = -1;
    private readonly HashSet<SteamId> remoteReadyReceived = new HashSet<SteamId>();
    private OnlineMatchRoster activeRoster;
    private readonly Dictionary<SteamId, int> peerHighestRemoteFrameSeen = new Dictionary<SteamId, int>();
    private readonly Dictionary<SteamId, int> peerPingMs = new Dictionary<SteamId, int>();
    private readonly HashSet<SteamId> connectedPeers = new HashSet<SteamId>();
    private readonly Dictionary<SteamId, CircularArray<float>> sentFrameTimesByPeer = new Dictionary<SteamId, CircularArray<float>>();
    private readonly Dictionary<SteamId, float> peerLastPacketTime = new Dictionary<SteamId, float>();
    private readonly Dictionary<SteamId, float> peerLastHandshakeSendTime = new Dictionary<SteamId, float>();
    private readonly HashSet<SteamId> handshakeSentToPeers = new HashSet<SteamId>();
    private readonly HashSet<SteamId> handshakeSeenFromPeers = new HashSet<SteamId>();

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

    }

    private void OnEnable()
    {
        SteamNetworking.OnP2PSessionRequest += OnP2PSessionRequest;
        SteamNetworking.OnP2PConnectionFailed += OnP2PConnectionFailed;
    }

    private void OnDisable()
    {
        SteamNetworking.OnP2PSessionRequest -= OnP2PSessionRequest;
        SteamNetworking.OnP2PConnectionFailed -= OnP2PConnectionFailed;
    }

    private void OnP2PSessionRequest(SteamId steamId)
    {
        if (IsKnownPeer(steamId) || IsCurrentLobbyMember(steamId) || (!opponentSteamId.IsValid && activeRoster == null))
        {
            SteamNetworking.AcceptP2PSessionWithUser(steamId);
            if (!opponentSteamId.IsValid)
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
        handshakeSentToPeers.Remove(steamId);
        handshakeSeenFromPeers.Remove(steamId);
        peerLastHandshakeSendTime.Remove(steamId);

        if (connectedPeers.Contains(steamId))
        {
            GameManager.Instance?.StopMatch($"Peer connection failed: {error}");
        }
        else if (IsKnownPeer(steamId) || IsCurrentLobbyMember(steamId))
        {
            SteamNetworking.CloseP2PSessionWithUser(steamId);
            SendHandshakeToPeer(steamId);
        }
    }

    private void Update()
    {
        PumpNetwork();
    }

    public void PumpNetwork()
    {
        if (!SteamClient.IsValid)
        {
            return;
        }

        while (SteamNetworking.IsP2PPacketAvailable(MATCH_MESSAGE_CHANNEL))
        {
            P2Packet? packet = SteamNetworking.ReadP2PPacket(MATCH_MESSAGE_CHANNEL);
            if (!packet.HasValue)
            {
                continue;
            }

            if (!IsKnownPeer(packet.Value.SteamId) && !IsCurrentLobbyMember(packet.Value.SteamId))
            {
                Debug.LogWarning($"Received packet from unknown SteamId: {packet.Value.SteamId}");
                continue;
            }

            if (!isRunning && !IsBootstrapPacket(packet.Value.Data))
            {
                continue;
            }

            connectedPeers.Add(packet.Value.SteamId);

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

        ProcessOutboundQueue();
        ProcessInboundQueue();
        MaintainPeerHandshakes();
        RefreshAggregatePing();
    }

    public int GetConnectedPeerCount()
    {
        return connectedPeers.Count;
    }

    public int GetPingForSlot(int slot)
    {
        if (activeRoster != null && activeRoster.TryGetSteamIdForSlot(slot, out SteamId peerId))
        {
            if (peerPingMs.TryGetValue(peerId, out int peerPing))
            {
                return peerPing;
            }
        }

        return Ping;
    }

    public bool HasAllPeersResponsive(float timeoutSeconds, out int stalePeerSlot)
    {
        stalePeerSlot = -1;
        if (!HasRemotePeers())
        {
            return true;
        }

        float now = Time.unscaledTime;
        for (int i = 0; i < activeRoster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = activeRoster.Peers[i];
            if (peer == null || SameSteamId(peer.SteamId, SteamClient.SteamId))
            {
                continue;
            }

            if (!peerLastPacketTime.TryGetValue(peer.SteamId, out float lastPacketTime)
                || now - lastPacketTime > timeoutSeconds)
            {
                stalePeerSlot = peer.PlayerSlot;
                return false;
            }
        }

        return true;
    }

    public void StartMatch(SteamId opponentId)
    {
        OnlineMatchRoster roster = new OnlineMatchRoster
        {
            HostSteamId = SteamClient.SteamId,
            LocalPlayerSlot = GameManager.Instance != null ? GameManager.Instance.localPlayerIndex : 0
        };
        roster.Peers.Add(new OnlineMatchPeerInfo { SteamId = SteamClient.SteamId, PlayerSlot = roster.LocalPlayerSlot });
        int remoteSlot = GameManager.Instance != null ? GameManager.Instance.remotePlayerIndex : 1;
        roster.Peers.Add(new OnlineMatchPeerInfo { SteamId = opponentId, PlayerSlot = remoteSlot });
        StartMatch(roster);
    }

    public void StartMatch(OnlineMatchRoster roster)
    {
        if (roster == null || roster.PlayerCount <= 1)
        {
            Debug.LogError("MatchMessageManager: invalid roster provided.");
            isRunning = false;
            return;
        }

        activeRoster = roster;
        isRunning = true;
        Ping = 100;
        sentFrameTimes.Clear();
        outboundQueue.Clear();
        inboundQueue.Clear();
        connectedPeers.Clear();
        peerHighestRemoteFrameSeen.Clear();
        peerPingMs.Clear();
        peerLastPacketTime.Clear();
        peerLastHandshakeSendTime.Clear();
        sentFrameTimesByPeer.Clear();
        handshakeSentToPeers.Clear();
        handshakeSeenFromPeers.Clear();
        ResetReadyFlags();
        SteamNetworking.AllowP2PPacketRelay(true);

        opponentSteamId = default;
        float now = Time.unscaledTime;
        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer == null || SameSteamId(peer.SteamId, SteamClient.SteamId))
            {
                continue;
            }

            peerHighestRemoteFrameSeen[peer.SteamId] = -1;
            peerPingMs[peer.SteamId] = Ping;
            peerLastPacketTime[peer.SteamId] = now;
            sentFrameTimesByPeer[peer.SteamId] = new CircularArray<float>(RollbackManager.InputArraySize);
            if (!opponentSteamId.IsValid)
            {
                opponentSteamId = peer.SteamId;
            }
        }

        SendHandshake();
    }

    public void UpdateRoster(OnlineMatchRoster roster)
    {
        if (roster == null || roster.PlayerCount <= 1)
        {
            return;
        }

        activeRoster = roster;
        isRunning = true;

        float now = Time.unscaledTime;
        HashSet<SteamId> rosterPeerIds = new HashSet<SteamId>();
        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer == null || SameSteamId(peer.SteamId, SteamClient.SteamId))
            {
                continue;
            }

            rosterPeerIds.Add(peer.SteamId);
            if (!peerHighestRemoteFrameSeen.ContainsKey(peer.SteamId))
            {
                peerHighestRemoteFrameSeen[peer.SteamId] = -1;
            }

            if (!peerPingMs.ContainsKey(peer.SteamId))
            {
                peerPingMs[peer.SteamId] = Ping;
            }

            if (!peerLastPacketTime.ContainsKey(peer.SteamId))
            {
                peerLastPacketTime[peer.SteamId] = now;
            }

            if (!sentFrameTimesByPeer.ContainsKey(peer.SteamId))
            {
                sentFrameTimesByPeer[peer.SteamId] = new CircularArray<float>(RollbackManager.InputArraySize);
            }

            if (!opponentSteamId.IsValid)
            {
                opponentSteamId = peer.SteamId;
            }
        }

        PrunePeerTracking(rosterPeerIds);
        SendHandshake();
    }

    private void PrunePeerTracking(HashSet<SteamId> rosterPeerIds)
    {
        PrunePeerSet(connectedPeers, rosterPeerIds);
        PrunePeerSet(remoteReadyReceived, rosterPeerIds);
        PrunePeerSet(handshakeSentToPeers, rosterPeerIds);
        PrunePeerSet(handshakeSeenFromPeers, rosterPeerIds);
        PrunePeerDictionary(peerHighestRemoteFrameSeen, rosterPeerIds);
        PrunePeerDictionary(peerPingMs, rosterPeerIds);
        PrunePeerDictionary(peerLastPacketTime, rosterPeerIds);
        PrunePeerDictionary(peerLastHandshakeSendTime, rosterPeerIds);
        PrunePeerDictionary(sentFrameTimesByPeer, rosterPeerIds);
    }

    private void MaintainPeerHandshakes()
    {
        if (!HasRemotePeers())
        {
            return;
        }

        float now = Time.unscaledTime;
        for (int i = 0; i < activeRoster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = activeRoster.Peers[i];
            if (peer == null || SameSteamId(peer.SteamId, SteamClient.SteamId))
            {
                continue;
            }

            if (connectedPeers.Contains(peer.SteamId) && handshakeSeenFromPeers.Contains(peer.SteamId))
            {
                continue;
            }

            if (peerLastHandshakeSendTime.TryGetValue(peer.SteamId, out float lastSendTime)
                && now - lastSendTime < PEER_HANDSHAKE_RESEND_SECONDS)
            {
                continue;
            }

            SendHandshakeToPeer(peer.SteamId);
        }
    }

    private void PrunePeerSet(HashSet<SteamId> peers, HashSet<SteamId> rosterPeerIds)
    {
        List<SteamId> stalePeers = new List<SteamId>();
        foreach (SteamId peerId in peers)
        {
            if (!rosterPeerIds.Contains(peerId))
            {
                stalePeers.Add(peerId);
            }
        }

        for (int i = 0; i < stalePeers.Count; i++)
        {
            peers.Remove(stalePeers[i]);
        }
    }

    private bool IsBootstrapPacket(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return false;
        }

        byte packetType = data[0];
        return packetType == 0xFF || packetType == PACKET_TYPE_LOBBY_ROSTER_SNAPSHOT;
    }

    private void PrunePeerDictionary<T>(Dictionary<SteamId, T> valuesByPeer, HashSet<SteamId> rosterPeerIds)
    {
        List<SteamId> stalePeers = new List<SteamId>();
        foreach (SteamId peerId in valuesByPeer.Keys)
        {
            if (!rosterPeerIds.Contains(peerId))
            {
                stalePeers.Add(peerId);
            }
        }

        for (int i = 0; i < stalePeers.Count; i++)
        {
            valuesByPeer.Remove(stalePeers[i]);
        }
    }

    public void StopMatch()
    {
        isRunning = false;
        opponentSteamId = default;
        activeRoster = null;
        connectedPeers.Clear();
        remoteReadyReceived.Clear();
        peerHighestRemoteFrameSeen.Clear();
        peerPingMs.Clear();
        peerLastPacketTime.Clear();
        sentFrameTimesByPeer.Clear();
        handshakeSentToPeers.Clear();
        handshakeSeenFromPeers.Clear();
    }

    public void ResetReadyFlags()
    {
        localReadySent = false;
        remoteReadyReceived.Clear();
        highestRemoteFrameSeen = -1;
        peerHighestRemoteFrameSeen.Clear();
    }

    public void ResetFrameSyncForSceneTransition()
    {
        highestRemoteFrameSeen = -1;
        sentFrameTimes.Clear();
        outboundQueue.Clear();
        inboundQueue.Clear();
        peerHighestRemoteFrameSeen.Clear();
        foreach (CircularArray<float> peerTimes in sentFrameTimesByPeer.Values)
        {
            peerTimes.Clear();
        }
    }

    public void SendSeed(int seed)
    {
        if (!HasRemotePeers()) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write(PACKET_TYPE_SEED);
            writer.Write(seed);
            SendPacketToAll(ms.ToArray(), P2PSend.Reliable);
        }
    }

    public void SendRollbackSettings()
    {
        if (!HasRemotePeers() || RollbackManager.Instance == null) return;

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
                writer.Write(RollbackManager.Instance.EnableFrameExtension);
                writer.Write(RollbackManager.Instance.SleepTimeMicro);
                writer.Write(RollbackManager.Instance.FrameExtensionLimit);
                writer.Write(RollbackManager.Instance.FrameExtensionWindow);
                writer.Write(RollbackManager.Instance.TimeoutFrames);
                writer.Write(RollbackManager.Instance.SoftFramePacingThreshold);
                writer.Write(RollbackManager.Instance.MaxConsecutiveFrameDrops);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending rollback settings: {e}");
        }
    }

    public void SendReadySignal()
    {
        if (!HasRemotePeers() || localReadySent)
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_READY);
                writer.Write(SteamClient.SteamId.Value);
                if (SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable))
                {
                    localReadySent = true;
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
        SendSimplePacket(PACKET_TYPE_MATCH_START);
    }

    public void SendLobbyReadySignal(int transitionId)
    {
        SendTransitionPacket(PACKET_TYPE_LOBBY_READY, transitionId);
    }

    public void SendShopReadySignal(int transitionId)
    {
        SendTransitionPacket(PACKET_TYPE_SHOP_READY, transitionId);
    }

    public void SendSceneTransitionReadySignal(int transitionId)
    {
        if (!HasRemotePeers())
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_SCENE_READY);
                writer.Write(transitionId);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneTypeCode() : (byte)0);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneSignature() : 0);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending scene transition ready signal: {e}");
        }
    }

    public void SendShopTransitionSignal(int transitionId)
    {
        if (!HasRemotePeers())
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_SHOP_TRANSITION);
                writer.Write(transitionId);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneTypeCode() : (byte)0);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneSignature() : 0);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending shop transition signal: {e}");
        }
    }

    public void SendEndTransitionSignal(int transitionId, int winnerPid)
    {
        if (!HasRemotePeers())
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_END_TRANSITION);
                writer.Write(transitionId);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneTypeCode() : (byte)0);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneSignature() : 0);
                writer.Write(winnerPid);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending end transition signal: {e}");
        }
    }

    public void SendInputs()
    {
        if (RollbackManager.Instance == null || !HasRemotePeers() || !isRunning)
        {
            return;
        }

        int currentLocalFrame = GameManager.Instance.frameNumber;
        int latestTargetFrame = currentLocalFrame + RollbackManager.Instance.InputDelay;
        int resendWindow = RollbackManager.Instance.MaxRollBackFrames + RollbackManager.Instance.InputDelay + Mathf.Max(14, EXTRA_RESEND_FRAMES);
        int firstFrameToSend = Math.Max(0, latestTargetFrame - resendWindow);

        int inputCount = latestTargetFrame - firstFrameToSend + 1;
        if (inputCount <= 0)
        {
            return;
        }

        int maxInputsPerPacket = Mathf.Max(32, MAX_INPUTS_PER_PACKET);
        if (inputCount > maxInputsPerPacket)
        {
            firstFrameToSend = latestTargetFrame - maxInputsPerPacket + 1;
            inputCount = maxInputsPerPacket;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)0);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneSignature() : 0);
                writer.Write(RollbackManager.Instance.localFrameAdvantage);
                writer.Write(firstFrameToSend);
                writer.Write((byte)inputCount);

                for (int i = 0; i < inputCount; i++)
                {
                    int frame = firstFrameToSend + i;
                    ulong inputToSend = RollbackManager.Instance.clientInputs.ContainsKey(frame)
                        ? RollbackManager.Instance.clientInputs.GetInput(frame)
                        : 5UL;
                    writer.Write(inputToSend);
                }

                byte[] packetData = memoryStream.ToArray();
                RecordSentInputTimestamp(latestTargetFrame);
                SendPacketToAll(packetData, INPUT_SEND_TYPE);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending inputs: {e}");
        }
    }

    public void SendMessageACK(SteamId peerId, int frameToAck)
    {
        if (!peerId.IsValid || !isRunning)
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)1);
                writer.Write(frameToAck);
                SendPacket(peerId, memoryStream.ToArray(), ACK_SEND_TYPE);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending ACK: {e}");
        }
    }

    public void SendStateHash(int frame, uint hash, uint sharedHash, uint projectileHash, uint[] playerHashes, uint[] playerCoreHashes, uint[] playerSpellHashes)
    {
        if (!HasRemotePeers())
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
                WriteUIntArray(writer, playerHashes);
                WriteUIntArray(writer, playerCoreHashes);
                WriteUIntArray(writer, playerSpellHashes);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending state hash: {e}");
        }
    }

    public void SendStageSelect(int transitionId, int stageIndex, uint stageRngState)
    {
        if (!HasRemotePeers())
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
                writer.Write(stageRngState);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending stage select: {e}");
        }
    }

    public void SendLobbyRosterSnapshot(SteamId peerId, OnlineMatchRoster roster, int frame, byte[] stateData, bool forceApply = false)
    {
        if (!peerId.IsValid || roster == null || stateData == null || stateData.Length == 0)
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_LOBBY_ROSTER_SNAPSHOT);
                WriteRoster(writer, roster);
                writer.Write(frame);
                writer.Write(stateData.Length);
                writer.Write(stateData);
                writer.Write(forceApply);
                SendPacket(peerId, memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending lobby roster snapshot: {e}");
        }
    }

    public void SendLobbyRosterUpdate(SteamId peerId, OnlineMatchRoster roster)
    {
        if (!peerId.IsValid || roster == null)
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_LOBBY_ROSTER_UPDATE);
                WriteRoster(writer, roster);
                SendPacket(peerId, memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending lobby roster update: {e}");
        }
    }

    private void SendLobbyRosterSnapshotAck(SteamId peerId)
    {
        if (!peerId.IsValid)
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_LOBBY_ROSTER_SNAPSHOT_ACK);
                SendPacket(peerId, memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending lobby roster snapshot ack: {e}");
        }
    }

    private void SendSimplePacket(byte packetType)
    {
        if (!HasRemotePeers())
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(packetType);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending packet type {packetType}: {e}");
        }
    }

    private void SendTransitionPacket(byte packetType, int transitionId)
    {
        if (!HasRemotePeers())
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(packetType);
                writer.Write(transitionId);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending transition packet {packetType}: {e}");
        }
    }

    private void SendHandshake()
    {
        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)0xFF);
                writer.Write("HANDSHAKE");
                byte[] data = memoryStream.ToArray();
                foreach (OnlineMatchPeerInfo peer in activeRoster.Peers)
                {
                    if (peer == null || SameSteamId(peer.SteamId, SteamClient.SteamId))
                    {
                        continue;
                    }

                    SendHandshakeToPeer(peer.SteamId, data);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending handshake: {e}");
        }
    }

    private void ProcessPacket(SteamId senderSteamId, byte[] messageData)
    {
        if (RollbackManager.Instance == null)
        {
            return;
        }

        GameManager.Instance?.OnPacketReceived();

        try
        {
            using (MemoryStream memoryStream = new MemoryStream(messageData))
            using (BinaryReader reader = new BinaryReader(memoryStream))
            {
                byte packetType = reader.ReadByte();
                int senderSlot = ResolveSlot(senderSteamId);
                peerLastPacketTime[senderSteamId] = Time.unscaledTime;
                connectedPeers.Add(senderSteamId);

                if (packetType == 0xFF)
                {
                    reader.ReadString();
                    if (handshakeSeenFromPeers.Add(senderSteamId) && !handshakeSentToPeers.Contains(senderSteamId))
                    {
                        SendHandshakeToPeer(senderSteamId);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_READY)
                {
                    reader.ReadUInt64();
                    if (!remoteReadyReceived.Add(senderSteamId))
                    {
                        return;
                    }

                    if (senderSlot >= 0)
                    {
                        GameManager.Instance?.OnPeerReady(senderSlot);
                    }

                    if (!localReadySent)
                    {
                        SendReadySignal();
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_MATCH_START)
                {
                    return;
                }

                if (packetType == PACKET_TYPE_SEED)
                {
                    int receivedSeed = reader.ReadInt32();
                    GameManager.Instance.InitializeWithSeed(receivedSeed);
                    GameManager.Instance.StartLobbySimulation();
                    return;
                }

                if (packetType == PACKET_TYPE_SETTINGS)
                {
                    int inputDelay = reader.ReadInt32();
                    bool delayBased = reader.ReadBoolean();
                    int maxRollback = reader.ReadInt32();
                    int frameAdvLimit = reader.ReadInt32();
                    bool enableFrameExtension = reader.ReadBoolean();
                    int sleepTimeMicro = reader.ReadInt32();
                    float frameExtensionLimit = reader.ReadSingle();
                    int frameExtensionWindow = reader.ReadInt32();
                    int timeoutFrames = reader.ReadInt32();
                    int softFramePacingThreshold = reader.ReadInt32();
                    int maxConsecutiveFrameDrops = reader.ReadInt32();

                    RollbackManager.Instance.ApplyOnlineSettings(
                        inputDelay,
                        delayBased,
                        maxRollback,
                        frameAdvLimit,
                        enableFrameExtension,
                        sleepTimeMicro,
                        frameExtensionLimit,
                        frameExtensionWindow,
                        timeoutFrames,
                        softFramePacingThreshold,
                        maxConsecutiveFrameDrops);
                    return;
                }

                if (packetType == PACKET_TYPE_LOBBY_ROSTER_SNAPSHOT)
                {
                    OnlineMatchRoster roster = ReadRoster(reader);
                    if (roster.HostSteamId.IsValid && !SameSteamId(senderSteamId, roster.HostSteamId))
                    {
                        return;
                    }

                    int frame = reader.ReadInt32();
                    int stateLength = reader.ReadInt32();
                    byte[] stateData = reader.ReadBytes(stateLength);
                    bool forceApply = reader.BaseStream.Position < reader.BaseStream.Length && reader.ReadBoolean();
                    bool applied = GameManager.Instance != null
                        && GameManager.Instance.ApplyOnlineLobbyRosterSnapshot(roster, frame, stateData, forceApply);
                    UpdateRoster(roster);
                    if (applied)
                    {
                        SendLobbyRosterSnapshotAck(senderSteamId);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_LOBBY_ROSTER_SNAPSHOT_ACK)
                {
                    GameManager.Instance?.OnOnlineLobbySnapshotAcknowledged(senderSteamId);
                    return;
                }

                if (packetType == PACKET_TYPE_LOBBY_ROSTER_UPDATE)
                {
                    OnlineMatchRoster roster = ReadRoster(reader);
                    if (roster.HostSteamId.IsValid && !SameSteamId(senderSteamId, roster.HostSteamId))
                    {
                        return;
                    }

                    bool applied = GameManager.Instance != null
                        && GameManager.Instance.ApplyOnlineLobbyRosterUpdate(roster);
                    if (applied)
                    {
                        UpdateRoster(roster);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_STATE_HASH)
                {
                    int frame = reader.ReadInt32();
                    uint hash = reader.ReadUInt32();
                    uint sharedHash = reader.ReadUInt32();
                    uint projectileHash = reader.ReadUInt32();
                    uint[] playerHashes = ReadUIntArray(reader);
                    uint[] playerCoreHashes = ReadUIntArray(reader);
                    uint[] playerSpellHashes = ReadUIntArray(reader);
                    RollbackManager.Instance.OnRemoteStateHash(frame, hash, sharedHash, projectileHash, playerHashes, playerCoreHashes, playerSpellHashes);
                    return;
                }

                if (packetType == PACKET_TYPE_STAGE_SELECT)
                {
                    int transitionId = reader.ReadInt32();
                    byte packetSceneType = reader.ReadByte();
                    int packetSceneSignature = reader.ReadInt32();
                    int stageIndex = reader.ReadInt32();
                    uint stageRngState = reader.ReadUInt32();
                    GameManager.Instance?.HandleOnlineStageSelect(transitionId, packetSceneType, packetSceneSignature, stageIndex, stageRngState);
                    return;
                }

                if (packetType == PACKET_TYPE_LOBBY_READY)
                {
                    int transitionId = reader.ReadInt32();
                    if (senderSlot >= 0)
                    {
                        GameManager.Instance?.OnPeerReadyForGameplayFromLobby(senderSlot, transitionId);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_SHOP_READY)
                {
                    int transitionId = reader.ReadInt32();
                    if (senderSlot >= 0)
                    {
                        GameManager.Instance?.OnPeerReadyForGameplayFromShop(senderSlot, transitionId);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_SCENE_READY)
                {
                    int transitionId = reader.ReadInt32();
                    byte sceneType = reader.ReadByte();
                    int sceneSignature = reader.ReadInt32();
                    if (senderSlot >= 0)
                    {
                        GameManager.Instance?.OnPeerSceneTransitionReady(senderSlot, transitionId, sceneType, sceneSignature);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_SHOP_TRANSITION)
                {
                    int transitionId = reader.ReadInt32();
                    byte sceneType = reader.ReadByte();
                    int sceneSignature = reader.ReadInt32();
                    if (senderSlot >= 0)
                    {
                        GameManager.Instance?.OnPeerShopTransition(senderSlot, transitionId, sceneType, sceneSignature);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_END_TRANSITION)
                {
                    int transitionId = reader.ReadInt32();
                    byte sceneType = reader.ReadByte();
                    int sceneSignature = reader.ReadInt32();
                    int winnerPid = reader.ReadInt32();
                    if (senderSlot >= 0)
                    {
                        GameManager.Instance?.OnPeerEndTransition(senderSlot, transitionId, sceneType, sceneSignature, winnerPid);
                    }
                    return;
                }

                if (packetType == 0)
                {
                    if (GameManager.Instance != null && GameManager.Instance.isWaitingForOpponent)
                    {
                        return;
                    }

                    int packetSceneSignature = reader.ReadInt32();
                    int remoteFrameAdvantage = reader.ReadInt32();
                    int startFrame = reader.ReadInt32();
                    int inputCount = reader.ReadByte();
                    int newestPacketFrame = startFrame + inputCount - 1;

                    if (senderSlot < 0 && activeRoster != null)
                    {
                        return;
                    }

                    if (GameManager.Instance != null && packetSceneSignature != GameManager.Instance.GetNetworkSceneSignature())
                    {
                        GameManager.Instance.HandleInputSceneSignatureMismatch(senderSlot, packetSceneSignature);
                        Debug.LogWarning($"Ignoring stale input packet after scene transition. StartFrame={startFrame}, Count={inputCount}, PacketScene={packetSceneSignature}, LocalScene={GameManager.Instance.GetNetworkSceneSignature()}");
                        return;
                    }

                    for (int i = 0; i < inputCount; i++)
                    {
                        int frame = startFrame + i;
                        ulong input = reader.ReadUInt64();

                        if (senderSlot >= 0)
                        {
                            RollbackManager.Instance.SetRemoteInput(senderSlot, frame, input, newestPacketFrame);
                        }
                        else if (!RollbackManager.Instance.receivedInputs.ContainsKey(frame))
                        {
                            RollbackManager.Instance.SetOpponentInput(frame, input);
                        }
                        else
                        {
                            ulong existingInput = RollbackManager.Instance.receivedInputs.GetInput(frame);
                            if (existingInput != input && frame > RollbackManager.Instance.syncFrame)
                            {
                                RollbackManager.Instance.SetOpponentInput(frame, input);
                            }
                        }

                        if (i == inputCount - 1)
                        {
                            int highestSeen = peerHighestRemoteFrameSeen.TryGetValue(senderSteamId, out int seen) ? seen : -1;
                            if (frame > highestSeen)
                            {
                                peerHighestRemoteFrameSeen[senderSteamId] = frame;
                                highestRemoteFrameSeen = Mathf.Max(highestRemoteFrameSeen, frame);
                                if (senderSlot >= 0)
                                {
                                    RollbackManager.Instance.SetRemoteFrameAdvantage(senderSlot, frame, remoteFrameAdvantage);
                                    RollbackManager.Instance.SetRemoteFrame(senderSlot, frame);
                                }
                                else
                                {
                                    RollbackManager.Instance.SetRemoteFrameAdvantage(frame, remoteFrameAdvantage);
                                    RollbackManager.Instance.SetRemoteFrame(frame);
                                }
                            }
                        }
                    }

                    SendMessageACK(senderSteamId, newestPacketFrame);
                    return;
                }

                if (packetType == 1)
                {
                    ProcessACK(senderSteamId, reader.ReadInt32());
                    return;
                }

                Debug.LogWarning($"Received unknown packet type: {packetType}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Packet processing error: {e}");
        }
    }

    private void ProcessACK(SteamId senderSteamId, int frame)
    {
        if (!sentFrameTimesByPeer.TryGetValue(senderSteamId, out CircularArray<float> peerSentFrameTimes))
        {
            return;
        }

        float sentTime = peerSentFrameTimes.Get(frame);
        if (sentTime <= 0f)
        {
            return;
        }

        int rttMs = Mathf.RoundToInt((Time.unscaledTime - sentTime) * 1000f);
        peerPingMs[senderSteamId] = rttMs;
        peerSentFrameTimes.Insert(frame, 0f);
    }

    private void RefreshAggregatePing()
    {
        if (peerPingMs.Count == 0)
        {
            return;
        }

        int total = 0;
        foreach (int ping in peerPingMs.Values)
        {
            total += ping;
        }

        Ping = Mathf.RoundToInt((float)total / peerPingMs.Count);
    }

    private bool HasRemotePeers()
    {
        return isRunning && activeRoster != null && activeRoster.PlayerCount > 1;
    }

    private bool IsKnownPeer(SteamId steamId)
    {
        if (activeRoster != null)
        {
            return activeRoster.TryGetSlotForSteamId(steamId, out int slot) && slot != activeRoster.LocalPlayerSlot;
        }

        return SameSteamId(steamId, opponentSteamId);
    }

    private bool IsCurrentLobbyMember(SteamId steamId)
    {
        return SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsCurrentLobbyMember(steamId);
    }

    private int ResolveSlot(SteamId steamId)
    {
        if (activeRoster != null && activeRoster.TryGetSlotForSteamId(steamId, out int slot))
        {
            return slot;
        }

        return GameManager.Instance != null && SameSteamId(steamId, opponentSteamId) ? GameManager.Instance.remotePlayerIndex : -1;
    }

    private bool SendPacketToAll(byte[] data, P2PSend sendType)
    {
        bool any = false;
        foreach (OnlineMatchPeerInfo peer in activeRoster.Peers)
        {
            if (peer == null || SameSteamId(peer.SteamId, SteamClient.SteamId))
            {
                continue;
            }

            any |= SendPacket(peer.SteamId, data, sendType);
        }

        return any;
    }

    private void WriteRoster(BinaryWriter writer, OnlineMatchRoster roster)
    {
        writer.Write(roster.HostSteamId.Value);
        writer.Write(roster.Peers?.Count ?? 0);
        if (roster.Peers == null)
        {
            return;
        }

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            writer.Write(peer != null ? peer.SteamId.Value : 0UL);
            writer.Write(peer != null ? peer.PlayerSlot : -1);
        }
    }

    private OnlineMatchRoster ReadRoster(BinaryReader reader)
    {
        OnlineMatchRoster roster = new OnlineMatchRoster
        {
            HostSteamId = reader.ReadUInt64(),
            LocalPlayerSlot = -1
        };

        int peerCount = reader.ReadInt32();
        for (int i = 0; i < peerCount; i++)
        {
            SteamId steamId = reader.ReadUInt64();
            int playerSlot = reader.ReadInt32();
            if (!steamId.IsValid || playerSlot < 0)
            {
                continue;
            }

            roster.Peers.Add(new OnlineMatchPeerInfo
            {
                SteamId = steamId,
                PlayerSlot = playerSlot
            });

            if (SameSteamId(steamId, SteamClient.SteamId))
            {
                roster.LocalPlayerSlot = playerSlot;
            }
        }

        return roster;
    }

    private static bool SameSteamId(SteamId a, SteamId b)
    {
        return a.IsValid && b.IsValid && a.Value == b.Value;
    }

    private void RecordSentInputTimestamp(int frame)
    {
        float now = Time.unscaledTime;
        sentFrameTimes.Insert(frame, now);
        foreach (OnlineMatchPeerInfo peer in activeRoster.Peers)
        {
            if (peer == null || SameSteamId(peer.SteamId, SteamClient.SteamId))
            {
                continue;
            }

            if (!sentFrameTimesByPeer.TryGetValue(peer.SteamId, out CircularArray<float> peerTimes))
            {
                peerTimes = new CircularArray<float>(RollbackManager.InputArraySize);
                sentFrameTimesByPeer[peer.SteamId] = peerTimes;
            }

            peerTimes.Insert(frame, now);
        }
    }

    private void SendHandshakeToPeer(SteamId peerId)
    {
        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)0xFF);
                writer.Write("HANDSHAKE");
                SendHandshakeToPeer(peerId, memoryStream.ToArray());
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending handshake: {e}");
        }
    }

    private void SendHandshakeToPeer(SteamId peerId, byte[] data)
    {
        try
        {
            if (SendPacket(peerId, data, P2PSend.Reliable))
            {
                handshakeSentToPeers.Add(peerId);
                peerLastHandshakeSendTime[peerId] = Time.unscaledTime;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending handshake: {e}");
        }
    }

    private bool SendPacket(SteamId peerId, byte[] data, P2PSend sendType)
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

        return SteamNetworking.SendP2PPacket(peerId, data, data.Length, MATCH_MESSAGE_CHANNEL, sendType);
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
                SteamNetworking.SendP2PPacket(packet.peerId, packet.data, packet.data.Length, MATCH_MESSAGE_CHANNEL, packet.sendType);
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

    private static void WriteUIntArray(BinaryWriter writer, uint[] values)
    {
        int count = values?.Length ?? 0;
        writer.Write(count);
        for (int i = 0; i < count; i++)
        {
            writer.Write(values[i]);
        }
    }

    private static uint[] ReadUIntArray(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        uint[] values = new uint[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = reader.ReadUInt32();
        }
        return values;
    }
}
