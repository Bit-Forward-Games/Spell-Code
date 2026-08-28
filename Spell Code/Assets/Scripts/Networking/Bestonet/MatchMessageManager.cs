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
    public ulong MatchSessionId;
    public int LocalPlayerSlot;
    public List<OnlineMatchPeerInfo> Peers = new List<OnlineMatchPeerInfo>();

    public int PlayerCount => Peers?.Count ?? 0;

    /// <summary>
    /// Number of simulation slots needed to preserve the authored P1-P4 assignments. This differs
    /// from PlayerCount for a sparse party roster: P1 + P3 has two peers but occupies three slots.
    /// </summary>
    public int SlotCount
    {
        get
        {
            int highestSlot = -1;
            if (Peers != null)
            {
                for (int i = 0; i < Peers.Count; i++)
                {
                    if (Peers[i] != null && Peers[i].PlayerSlot > highestSlot)
                    {
                        highestSlot = Peers[i].PlayerSlot;
                    }
                }
            }

            return highestSlot + 1;
        }
    }

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
    private const byte PACKET_TYPE_END_OPTION_STATE = 16;
    private const byte PACKET_TYPE_END_OPTION_RESULT = 17;
    private const byte PACKET_TYPE_END_OPTION_RESULT_ACK = 18;
    private const byte PACKET_TYPE_REMATCH_LOBBY_TRANSITION = 19;
    private const byte PACKET_TYPE_STATE_HASH = 20;
    private const byte PACKET_TYPE_STAGE_SELECT = 30;
    private const byte PACKET_TYPE_SETTINGS = 40;
    private const byte PACKET_TYPE_LOBBY_ROSTER_SNAPSHOT = 41;
    private const byte PACKET_TYPE_LOBBY_ROSTER_SNAPSHOT_ACK = 42;
    private const byte PACKET_TYPE_LOBBY_ROSTER_UPDATE = 43;
    private const byte PACKET_TYPE_PEER_DROP = 50;
    private const byte PACKET_TYPE_PEER_DROP_ACK = 51;
    private const byte PACKET_TYPE_INITIAL_INPUT_STREAM_READY = 52;
    private const byte PACKET_TYPE_MATCH_ABORT = 53;
    private const float PEER_HANDSHAKE_RESEND_SECONDS = 0.75f;
    private const float READY_SIGNAL_RESEND_SECONDS = 0.75f;
    // Connection-establishment grace. A P2P failure that lands before the match simulation has
    // started means the peer is still finishing its cold join (relay warmup, lobby slot metadata
    // propagation), not that it dropped mid-match. Adjudicating a drop there ends the match at
    // frame 0 with a bogus "win by disconnect", so we retry the handshake for a bounded window
    // and only fall back to the normal disconnect path once it expires.
    private const float PREMATCH_CONNECT_GRACE_SECONDS = 20f;
    private const float PREMATCH_CONNECT_RETRY_INTERVAL_SECONDS = 1f;

    [Header("Ping Calculation")]
    public CircularArray<float> sentFrameTimes = new CircularArray<float>(RollbackManager.InputArraySize);
    public int Ping { get; private set; } = 100;

    private SteamId opponentSteamId;
    public SteamId GetOpponentSteamId() => opponentSteamId;
    public ulong ActiveMatchSessionId => activeRoster?.MatchSessionId ?? 0UL;

    private bool isRunning;
    private bool localReadySent;
    private float lastReadySignalSendTime = float.NegativeInfinity;
    private int highestRemoteFrameSeen = -1;
    private readonly HashSet<SteamId> remoteReadyReceived = new HashSet<SteamId>();
    private OnlineMatchRoster activeRoster;
    private readonly Dictionary<SteamId, int> peerHighestRemoteFrameSeen = new Dictionary<SteamId, int>();
    private readonly Dictionary<SteamId, int> peerPingMs = new Dictionary<SteamId, int>();
    private readonly HashSet<SteamId> connectedPeers = new HashSet<SteamId>();
    private readonly Dictionary<SteamId, CircularArray<float>> sentFrameTimesByPeer = new Dictionary<SteamId, CircularArray<float>>();
    private readonly Dictionary<SteamId, int> highestTimestampedInputFrameByPeer = new Dictionary<SteamId, int>();
    private int highestTimestampedLocalInputFrame = -1;
    private readonly Dictionary<SteamId, float> peerLastPacketTime = new Dictionary<SteamId, float>();
    private readonly Dictionary<SteamId, float> peerLastHandshakeSendTime = new Dictionary<SteamId, float>();
    private readonly HashSet<SteamId> handshakeSentToPeers = new HashSet<SteamId>();
    private readonly HashSet<SteamId> handshakeSeenFromPeers = new HashSet<SteamId>();
    private readonly HashSet<int> locallyRemovedPeerSlots = new HashSet<int>();
    private byte[] lastStageSelectPacket;
    private int lastStageSelectTransitionId;
    private byte[] lastRematchLobbyTransitionPacket;
    private int lastRematchLobbyTransitionId;

    private struct PrematchConnectRetry
    {
        public float deadline;
        public float lastAttemptTime;
        public int attempts;
    }

    private readonly Dictionary<SteamId, PrematchConnectRetry> prematchConnectRetries = new Dictionary<SteamId, PrematchConnectRetry>();

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

    // Most-recent input packet from each peer that arrived while our scene
    // signature didn't match the sender's. Replayed automatically each pump so inputs
    // are never silently dropped just because a peer transitioned scenes slightly ahead
    // or behind us, or because two peers ended up on different stage indices.
    private readonly Dictionary<SteamId, byte[]> sceneMismatchedInputByPeer = new Dictionary<SteamId, byte[]>();
    // How many pump cycles a buffered input packet may live before being
    // discarded. Prevents the buffer from holding genuinely stale data forever.
    private readonly Dictionary<SteamId, int> sceneMismatchedInputAgeByPeer = new Dictionary<SteamId, int>();
    private const int SCENE_MISMATCH_REPLAY_MAX_AGE_TICKS = 600; // ~10s at 60fps
    // Rate-limit diagnostic logs about scene-mismatched buffering. Without this, a peer
    // that's persistently in a different scene would spam the log every packet (~60/s).
    // We emit one log per peer-scene-signature combo so each unique mismatch shows once.
    private readonly Dictionary<SteamId, int> lastLoggedMismatchSignatureByPeer = new Dictionary<SteamId, int>();

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
        int slot = ResolveSlot(steamId);
        if (IsPeerSlotRemovedFromTransport(slot))
        {
            SteamNetworking.CloseP2PSessionWithUser(steamId);
            return;
        }

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
        int slot = ResolveSlot(steamId);
        bool wasConnected = connectedPeers.Contains(steamId);
        bool wasAlreadyRemoved = IsPeerSlotRemovedFromTransport(slot);
        ForgetPeerTransport(steamId, closeSession: true);

        if (wasAlreadyRemoved)
        {
            Debug.LogWarning($"[P2P] Ignoring connection failure from already removed peer P{slot + 1}: {error}");
            return;
        }

        if (activeRoster != null
            && slot >= 0
            && GameManager.Instance != null
            && GameManager.Instance.isOnlineMatchActive)
        {
            if (GameManager.Instance.isTransitioning)
            {
                if (GameManager.Instance.IsOnlineHostSlot(slot))
                {
                    GameManager.Instance.ResetToMainMenuAfterHostDisconnect($"Host connection failed during scene transition: {error}");
                }
                else if (GameManager.Instance.IsOnlineHostAuthority())
                {
                    GameManager.Instance.DropUnresponsiveOnlineTransitionPeer(slot);
                }
                else
                {
                    Debug.LogWarning($"[OnlineTransition] P{slot + 1} connection failed; waiting for the host's authoritative removal.");
                }
                return;
            }

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "End")
            {
                if (GameManager.Instance.IsOnlineHostSlot(slot))
                {
                    if (GameEndScreen.ActiveInstance != null)
                    {
                        GameEndScreen.ActiveInstance.HandleOnlineHostLost();
                    }
                    else
                    {
                        GameManager.Instance.StopMatch($"Host connection failed on End screen: {error}");
                        GameManager.Instance?.sceneManager?.SoloLobby();
                    }
                    return;
                }

                if (GameManager.Instance.IsOnlineHostAuthority())
                {
                    GameManager.Instance.DropUnresponsiveEndScreenPeer(slot);
                }
                else
                {
                    Debug.LogWarning($"[EndOptions] P{slot + 1} connection failed; waiting for host removal.");
                }
                return;
            }

            // Still waiting for everyone to be ready, so the simulation hasn't started: treat this
            // as a slow cold join rather than a disconnect and keep trying to reach the peer.
            if (TryPrematchConnectGrace(steamId, error, slot))
            {
                return;
            }

            if (GameManager.Instance.IsOnlineHostSlot(slot))
            {
                GameManager.Instance.ResetToMainMenuAfterHostDisconnect($"Host connection failed: {error}");
                return;
            }

            if (GameManager.Instance.IsOnlineHostAuthority())
            {
                int dropFrame = RollbackManager.Instance != null
                    ? RollbackManager.Instance.syncFrame
                    : GameManager.Instance.frameNumber;
                Debug.LogWarning($"[P2P] Guest P{slot + 1} connection failed ({error}); host adjudicating drop at frame {dropFrame}.");
                SendPeerDrop(slot, dropFrame);
                RollbackManager.Instance?.DropRemoteSlot(slot, dropFrame);
                return;
            }

            locallyRemovedPeerSlots.Add(slot);
            Debug.LogWarning($"[P2P] Guest P{slot + 1} connection failed ({error}); waiting for host drop adjudication.");
            return;
        }

        if (wasConnected)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsOnlineHostSlot(slot))
            {
                GameManager.Instance.ResetToMainMenuAfterHostDisconnect($"Host connection failed: {error}");
            }
            else
            {
                GameManager.Instance?.StopMatch($"Peer connection failed: {error}");
            }
        }
        else if (IsKnownPeer(steamId) || IsCurrentLobbyMember(steamId))
        {
            SendHandshakeToPeer(steamId);
        }
    }

    /// <summary>
    /// Absorbs a P2P connection failure that happens after the roster is applied but before the
    /// match simulation starts, retrying the handshake instead of letting the caller adjudicate a
    /// disconnect.
    /// </summary>
    /// <remarks>
    /// A peer joining cold has to warm up the Steam relay network and receive its lobby slot
    /// metadata before it can answer us. That can outlast Steam's P2P session timeout, and the
    /// resulting failure used to be handled identically to a mid-match disconnect: the host
    /// dropped the guest at frame 0 and immediately won by disconnect, so the match ended before
    /// it began.
    ///
    /// The grace only applies while <see cref="GameManager.isWaitingForOpponent"/> is set, which
    /// is exactly the window where GameManager skips the simulation, so the in-match drop path -
    /// where every peer must eliminate a player on the same frame - is untouched. If the peer is
    /// still unreachable when the window expires we return false and the original handling runs.
    /// </remarks>
    /// <returns>True if the failure was absorbed and the caller should stop processing it.</returns>
    private bool TryPrematchConnectGrace(SteamId peerId, P2PSessionError error, int slot)
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || !gameManager.isOnlineMatchActive || !gameManager.isWaitingForOpponent)
        {
            return false;
        }

        float now = Time.unscaledTime;
        if (!prematchConnectRetries.TryGetValue(peerId, out PrematchConnectRetry retry))
        {
            retry = new PrematchConnectRetry { deadline = now + PREMATCH_CONNECT_GRACE_SECONDS };
        }

        if (now >= retry.deadline)
        {
            Debug.LogWarning($"[P2P] Pre-match connect grace expired for P{slot + 1} after {retry.attempts} retries ({error}); handing back to normal disconnect handling.");
            prematchConnectRetries.Remove(peerId);
            return false;
        }

        if (now - retry.lastAttemptTime >= PREMATCH_CONNECT_RETRY_INTERVAL_SECONDS)
        {
            retry.lastAttemptTime = now;
            retry.attempts++;
            SendHandshakeToPeer(peerId);
            Debug.LogWarning($"[P2P] P{slot + 1} not connected yet ({error}); match hasn't started, retrying handshake #{retry.attempts} for another {retry.deadline - now:F1}s.");
        }

        prematchConnectRetries[peerId] = retry;
        return true;
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

            if (ShouldIgnorePacketFromPeer(packet.Value.SteamId))
            {
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
        TryReplayBufferedSceneMismatchedInputs();
        MaintainPeerHandshakes();
        RefreshAggregatePing();
    }

    // Replay any input packets that were buffered because they arrived while
    // our scene signature didn't match the sender's. Called every PumpNetwork tick so the
    // moment our scene catches up, the held packet is processed exactly as if it had just
    // arrived. Stale entries are aged out via SCENE_MISMATCH_REPLAY_MAX_AGE_TICKS.
    private void TryReplayBufferedSceneMismatchedInputs()
    {
        if (sceneMismatchedInputByPeer.Count == 0)
        {
            return;
        }

        if (GameManager.Instance == null || !isRunning)
        {
            return;
        }

        List<SteamId> replayablePeers = null;
        List<SteamId> expiredPeers = null;

        foreach (KeyValuePair<SteamId, byte[]> entry in sceneMismatchedInputByPeer)
        {
            SteamId peer = entry.Key;
            byte[] data = entry.Value;
            const int fixedInputHeaderBytes = 34;
            if (data == null
                || data.Length < fixedInputHeaderBytes
                || activeRoster == null)
            {
                expiredPeers ??= new List<SteamId>();
                expiredPeers.Add(peer);
                continue;
            }

            // Read deterministic context without disturbing the buffered bytes.
            // Layout: [type][session][epoch][sceneSignature][frameAdv]
            //         [batchFirst][batchHigh][chunkStart][count]...
            ulong bufferedSessionId = BitConverter.ToUInt64(data, 1);
            int bufferedTimelineEpoch = BitConverter.ToInt32(data, 9);
            int bufferedSceneSignature = BitConverter.ToInt32(data, 13);
            int localTimelineEpoch = GameManager.Instance.GetOnlineTimelineEpoch();
            int localSceneSignature = GameManager.Instance.GetNetworkSceneSignature();

            if (bufferedSessionId != activeRoster.MatchSessionId
                || bufferedTimelineEpoch < localTimelineEpoch)
            {
                expiredPeers ??= new List<SteamId>();
                expiredPeers.Add(peer);
            }
            else if (bufferedTimelineEpoch == localTimelineEpoch
                && bufferedSceneSignature == localSceneSignature)
            {
                replayablePeers ??= new List<SteamId>();
                replayablePeers.Add(peer);
            }
            else
            {
                int age = sceneMismatchedInputAgeByPeer.TryGetValue(peer, out int currentAge) ? currentAge + 1 : 1;
                sceneMismatchedInputAgeByPeer[peer] = age;
                if (age > SCENE_MISMATCH_REPLAY_MAX_AGE_TICKS)
                {
                    expiredPeers ??= new List<SteamId>();
                    expiredPeers.Add(peer);
                }
            }
        }

        if (expiredPeers != null)
        {
            for (int i = 0; i < expiredPeers.Count; i++)
            {
                sceneMismatchedInputByPeer.Remove(expiredPeers[i]);
                sceneMismatchedInputAgeByPeer.Remove(expiredPeers[i]);
            }
        }

        if (replayablePeers != null)
        {
            for (int i = 0; i < replayablePeers.Count; i++)
            {
                SteamId peer = replayablePeers[i];
                byte[] data = sceneMismatchedInputByPeer[peer];
                sceneMismatchedInputByPeer.Remove(peer);
                sceneMismatchedInputAgeByPeer.Remove(peer);
                lastLoggedMismatchSignatureByPeer.Remove(peer); // reset so next mismatch logs again
                Debug.Log($"[PacketDiag] Replayed scene-mismatched input packet from peer {peer}.");

                try
                {
                    ProcessPacket(peer, data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error replaying buffered scene-mismatched input from {peer}: {e}");
                }
            }
        }
    }

    // Stash the newest/highest chunk from this peer so it can be replayed once our scene signature
    // matches theirs. The sender keeps resending every chunk while its simulation remains active;
    // retaining the batch-high chunk prevents a reordered older chunk from replacing newer input.
    private void BufferSceneMismatchedInputPacket(SteamId peerId, byte[] originalPacket)
    {
        if (!peerId.IsValid || originalPacket == null || originalPacket.Length == 0)
        {
            return;
        }

        // Diagnostic: log the first time we buffer a packet for this peer at this
        // epoch/scene-signature combo. Buffering happens every packet (~60/s) while a
        // mismatch persists, so log only when that context changes.
        // The packet layout (set in SendInputs) is:
        //   [type][session][epoch][sceneSignature][frameAdv]
        //   [batchFirst][batchHigh][chunkStart][inputCount]...
        const int fixedInputHeaderBytes = 34;
        if (originalPacket.Length >= fixedInputHeaderBytes
            && originalPacket[0] == 0
            && GameManager.Instance != null)
        {
            int theirEpoch = BitConverter.ToInt32(originalPacket, 9);
            int theirSignature = BitConverter.ToInt32(originalPacket, 13);
            int ourSignature = GameManager.Instance.GetNetworkSceneSignature();
            // Use a composite int as the dedup key: theirSig XOR ourSig (cheap, collisions
            // would just suppress an extra log line, no functional impact).
            int dedupKey = theirSignature ^ (ourSignature << 1) ^ (theirEpoch * 397);
            if (!lastLoggedMismatchSignatureByPeer.TryGetValue(peerId, out int prev) || prev != dedupKey)
            {
                lastLoggedMismatchSignatureByPeer[peerId] = dedupKey;
                Debug.LogWarning($"[PacketDiag] Buffering input from peer {peerId} due to timeline/scene mismatch. theirEpoch={theirEpoch} ourEpoch={GameManager.Instance.GetOnlineTimelineEpoch()} theirSig={theirSignature} ourSig={ourSignature}. Will replay when our scene catches up.");
            }
        }

        if (originalPacket.Length < fixedInputHeaderBytes)
        {
            return;
        }

        ulong candidateSessionId = BitConverter.ToUInt64(originalPacket, 1);
        int candidateEpoch = BitConverter.ToInt32(originalPacket, 9);
        int candidateBatchHigh = BitConverter.ToInt32(originalPacket, 25);
        int candidateChunkStart = BitConverter.ToInt32(originalPacket, 29);
        int candidateChunkNewest = candidateChunkStart + originalPacket[33] - 1;
        if (sceneMismatchedInputByPeer.TryGetValue(peerId, out byte[] existingPacket)
            && existingPacket != null
            && existingPacket.Length >= fixedInputHeaderBytes)
        {
            ulong existingSessionId = BitConverter.ToUInt64(existingPacket, 1);
            int existingEpoch = BitConverter.ToInt32(existingPacket, 9);
            int existingBatchHigh = BitConverter.ToInt32(existingPacket, 25);
            int existingChunkStart = BitConverter.ToInt32(existingPacket, 29);
            int existingChunkNewest = existingChunkStart + existingPacket[33] - 1;
            if (candidateSessionId == existingSessionId
                && (candidateEpoch < existingEpoch
                    || (candidateEpoch == existingEpoch
                        && (candidateBatchHigh < existingBatchHigh
                            || (candidateBatchHigh == existingBatchHigh
                                && candidateChunkNewest <= existingChunkNewest)))))
            {
                return;
            }
        }

        // Defensive copy: the caller's buffer can be reused by the network layer.
        byte[] copy = new byte[originalPacket.Length];
        Buffer.BlockCopy(originalPacket, 0, copy, 0, originalPacket.Length);
        sceneMismatchedInputByPeer[peerId] = copy;
        sceneMismatchedInputAgeByPeer[peerId] = 0;
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

            // A peer that has already been dropped from the match is no longer expected to
            // send packets — skip it so it neither re-triggers a timeout nor masks a second
            // peer's genuine disconnect.
            if (GameManager.Instance != null && !GameManager.Instance.IsPlayerSlotConnected(peer.PlayerSlot))
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
            // Legacy two-player entry has no lobby token. This stable pair identity keeps
            // packet layouts valid; current party/matchmaking flows use the lobby id below.
            MatchSessionId = SteamClient.SteamId.Value
                ^ opponentId.Value
                ^ 0x9E3779B97F4A7C15UL,
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
        highestTimestampedLocalInputFrame = -1;
        outboundQueue.Clear();
        inboundQueue.Clear();
        connectedPeers.Clear();
        peerHighestRemoteFrameSeen.Clear();
        peerPingMs.Clear();
        peerLastPacketTime.Clear();
        peerLastHandshakeSendTime.Clear();
        sentFrameTimesByPeer.Clear();
        highestTimestampedInputFrameByPeer.Clear();
        handshakeSentToPeers.Clear();
        handshakeSeenFromPeers.Clear();
        locallyRemovedPeerSlots.Clear();
        prematchConnectRetries.Clear();
        lastStageSelectPacket = null;
        lastStageSelectTransitionId = 0;
        lastRematchLobbyTransitionPacket = null;
        lastRematchLobbyTransitionId = 0;
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
            highestTimestampedInputFrameByPeer[peer.SteamId] = -1;
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

            if (IsPeerSlotRemovedFromTransport(peer.PlayerSlot))
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

            if (!highestTimestampedInputFrameByPeer.ContainsKey(peer.SteamId))
            {
                highestTimestampedInputFrameByPeer[peer.SteamId] = -1;
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
        PrunePeerDictionary(highestTimestampedInputFrameByPeer, rosterPeerIds);
        PrunePeerDictionary(sceneMismatchedInputByPeer, rosterPeerIds);
        PrunePeerDictionary(sceneMismatchedInputAgeByPeer, rosterPeerIds);
        PrunePeerDictionary(lastLoggedMismatchSignatureByPeer, rosterPeerIds);
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

            if (IsPeerSlotRemovedFromTransport(peer.PlayerSlot))
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

        // READY used to be a one-shot broadcast. In a 4-player roster SendPacketToAll can queue for
        // one peer while another P2P session is still opening, and the successful send latched
        // localReadySent forever. Retry during the pre-match wait; READY is idempotent on receive.
        if (GameManager.Instance != null
            && GameManager.Instance.isOnlineMatchActive
            && GameManager.Instance.isWaitingForOpponent
            && now - lastReadySignalSendTime >= READY_SIGNAL_RESEND_SECONDS)
        {
            SendReadySignal(forceResend: true);
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
        peerLastHandshakeSendTime.Clear();
        sentFrameTimesByPeer.Clear();
        highestTimestampedInputFrameByPeer.Clear();
        highestTimestampedLocalInputFrame = -1;
        handshakeSentToPeers.Clear();
        handshakeSeenFromPeers.Clear();
        locallyRemovedPeerSlots.Clear();
        prematchConnectRetries.Clear();
        sceneMismatchedInputByPeer.Clear();
        sceneMismatchedInputAgeByPeer.Clear();
        lastLoggedMismatchSignatureByPeer.Clear();
        lastStageSelectPacket = null;
        lastStageSelectTransitionId = 0;
        lastRematchLobbyTransitionPacket = null;
        lastRematchLobbyTransitionId = 0;
    }

    public void ResetReadyFlags()
    {
        localReadySent = false;
        lastReadySignalSendTime = float.NegativeInfinity;
        remoteReadyReceived.Clear();
        highestRemoteFrameSeen = -1;
        peerHighestRemoteFrameSeen.Clear();
    }

    public void ResetFrameSyncForSceneTransition()
    {
        highestRemoteFrameSeen = -1;
        sentFrameTimes.Clear();
        highestTimestampedLocalInputFrame = -1;
        highestTimestampedInputFrameByPeer.Clear();
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
        SendReadySignal(forceResend: false);
    }

    private void SendReadySignal(bool forceResend)
    {
        if (!HasRemotePeers() || (!forceResend && localReadySent))
        {
            return;
        }

        lastReadySignalSendTime = Time.unscaledTime;
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

    public void SendSceneTransitionReadySignal(int transitionId, bool isRecoveryResponse = false)
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
                writer.Write(isRecoveryResponse);
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

    public void SendRematchLobbyTransition(int transitionId, int rematchSeed)
    {
        if (!HasRemotePeers()
            || GameManager.Instance == null
            || !GameManager.Instance.IsOnlineHostAuthority()
            || rematchSeed <= 0)
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_REMATCH_LOBBY_TRANSITION);
                writer.Write(transitionId);
                writer.Write(GameManager.Instance.GetConnectedPlayerSlotMask());
                writer.Write(rematchSeed);
                lastRematchLobbyTransitionPacket = memoryStream.ToArray();
                lastRematchLobbyTransitionId = transitionId;
                SendPacketToAll(lastRematchLobbyTransitionPacket, P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending rematch lobby transition: {e}");
        }
    }

    public void SendEndOptionState(int epoch, byte option, bool confirmed, uint revision)
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
                writer.Write(PACKET_TYPE_END_OPTION_STATE);
                writer.Write(epoch);
                writer.Write(option);
                writer.Write(confirmed);
                writer.Write(revision);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending end-option state: {e}");
        }
    }

    public void SendEndOptionResult(int epoch, byte result)
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
                writer.Write(PACKET_TYPE_END_OPTION_RESULT);
                writer.Write(epoch);
                writer.Write(result);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending end-option result: {e}");
        }
    }

    public void SendEndOptionResultAcknowledgement(int epoch)
    {
        if (!HasRemotePeers()
            || activeRoster == null
            || !activeRoster.HostSteamId.IsValid
            || SameSteamId(activeRoster.HostSteamId, SteamClient.SteamId))
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_END_OPTION_RESULT_ACK);
                writer.Write(epoch);
                SendPacket(activeRoster.HostSteamId, memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error acknowledging end-option result: {e}");
        }
    }

    public void SendInitialInputStreamReady(
        ulong matchSessionId,
        int timelineEpoch,
        int sceneSignature,
        int matchSeed)
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
                writer.Write(PACKET_TYPE_INITIAL_INPUT_STREAM_READY);
                writer.Write(matchSessionId);
                writer.Write(timelineEpoch);
                writer.Write(sceneSignature);
                writer.Write(matchSeed);
                SendPacketToAll(memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending initial input-stream READY: {e}");
        }
    }

    public void SendMatchAbortToAll(string reason)
    {
        byte[] packet = BuildMatchAbortPacket(reason);
        if (packet != null)
        {
            SendPacketToAllDirect(packet, P2PSend.Reliable);
        }
    }

    private byte[] BuildMatchAbortPacket(string reason)
    {
        if (!HasRemotePeers()
            || GameManager.Instance == null
            || !GameManager.Instance.IsOnlineHostAuthority())
        {
            return null;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_MATCH_ABORT);
                writer.Write(activeRoster.MatchSessionId);
                writer.Write(GameManager.Instance.GetOnlineTimelineEpoch());
                writer.Write(string.IsNullOrWhiteSpace(reason)
                    ? "Host aborted the online match"
                    : reason.Substring(0, Mathf.Min(reason.Length, 256)));
                return memoryStream.ToArray();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error building match-abort packet: {e}");
            return null;
        }
    }

    /// <summary>
    /// Host-authoritative notification that a peer has dropped from the match.
    /// All surviving peers apply the drop deterministically at <paramref name="dropFrame"/>.
    /// </summary>
    public void SendPeerDrop(
        int slot,
        int dropFrame,
        bool outsideSimulation = false,
        ulong? matchSessionId = null,
        int? timelineEpoch = null,
        int? sceneSignature = null)
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
                writer.Write(PACKET_TYPE_PEER_DROP);
                writer.Write(matchSessionId ?? activeRoster.MatchSessionId);
                writer.Write(timelineEpoch
                    ?? (GameManager.Instance != null
                        ? GameManager.Instance.GetOnlineTimelineEpoch()
                        : 0));
                writer.Write(sceneSignature
                    ?? (GameManager.Instance != null
                        ? GameManager.Instance.GetNetworkSceneSignature()
                        : 0));
                writer.Write(outsideSimulation);
                writer.Write(slot);
                writer.Write(dropFrame);
                SendPacketToAllExceptSlot(memoryStream.ToArray(), P2PSend.Reliable, slot);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending peer drop signal: {e}");
        }
    }

    /// <summary>
    /// Returns true if the peer occupying <paramref name="playerSlot"/> has sent a packet
    /// within <paramref name="timeoutSeconds"/>. Used to tell a real host disconnect apart
    /// from a guest disconnect while waiting for the host's authoritative drop.
    /// </summary>
    public bool IsPeerResponsive(int playerSlot, float timeoutSeconds)
    {
        if (activeRoster == null)
        {
            return false;
        }

        float now = Time.unscaledTime;
        for (int i = 0; i < activeRoster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = activeRoster.Peers[i];
            if (peer == null || peer.PlayerSlot != playerSlot)
            {
                continue;
            }

            if (SameSteamId(peer.SteamId, SteamClient.SteamId))
            {
                return true; // The local player is always "responsive" to itself.
            }

            return peerLastPacketTime.TryGetValue(peer.SteamId, out float lastPacketTime)
                && now - lastPacketTime <= timeoutSeconds;
        }

        return false;
    }

    public void SendInputs()
    {
        if (RollbackManager.Instance == null || !HasRemotePeers() || !isRunning)
        {
            return;
        }

        int currentLocalFrame = GameManager.Instance.frameNumber;
        int latestTargetFrame = RollbackManager.Instance.LatestScheduledLocalInputFrame;
        if (latestTargetFrame < 0)
        {
            return;
        }

        // InputDelay may decrease while older future frames are already immutable. Resend through
        // the scheduler's actual high-water mark rather than recomputing an earlier target.
        int scheduledLead = Mathf.Max(0, latestTargetFrame - currentLocalFrame);
        int resendWindow = RollbackManager.Instance.MaxRollBackFrames
            + scheduledLead
            + Mathf.Max(14, EXTRA_RESEND_FRAMES);
        int firstFrameToSend = Math.Max(0, latestTargetFrame - resendWindow);

        int totalInputCount = latestTargetFrame - firstFrameToSend + 1;
        if (totalInputCount <= 0)
        {
            return;
        }

        int maxInputsPerPacket = Mathf.Clamp(MAX_INPUTS_PER_PACKET, 32, byte.MaxValue);
        int oldestRetainedFrame = Mathf.Max(0, latestTargetFrame - RollbackManager.InputArraySize + 1);
        firstFrameToSend = Mathf.Max(firstFrameToSend, oldestRetainedFrame);

        try
        {
            // A same-scene authoritative snapshot can move localFrame backward while already-sent
            // future input remains immutable. Send every chunk through the high-water mark instead
            // of keeping only the newest packet-sized tail, or an unreliably lost upcoming frame
            // could become impossible for a peer to recover.
            for (int chunkStartFrame = firstFrameToSend;
                 chunkStartFrame <= latestTargetFrame;
                 chunkStartFrame += maxInputsPerPacket)
            {
                int chunkInputCount = Mathf.Min(
                    maxInputsPerPacket,
                    latestTargetFrame - chunkStartFrame + 1);
                using (MemoryStream memoryStream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write((byte)0);
                    writer.Write(ActiveMatchSessionId);
                    writer.Write(GameManager.Instance != null
                        ? GameManager.Instance.GetOnlineTimelineEpoch()
                        : 0);
                    writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneSignature() : 0);
                    writer.Write(RollbackManager.Instance.localFrameAdvantage);
                    writer.Write(firstFrameToSend);
                    writer.Write(latestTargetFrame);
                    writer.Write(chunkStartFrame);
                    writer.Write((byte)chunkInputCount);

                    for (int i = 0; i < chunkInputCount; i++)
                    {
                        int frame = chunkStartFrame + i;
                        writer.Write(RollbackManager.Instance.GetOrSealScheduledLocalInput(frame));
                    }

                    SendPacketToAll(memoryStream.ToArray(), INPUT_SEND_TYPE);
                }
            }

            RecordSentInputTimestamp(latestTargetFrame);
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

    public void SendStateHash(int frame, uint hash, uint sharedHash, uint projectileHash, uint[] playerHashes, uint[] playerCoreHashes, uint[] playerSpellHashes, uint[] playerCoreSubHashes)
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
                WriteUIntArray(writer, playerCoreSubHashes);
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
            // Hoisted so the values written into the packet and the values stashed for the host's
            // own scene-in restore are guaranteed identical (captured once, same call stack).
            uint gameplayRngState = GameManager.Instance != null ? GameManager.Instance.CurrentRngState : 0u;
            int gameplayRandomCallCount = GameManager.Instance != null ? GameManager.Instance.randomCallCount : -1;
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_STAGE_SELECT);
                writer.Write(transitionId);
                writer.Write((byte)1);
                writer.Write(100000 + stageIndex);
                writer.Write(stageIndex);
                writer.Write(stageRngState);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.CurrentTotalRoundsPlayed : -1);
                writer.Write(gameplayRngState);
                writer.Write(gameplayRandomCallCount);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetConnectedPlayerSlotMask() : 0);
                lastStageSelectPacket = memoryStream.ToArray();
                lastStageSelectTransitionId = transitionId;
                SendPacketToAll(lastStageSelectPacket, P2PSend.Reliable);
            }

            // Every peer will adopt EXACTLY the rng state written above. The host must land on the
            // same value at the next round, no matter what its own sim consumes between this send
            // and its scene switch (e.g. respawn rolls in the dying seconds of the old round) --
            // see GameManager.StashHostGameplayRngFromStageSelect.
            GameManager.Instance?.StashHostGameplayRngFromStageSelect(gameplayRngState, gameplayRandomCallCount);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending stage select: {e}");
        }
    }

    public void SendPeerDropAcknowledgement(
        ulong matchSessionId,
        int timelineEpoch,
        int sceneSignature,
        int droppedSlot,
        int dropFrame)
    {
        if (!HasRemotePeers()
            || activeRoster == null
            || !activeRoster.HostSteamId.IsValid
            || SameSteamId(activeRoster.HostSteamId, SteamClient.SteamId))
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(PACKET_TYPE_PEER_DROP_ACK);
                writer.Write(matchSessionId);
                writer.Write(timelineEpoch);
                writer.Write(sceneSignature);
                writer.Write(droppedSlot);
                writer.Write(dropFrame);
                SendPacket(activeRoster.HostSteamId, memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error acknowledging peer drop: {e}");
        }
    }

    public void ResendLastStageSelect(int transitionId)
    {
        if (!HasRemotePeers()
            || lastStageSelectPacket == null
            || lastStageSelectTransitionId != transitionId)
        {
            return;
        }

        SendPacketToAll(lastStageSelectPacket, P2PSend.Reliable);
    }

    public void ResendLastRematchLobbyTransition(int transitionId)
    {
        if (!HasRemotePeers()
            || lastRematchLobbyTransitionPacket == null
            || lastRematchLobbyTransitionId != transitionId)
        {
            return;
        }

        SendPacketToAll(lastRematchLobbyTransitionPacket, P2PSend.Reliable);
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
                writer.Write(GameManager.Instance != null
                    ? GameManager.Instance.GetOnlineTimelineEpoch()
                    : 0);
                writer.Write(ComputeRosterContextHash(roster));
                writer.Write(frame);
                writer.Write(stateData.Length);
                writer.Write(stateData);
                writer.Write(forceApply);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneTypeCode() : (byte)0);
                writer.Write(GameManager.Instance != null ? GameManager.Instance.GetNetworkSceneSignature() : 0);
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
                writer.Write(GameManager.Instance != null
                    ? GameManager.Instance.GetOnlineTimelineEpoch()
                    : 0);
                writer.Write(ComputeRosterContextHash(roster));
                SendPacket(peerId, memoryStream.ToArray(), P2PSend.Reliable);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending lobby roster update: {e}");
        }
    }

    private void SendLobbyRosterSnapshotAck(
        SteamId peerId,
        ulong matchSessionId,
        int timelineEpoch,
        uint rosterContextHash)
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
                writer.Write(matchSessionId);
                writer.Write(timelineEpoch);
                writer.Write(rosterContextHash);
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
                    if (!roster.HostSteamId.IsValid
                        || !SameSteamId(senderSteamId, roster.HostSteamId)
                        || (activeRoster != null
                            && (activeRoster.MatchSessionId != roster.MatchSessionId
                                || !SameSteamId(
                                    activeRoster.HostSteamId,
                                    roster.HostSteamId)))
                        || (activeRoster == null
                            && SteamLobbyManager.Instance != null
                            && !SteamLobbyManager.Instance.IsCurrentLobbyMatchSession(
                                roster.MatchSessionId)))
                    {
                        return;
                    }

                    int timelineEpoch = reader.ReadInt32();
                    uint rosterContextHash = reader.ReadUInt32();
                    if (rosterContextHash != ComputeRosterContextHash(roster)
                        || (GameManager.Instance != null
                            && GameManager.Instance.isOnlineMatchActive
                            && timelineEpoch != GameManager.Instance.GetOnlineTimelineEpoch()))
                    {
                        return;
                    }

                    int frame = reader.ReadInt32();
                    int stateLength = reader.ReadInt32();
                    byte[] stateData = reader.ReadBytes(stateLength);
                    bool forceApply = reader.BaseStream.Position < reader.BaseStream.Length && reader.ReadBoolean();
                    byte snapshotSceneType = reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadByte() : (byte)0;
                    int snapshotSceneSignature = reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadInt32() : 0;
                    bool applied = GameManager.Instance != null
                        && GameManager.Instance.ApplyOnlineLobbyRosterSnapshot(
                            roster,
                            frame,
                            stateData,
                            forceApply,
                            snapshotSceneType,
                            snapshotSceneSignature,
                            timelineEpoch);
                    if (applied)
                    {
                        UpdateRoster(roster);
                        SendLobbyRosterSnapshotAck(
                            senderSteamId,
                            roster.MatchSessionId,
                            timelineEpoch,
                            rosterContextHash);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_LOBBY_ROSTER_SNAPSHOT_ACK)
                {
                    ulong matchSessionId = reader.ReadUInt64();
                    int timelineEpoch = reader.ReadInt32();
                    uint rosterContextHash = reader.ReadUInt32();
                    if (activeRoster != null
                        && activeRoster.MatchSessionId == matchSessionId
                        && GameManager.Instance != null
                        && timelineEpoch == GameManager.Instance.GetOnlineTimelineEpoch()
                        && rosterContextHash == ComputeRosterContextHash(activeRoster))
                    {
                        GameManager.Instance?.OnOnlineLobbySnapshotAcknowledged(senderSteamId);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_LOBBY_ROSTER_UPDATE)
                {
                    OnlineMatchRoster roster = ReadRoster(reader);
                    if (!roster.HostSteamId.IsValid
                        || !SameSteamId(senderSteamId, roster.HostSteamId)
                        || (activeRoster != null
                            && (activeRoster.MatchSessionId != roster.MatchSessionId
                                || !SameSteamId(
                                    activeRoster.HostSteamId,
                                    roster.HostSteamId)))
                        || (activeRoster == null
                            && SteamLobbyManager.Instance != null
                            && !SteamLobbyManager.Instance.IsCurrentLobbyMatchSession(
                                roster.MatchSessionId)))
                    {
                        return;
                    }

                    int timelineEpoch = reader.ReadInt32();
                    uint rosterContextHash = reader.ReadUInt32();
                    if (rosterContextHash != ComputeRosterContextHash(roster)
                        || (GameManager.Instance != null
                            && GameManager.Instance.isOnlineMatchActive
                            && timelineEpoch != GameManager.Instance.GetOnlineTimelineEpoch()))
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
                    uint[] playerCoreSubHashes = ReadUIntArray(reader);
                    RollbackManager.Instance.OnRemoteStateHash(senderSlot, frame, hash, sharedHash, projectileHash, playerHashes, playerCoreHashes, playerSpellHashes, playerCoreSubHashes);
                    return;
                }

                if (packetType == PACKET_TYPE_STAGE_SELECT)
                {
                    // Stage selection (including an End -> Gameplay rematch) is host-authoritative.
                    if (activeRoster != null
                        && activeRoster.HostSteamId.IsValid
                        && !SameSteamId(senderSteamId, activeRoster.HostSteamId))
                    {
                        return;
                    }

                    int transitionId = reader.ReadInt32();
                    byte packetSceneType = reader.ReadByte();
                    int packetSceneSignature = reader.ReadInt32();
                    int stageIndex = reader.ReadInt32();
                    uint stageRngState = reader.ReadUInt32();
                    int totalRoundsPlayed = reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadInt32() : -1;
                    uint gameplayRngState = reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadUInt32() : 0u;
                    int randomCallCount = reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadInt32() : -1;
                    int connectedPlayerSlotMask = reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadInt32() : -1;
                    GameManager.Instance?.HandleOnlineStageSelect(transitionId, packetSceneType, packetSceneSignature, stageIndex, stageRngState, totalRoundsPlayed, gameplayRngState, randomCallCount, connectedPlayerSlotMask);
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
                    bool isRecoveryResponse = reader.BaseStream.Position < reader.BaseStream.Length
                        && reader.ReadBoolean();
                    if (senderSlot >= 0)
                    {
                        GameManager.Instance?.OnPeerSceneTransitionReady(
                            senderSlot,
                            transitionId,
                            sceneType,
                            sceneSignature,
                            isRecoveryResponse);
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

                if (packetType == PACKET_TYPE_REMATCH_LOBBY_TRANSITION)
                {
                    if (activeRoster == null
                        || !activeRoster.HostSteamId.IsValid
                        || !SameSteamId(senderSteamId, activeRoster.HostSteamId))
                    {
                        return;
                    }

                    int transitionId = reader.ReadInt32();
                    int connectedPlayerSlotMask = reader.ReadInt32();
                    int rematchSeed = reader.BaseStream.Length - reader.BaseStream.Position >= sizeof(int)
                        ? reader.ReadInt32()
                        : 0;
                    GameManager.Instance?.HandleOnlineRematchLobbyTransition(
                        transitionId,
                        connectedPlayerSlotMask,
                        rematchSeed);
                    return;
                }

                if (packetType == PACKET_TYPE_END_OPTION_STATE)
                {
                    int epoch = reader.ReadInt32();
                    byte option = reader.ReadByte();
                    bool confirmed = reader.ReadBoolean();
                    uint revision = reader.ReadUInt32();
                    if (senderSlot >= 0)
                    {
                        GameEndScreen.ActiveInstance?.ReceiveOnlineOptionState(
                            senderSlot,
                            epoch,
                            option,
                            confirmed,
                            revision);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_END_OPTION_RESULT)
                {
                    int epoch = reader.ReadInt32();
                    byte result = reader.ReadByte();
                    if (senderSlot >= 0
                        && GameManager.Instance != null
                        && GameManager.Instance.IsOnlineHostSlot(senderSlot))
                    {
                        GameEndScreen.ActiveInstance?.ReceiveOnlineOptionResult(senderSlot, epoch, result);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_END_OPTION_RESULT_ACK)
                {
                    int epoch = reader.ReadInt32();
                    if (senderSlot >= 0
                        && GameManager.Instance != null
                        && GameManager.Instance.IsOnlineHostAuthority())
                    {
                        GameEndScreen.ActiveInstance?.ReceiveOnlineOptionResultAcknowledgement(senderSlot, epoch);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_INITIAL_INPUT_STREAM_READY)
                {
                    ulong matchSessionId = reader.ReadUInt64();
                    int timelineEpoch = reader.ReadInt32();
                    int sceneSignature = reader.ReadInt32();
                    int matchSeed = reader.ReadInt32();
                    if (senderSlot >= 0 && RollbackManager.Instance != null)
                    {
                        RollbackManager.Instance.ReceiveInitialInputStreamReady(
                            senderSlot,
                            matchSessionId,
                            timelineEpoch,
                            sceneSignature,
                            matchSeed);
                    }
                    return;
                }

                if (packetType == PACKET_TYPE_MATCH_ABORT)
                {
                    ulong matchSessionId = reader.ReadUInt64();
                    int timelineEpoch = reader.ReadInt32();
                    string reason = reader.ReadString();
                    // Only the roster host may terminate the match for every peer.
                    if (activeRoster == null
                        || !activeRoster.HostSteamId.IsValid
                        || !SameSteamId(senderSteamId, activeRoster.HostSteamId)
                        || activeRoster.MatchSessionId != matchSessionId
                        || GameManager.Instance == null
                        || !GameManager.Instance.isOnlineMatchActive
                        // A client stuck before the host's scene transition may be one epoch
                        // behind and still needs this rescue. An abort older than our current
                        // epoch is delayed traffic from a scene that already finished.
                        || timelineEpoch < GameManager.Instance.GetOnlineTimelineEpoch())
                    {
                        return;
                    }

                    Debug.LogWarning($"[OnlineMatch] Host aborted match startup: {reason}");
                    GameManager.Instance.ResetToMainMenuAfterHostDisconnect(
                        $"Host aborted match startup: {reason}");
                    return;
                }

                if (packetType == PACKET_TYPE_PEER_DROP)
                {
                    ulong matchSessionId = reader.ReadUInt64();
                    int timelineEpoch = reader.ReadInt32();
                    int sceneSignature = reader.ReadInt32();
                    bool outsideSimulation = reader.ReadBoolean();
                    int droppedSlot = reader.ReadInt32();
                    int dropFrame = reader.ReadInt32();
                    // Only the host adjudicates drops; ignore any spoofed/non-host sender.
                    if (activeRoster == null
                        || !activeRoster.HostSteamId.IsValid
                        || !SameSteamId(senderSteamId, activeRoster.HostSteamId)
                        || activeRoster.MatchSessionId != matchSessionId
                        || GameManager.Instance == null
                        || !GameManager.Instance.isOnlineMatchActive
                        || timelineEpoch != GameManager.Instance.GetOnlineTimelineEpoch())
                    {
                        return;
                    }

                    bool exactSceneContext =
                        sceneSignature == GameManager.Instance.GetNetworkSceneSignature();
                    bool startupHold = RollbackManager.Instance != null
                        && RollbackManager.Instance.IsHoldingForInitialRemoteInputStreams();
                    if (!exactSceneContext
                        && !GameManager.Instance.isTransitioning
                        && !(outsideSimulation && startupHold))
                    {
                        return;
                    }

                    // Transition/End drops are explicitly tagged by the host. Apply that
                    // topology change the same way even if this packet lands just after
                    // the transition completed and the new deterministic scene is held
                    // at frame zero. This removes arrival-time-dependent survivor sets.
                    if (outsideSimulation)
                    {
                        bool slotAlreadyDisconnected =
                            !GameManager.Instance.IsPlayerSlotConnected(droppedSlot);
                        bool canStillApplyOutsideSimulation =
                            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "End"
                            || GameManager.Instance.isTransitioning
                            || startupHold;

                        if (!slotAlreadyDisconnected && !canStillApplyOutsideSimulation)
                        {
                            // This survivor already advanced after the host applied a
                            // transition-time frame-zero topology change. Applying it now
                            // would create a different baseline; leave the invalid branch.
                            SendPeerDropAcknowledgement(
                                matchSessionId,
                                timelineEpoch,
                                sceneSignature,
                                droppedSlot,
                                dropFrame);
                            GameManager.Instance.ResetToMainMenuAfterHostDisconnect(
                                $"Received a startup roster change after local simulation began " +
                                $"(P{droppedSlot + 1} disconnected)");
                            return;
                        }

                        GameManager.Instance.ApplyPeerDropOutsideSimulation(droppedSlot, dropFrame);
                        if (GameManager.Instance.IsPlayerSlotConnected(droppedSlot))
                        {
                            SendPeerDropAcknowledgement(
                                matchSessionId,
                                timelineEpoch,
                                sceneSignature,
                                droppedSlot,
                                dropFrame);
                            GameManager.Instance.ResetToMainMenuAfterHostDisconnect(
                                $"Could not apply the host's startup roster change " +
                                $"(P{droppedSlot + 1} disconnected)");
                            return;
                        }

                        SendPeerDropAcknowledgement(
                            matchSessionId,
                            timelineEpoch,
                            sceneSignature,
                            droppedSlot,
                            dropFrame);
                        return;
                    }

                    // A client still held at frame zero cannot safely apply a drop chosen by a
                    // survivor that has already released the startup barrier: the same nominal
                    // drop frame would be clamped against two different simulation timelines.
                    // Leave the match instead of manufacturing another deterministic branch.
                    if (startupHold && exactSceneContext)
                    {
                        SendPeerDropAcknowledgement(
                            matchSessionId,
                            timelineEpoch,
                            sceneSignature,
                            droppedSlot,
                            dropFrame);
                        Debug.LogWarning(
                            $"[OnlineMatch] P{droppedSlot + 1} dropped while this client was still " +
                            "waiting for the startup input handshake; returning to the solo lobby.");
                        GameManager.Instance?.ResetToMainMenuAfterHostDisconnect(
                            $"Match roster changed before startup synchronization completed " +
                            $"(P{droppedSlot + 1} disconnected)");
                        return;
                    }

                    if (GameManager.Instance != null
                        && (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "End"
                            || GameManager.Instance.isTransitioning))
                    {
                        GameManager.Instance.ApplyPeerDropOutsideSimulation(droppedSlot, dropFrame);
                    }
                    else
                    {
                        RollbackManager.Instance.DropRemoteSlot(droppedSlot, dropFrame);
                    }
                    SendPeerDropAcknowledgement(
                        matchSessionId,
                        timelineEpoch,
                        sceneSignature,
                        droppedSlot,
                        dropFrame);
                    return;
                }

                if (packetType == PACKET_TYPE_PEER_DROP_ACK)
                {
                    ulong matchSessionId = reader.ReadUInt64();
                    int timelineEpoch = reader.ReadInt32();
                    reader.ReadInt32(); // scene signature is diagnostic context for this drop
                    int droppedSlot = reader.ReadInt32();
                    int dropFrame = reader.ReadInt32();
                    if (senderSlot >= 0
                        && activeRoster != null
                        && activeRoster.MatchSessionId == matchSessionId
                        && GameManager.Instance != null
                        && timelineEpoch == GameManager.Instance.GetOnlineTimelineEpoch()
                        && GameManager.Instance.IsOnlineHostAuthority())
                    {
                        GameManager.Instance.OnPeerDropAcknowledged(senderSlot, droppedSlot, dropFrame);
                    }
                    return;
                }

                if (packetType == 0)
                {
                    if (GameManager.Instance != null && GameManager.Instance.isWaitingForOpponent)
                    {
                        return;
                    }

                    ulong matchSessionId = reader.ReadUInt64();
                    int timelineEpoch = reader.ReadInt32();
                    int packetSceneSignature = reader.ReadInt32();
                    int remoteFrameAdvantage = reader.ReadInt32();
                    int batchFirstFrame = reader.ReadInt32();
                    int batchHighWaterFrame = reader.ReadInt32();
                    int startFrame = reader.ReadInt32();
                    int inputCount = reader.ReadByte();
                    int newestPacketFrame = startFrame + inputCount - 1;

                    if (inputCount <= 0
                        || batchFirstFrame < 0
                        || startFrame < 0
                        || batchFirstFrame > startFrame
                        || batchHighWaterFrame < newestPacketFrame
                        || batchFirstFrame > batchHighWaterFrame)
                    {
                        Debug.LogWarning($"Rejected malformed input packet from {senderSteamId}: batch={batchFirstFrame}-{batchHighWaterFrame}, chunk={startFrame}-{newestPacketFrame}.");
                        return;
                    }

                    bool isIntentionalMultiChunkBackfill =
                        (long)batchHighWaterFrame - batchFirstFrame + 1L > inputCount;

                    if (senderSlot < 0 && activeRoster != null)
                    {
                        return;
                    }

                    if (activeRoster == null
                        || activeRoster.MatchSessionId != matchSessionId
                        || GameManager.Instance == null)
                    {
                        return;
                    }

                    int localTimelineEpoch = GameManager.Instance.GetOnlineTimelineEpoch();
                    if (timelineEpoch < localTimelineEpoch)
                    {
                        return;
                    }

                    if (timelineEpoch > localTimelineEpoch
                        || packetSceneSignature != GameManager.Instance.GetNetworkSceneSignature())
                    {
                        // Instead of silently discarding the entire packet (which
                        // dropped every input frame inside, every peer's advantage/ping update,
                        // and could mask their inputs for the whole transition window) we now
                        // buffer the most recent mismatched packet and replay it the moment our
                        // timeline/scene catches up. Host-side correction is still triggered
                        // for a same-epoch scene mismatch.
                        if (timelineEpoch == localTimelineEpoch)
                        {
                            GameManager.Instance.HandleInputSceneSignatureMismatch(senderSlot, packetSceneSignature);
                        }
                        BufferSceneMismatchedInputPacket(senderSteamId, messageData);
                        return;
                    }

                    for (int i = 0; i < inputCount; i++)
                    {
                        int frame = startFrame + i;
                        ulong input = reader.ReadUInt64();

                        if (senderSlot >= 0)
                        {
                            RollbackManager.Instance.SetRemoteInput(
                                senderSlot,
                                frame,
                                input,
                                batchHighWaterFrame,
                                isIntentionalMultiChunkBackfill);
                        }
                        else if (!RollbackManager.Instance.receivedInputs.ContainsKey(frame))
                        {
                            RollbackManager.Instance.SetOpponentInput(
                                frame,
                                input,
                                isIntentionalMultiChunkBackfill);
                        }
                        else
                        {
                            ulong existingInput = RollbackManager.Instance.receivedInputs.GetInput(frame);
                            if (existingInput != input && frame > RollbackManager.Instance.syncFrame)
                            {
                                RollbackManager.Instance.SetOpponentInput(
                                    frame,
                                    input,
                                    isIntentionalMultiChunkBackfill);
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

    public void DropPeerTransport(int slot)
    {
        if (slot < 0)
        {
            return;
        }

        locallyRemovedPeerSlots.Add(slot);
        if (activeRoster != null && activeRoster.TryGetSteamIdForSlot(slot, out SteamId peerId))
        {
            ForgetPeerTransport(peerId, closeSession: true);
        }
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

            if (IsPeerSlotRemovedFromTransport(peer.PlayerSlot))
            {
                continue;
            }

            any |= SendPacket(peer.SteamId, data, sendType);
        }

        return any;
    }

    // Match-abort notifications bypass the optional stress-test delay/drop queue.
    // They must be submitted to Steam's reliable channel before local StopMatch clears
    // this manager's roster and queued packets.
    private bool SendPacketToAllDirect(byte[] data, P2PSend sendType)
    {
        if (activeRoster?.Peers == null || data == null || data.Length == 0)
        {
            return false;
        }

        bool any = false;
        foreach (OnlineMatchPeerInfo peer in activeRoster.Peers)
        {
            if (peer == null
                || SameSteamId(peer.SteamId, SteamClient.SteamId)
                || IsPeerSlotRemovedFromTransport(peer.PlayerSlot))
            {
                continue;
            }

            any |= SteamNetworking.SendP2PPacket(
                peer.SteamId,
                data,
                data.Length,
                MATCH_MESSAGE_CHANNEL,
                sendType);
        }

        return any;
    }

    private bool SendPacketToAllExceptSlot(byte[] data, P2PSend sendType, int excludedSlot)
    {
        bool any = false;
        foreach (OnlineMatchPeerInfo peer in activeRoster.Peers)
        {
            if (peer == null || peer.PlayerSlot == excludedSlot || SameSteamId(peer.SteamId, SteamClient.SteamId))
            {
                continue;
            }

            if (IsPeerSlotRemovedFromTransport(peer.PlayerSlot))
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
        writer.Write(roster.MatchSessionId);
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

    private static uint ComputeRosterContextHash(OnlineMatchRoster roster)
    {
        const uint fnvOffset = 2166136261u;
        const uint fnvPrime = 16777619u;
        uint hash = fnvOffset;

        void MixUInt64(ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= fnvPrime;
            }
        }

        void MixInt32(int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(bits >> shift);
                hash *= fnvPrime;
            }
        }

        if (roster == null)
        {
            return 0u;
        }

        MixUInt64(roster.MatchSessionId);
        MixUInt64(roster.HostSteamId.Value);

        List<OnlineMatchPeerInfo> orderedPeers = roster.Peers != null
            ? roster.Peers.FindAll(peer => peer != null)
            : new List<OnlineMatchPeerInfo>();
        orderedPeers.Sort((a, b) => a.PlayerSlot.CompareTo(b.PlayerSlot));
        MixInt32(orderedPeers.Count);
        for (int i = 0; i < orderedPeers.Count; i++)
        {
            MixInt32(orderedPeers[i].PlayerSlot);
            MixUInt64(orderedPeers[i].SteamId.Value);
        }

        return hash;
    }

    private OnlineMatchRoster ReadRoster(BinaryReader reader)
    {
        OnlineMatchRoster roster = new OnlineMatchRoster
        {
            HostSteamId = reader.ReadUInt64(),
            MatchSessionId = reader.ReadUInt64(),
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
        if (frame > highestTimestampedLocalInputFrame)
        {
            sentFrameTimes.Insert(frame, now);
            highestTimestampedLocalInputFrame = frame;
        }

        foreach (OnlineMatchPeerInfo peer in activeRoster.Peers)
        {
            if (peer == null || SameSteamId(peer.SteamId, SteamClient.SteamId))
            {
                continue;
            }

            if (IsPeerSlotRemovedFromTransport(peer.PlayerSlot))
            {
                continue;
            }

            if (!sentFrameTimesByPeer.TryGetValue(peer.SteamId, out CircularArray<float> peerTimes))
            {
                peerTimes = new CircularArray<float>(RollbackManager.InputArraySize);
                sentFrameTimesByPeer[peer.SteamId] = peerTimes;
            }

            int highestTimestampedFrame = highestTimestampedInputFrameByPeer.TryGetValue(
                peer.SteamId,
                out int existingHighest)
                ? existingHighest
                : -1;
            if (frame <= highestTimestampedFrame)
            {
                continue;
            }

            peerTimes.Insert(frame, now);
            highestTimestampedInputFrameByPeer[peer.SteamId] = frame;
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

        if (IsPeerSlotRemovedFromTransport(ResolveSlot(peerId)))
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
                if (IsPeerSlotRemovedFromTransport(ResolveSlot(packet.peerId)))
                {
                    continue;
                }

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
                if (ShouldIgnorePacketFromPeer(packet.peerId))
                {
                    continue;
                }

                ProcessPacket(packet.peerId, packet.data);
            }
        }
    }

    private bool ShouldIgnorePacketFromPeer(SteamId peerId)
    {
        return IsPeerSlotRemovedFromTransport(ResolveSlot(peerId));
    }

    private bool IsPeerSlotRemovedFromTransport(int slot)
    {
        if (slot < 0)
        {
            return false;
        }

        if (locallyRemovedPeerSlots.Contains(slot))
        {
            return true;
        }

        return GameManager.Instance != null
            && GameManager.Instance.isOnlineMatchActive
            && slot < GameManager.Instance.playerCount
            && !GameManager.Instance.IsPlayerSlotConnected(slot);
    }

    private void ForgetPeerTransport(SteamId peerId, bool closeSession)
    {
        if (!peerId.IsValid)
        {
            return;
        }

        connectedPeers.Remove(peerId);
        remoteReadyReceived.Remove(peerId);
        handshakeSentToPeers.Remove(peerId);
        handshakeSeenFromPeers.Remove(peerId);
        peerHighestRemoteFrameSeen.Remove(peerId);
        peerPingMs.Remove(peerId);
        peerLastPacketTime.Remove(peerId);
        peerLastHandshakeSendTime.Remove(peerId);
        sentFrameTimesByPeer.Remove(peerId);
        highestTimestampedInputFrameByPeer.Remove(peerId);
        sceneMismatchedInputByPeer.Remove(peerId);
        sceneMismatchedInputAgeByPeer.Remove(peerId);
        lastLoggedMismatchSignatureByPeer.Remove(peerId);
        outboundQueue.RemoveAll(packet => SameSteamId(packet.peerId, peerId));
        inboundQueue.RemoveAll(packet => SameSteamId(packet.peerId, peerId));

        if (closeSession)
        {
            SteamNetworking.CloseP2PSessionWithUser(peerId);
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
