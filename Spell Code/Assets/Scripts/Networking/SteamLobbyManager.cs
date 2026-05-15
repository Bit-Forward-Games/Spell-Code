using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;

public class SteamLobbyManager : MonoBehaviour
{
    private const int TargetOnlineLobbySize = 4;
    private const int MinimumOnlineLobbyStartSize = 2;
    private const string MatchReadyKey = "matchReady";
    private const string MatchStartTokenKey = "matchStartToken";
    private const string LobbySlotKeyPrefix = "slot_";

    public static SteamLobbyManager Instance { get; private set; }

    private Lobby? currentLobby;
    private bool isHostingFlow;
    private bool isShuttingDown;
    private Result lastLobbyCreateResult = Result.None;
    private Lobby? lastLobbyCreated;
    private uint hostFlowVersion;
    private bool startedCurrentLobbyMatch;
    private string currentMatchStartToken = string.Empty;
    private readonly HashSet<SteamId> activeMatchPeerIds = new HashSet<SteamId>();
    private readonly Dictionary<SteamId, float> pendingLobbySnapshotPeers = new Dictionary<SteamId, float>();
    private const float LobbySnapshotResendSeconds = 1f;

    [SerializeField] private bool debugLogs = true;
    [SerializeField] private KeyCode inviteOverlayKey = KeyCode.F6;

    public bool IsInLobby => currentLobby.HasValue;
    public bool IsHostingFlow => isHostingFlow;

    public bool TryOpenInviteOverlay()
    {
        if (isShuttingDown || !SteamClient.IsValid || !currentLobby.HasValue)
        {
            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] TryOpenInviteOverlay blocked. ShuttingDown={isShuttingDown} SteamValid={SteamClient.IsValid} HasLobby={currentLobby.HasValue}");
            }
            return false;
        }

        if (currentLobby.Value.Owner.Id != SteamClient.SteamId)
        {
            if (debugLogs)
            {
                Debug.Log("[SteamLobbyManager] TryOpenInviteOverlay blocked. Not lobby owner.");
            }
            return false;
        }

        if (debugLogs)
        {
            Debug.Log($"[SteamLobbyManager] Opening invite overlay. OverlayEnabled={SteamUtils.IsOverlayEnabled} LobbyId={currentLobby.Value.Id.Value}");
        }
        SteamFriends.OpenGameInviteOverlay(currentLobby.Value.Id);
        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SteamMatchmaking.OnLobbyEntered += HandleLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined += HandleLobbyMemberJoined;
        SteamMatchmaking.OnLobbyCreated += HandleLobbyCreated;
        SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
    }

    private void OnDisable()
    {
        SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined -= HandleLobbyMemberJoined;
        SteamMatchmaking.OnLobbyCreated -= HandleLobbyCreated;
        SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
    }

    private void Update()
    {
        if (isShuttingDown || !currentLobby.HasValue)
        {
            return;
        }

        if (GameManager.Instance != null)
        {
            if (Input.GetKeyDown(inviteOverlayKey))
            {
                TryOpenInviteOverlay();
            }

            UpdateLobbyJoinableState(currentLobby.Value);
            TryStartOnlineMatchFromLobby(currentLobby.Value);
            TrySendPendingLobbySnapshots(currentLobby.Value);
        }
    }

    public async void HostAndInvite()
    {
        if (isShuttingDown || !SteamClient.IsValid)
        {
            Debug.LogError("Steam is not running or is shutting down. Cannot host online match.");
            return;
        }

        if (isHostingFlow)
        {
            return;
        }

        isHostingFlow = true;
        isShuttingDown = false;
        hostFlowVersion++;
        uint currentHostFlowVersion = hostFlowVersion;
        LeaveLobbyInternal();

        try
        {
            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Creating lobby. SteamId={SteamClient.SteamId.Value} AppId={SteamClient.AppId} OverlayEnabled={SteamUtils.IsOverlayEnabled}");
            }

            Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(TargetOnlineLobbySize);
            if (isShuttingDown || currentHostFlowVersion != hostFlowVersion || !SteamClient.IsValid)
            {
                if (lobby.HasValue)
                {
                    lobby.Value.Leave();
                }
                isHostingFlow = false;
                return;
            }

            if (!lobby.HasValue)
            {
                if (lastLobbyCreateResult == Result.OK && lastLobbyCreated.HasValue)
                {
                    lobby = lastLobbyCreated;
                }
                else
                {
                    Debug.LogError($"Failed to create Steam lobby. Result={lastLobbyCreateResult}");
                    isHostingFlow = false;
                    return;
                }
            }

            if (!lobby.HasValue)
            {
                Debug.LogError($"Failed to create Steam lobby. Result={lastLobbyCreateResult}");
                isHostingFlow = false;
                return;
            }

            currentLobby = lobby.Value;
            currentLobby.Value.SetFriendsOnly();
            currentLobby.Value.SetJoinable(true);
            currentLobby.Value.SetData("hostId", SteamClient.SteamId.Value.ToString());
            currentLobby.Value.SetData("targetSize", TargetOnlineLobbySize.ToString());
            currentLobby.Value.SetData(MatchReadyKey, "0");
            currentLobby.Value.SetData(MatchStartTokenKey, string.Empty);
            startedCurrentLobbyMatch = false;
            currentMatchStartToken = string.Empty;

            if (!isShuttingDown)
            {
                SteamFriends.OpenGameInviteOverlay(currentLobby.Value.Id);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception while creating lobby: {e.Message}");
            isHostingFlow = false;
        }
    }

    public void LeaveLobby()
    {
        LeaveLobbyInternal();
    }

    public void Shutdown()
    {
        isShuttingDown = true;
        hostFlowVersion++;
        LeaveLobbyInternal();
    }

    private void LeaveLobbyInternal()
    {
        if (currentLobby.HasValue)
        {
            currentLobby.Value.Leave();
            currentLobby = null;
        }

        isHostingFlow = false;
        startedCurrentLobbyMatch = false;
        currentMatchStartToken = string.Empty;
        activeMatchPeerIds.Clear();
        pendingLobbySnapshotPeers.Clear();
    }

    private async void HandleGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        if (isShuttingDown || !SteamClient.IsValid)
        {
            return;
        }

        try
        {
            Lobby? joined = await SteamMatchmaking.JoinLobbyAsync(lobby.Id);
            if (joined.HasValue)
            {
                currentLobby = joined.Value;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
        }
    }

    private void HandleLobbyEntered(Lobby lobby)
    {
        if (isShuttingDown)
        {
            lobby.Leave();
            return;
        }

        currentLobby = lobby;
        startedCurrentLobbyMatch = false;

        TryStartOnlineMatchFromLobby(lobby);
    }

    private void HandleLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        if (isShuttingDown)
        {
            return;
        }

        if (!currentLobby.HasValue || lobby.Id != currentLobby.Value.Id)
        {
            return;
        }

        if (lobby.Owner.Id != SteamClient.SteamId)
        {
            return;
        }

        if (friend.Id == SteamClient.SteamId)
        {
            return;
        }

        TryStartOnlineMatchFromLobby(lobby);
    }

    private void TryStartOnlineMatchFromLobby(Lobby lobby)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager not found; cannot start online match.");
            return;
        }

        OnlineMatchRoster roster = BuildRoster(lobby);
        if (roster == null)
        {
            return;
        }

        string expectedMatchStartToken = BuildMatchStartToken(lobby, roster);
        if (GameManager.Instance.isOnlineMatchActive)
        {
            if (lobby.Owner.Id == SteamClient.SteamId && roster.PlayerCount >= MinimumOnlineLobbyStartSize)
            {
                string currentReady = lobby.GetData(MatchReadyKey);
                string currentToken = lobby.GetData(MatchStartTokenKey);
                if (currentReady != "1" || currentToken != expectedMatchStartToken)
                {
                    lobby.SetData(MatchReadyKey, "1");
                    lobby.SetData(MatchStartTokenKey, expectedMatchStartToken);
                }
            }

            List<SteamId> newPeers = GetNewRosterPeers(roster);
            if (newPeers.Count == 0 || !GameManager.Instance.CanStartOrRefreshOnlineLobby(roster))
            {
                return;
            }

            if (GameManager.Instance.TryRefreshOnlineLobbyRoster(roster))
            {
                RememberRosterPeers(roster);
                if (lobby.Owner.Id == SteamClient.SteamId)
                {
                    for (int i = 0; i < newPeers.Count; i++)
                    {
                        QueueLobbySnapshotPeer(newPeers[i]);
                    }
                }
            }
            return;
        }

        bool canStartOrRefresh = GameManager.Instance.CanStartOrRefreshOnlineLobby(roster);
        if (!canStartOrRefresh)
        {
            return;
        }

        if (lobby.Owner.Id == SteamClient.SteamId && roster.PlayerCount >= MinimumOnlineLobbyStartSize)
        {
            string currentReady = lobby.GetData(MatchReadyKey);
            string currentToken = lobby.GetData(MatchStartTokenKey);
            if (currentReady != "1" || currentToken != expectedMatchStartToken)
            {
                lobby.SetData(MatchReadyKey, "1");
                lobby.SetData(MatchStartTokenKey, expectedMatchStartToken);
            }
        }

        string matchReady = lobby.GetData(MatchReadyKey);
        string matchStartToken = lobby.GetData(MatchStartTokenKey);

        if (roster.PlayerCount < MinimumOnlineLobbyStartSize || matchReady != "1" || matchStartToken != expectedMatchStartToken)
        {
            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Waiting for at least one guest before starting. Members={roster?.PlayerCount ?? 0}/{MinimumOnlineLobbyStartSize}");
            }
            return;
        }

        if (startedCurrentLobbyMatch && currentMatchStartToken == matchStartToken)
        {
            return;
        }

        startedCurrentLobbyMatch = true;
        currentMatchStartToken = matchStartToken;
        GameManager.Instance.StartOnlineMatch(roster);
        RememberRosterPeers(roster);
        isHostingFlow = false;
    }

    private void UpdateLobbyJoinableState(Lobby lobby)
    {
        if (lobby.Owner.Id != SteamClient.SteamId || GameManager.Instance == null)
        {
            return;
        }

        lobby.SetJoinable(GameManager.Instance.IsOnlineLobbyAcceptingAdditionalPlayers());
    }

    public bool IsCurrentLobbyMember(SteamId steamId)
    {
        if (!currentLobby.HasValue || !steamId.IsValid)
        {
            return false;
        }

        foreach (Friend member in currentLobby.Value.Members)
        {
            if (member.Id == steamId)
            {
                return true;
            }
        }

        return false;
    }

    public void OnLobbySnapshotAcknowledged(SteamId peerId)
    {
        pendingLobbySnapshotPeers.Remove(peerId);
    }

    private void QueueLobbySnapshotPeer(SteamId peerId)
    {
        if (!peerId.IsValid || peerId == SteamClient.SteamId)
        {
            return;
        }

        pendingLobbySnapshotPeers[peerId] = -LobbySnapshotResendSeconds;
    }

    private void TrySendPendingLobbySnapshots(Lobby lobby)
    {
        if (pendingLobbySnapshotPeers.Count == 0
            || lobby.Owner.Id != SteamClient.SteamId
            || GameManager.Instance == null
            || !GameManager.Instance.isOnlineMatchActive)
        {
            return;
        }

        float now = Time.unscaledTime;
        List<SteamId> peers = new List<SteamId>(pendingLobbySnapshotPeers.Keys);
        for (int i = 0; i < peers.Count; i++)
        {
            SteamId peerId = peers[i];
            if (!IsCurrentLobbyMember(peerId))
            {
                pendingLobbySnapshotPeers.Remove(peerId);
                continue;
            }

            float lastSendTime = pendingLobbySnapshotPeers[peerId];
            if (now - lastSendTime < LobbySnapshotResendSeconds)
            {
                continue;
            }

            pendingLobbySnapshotPeers[peerId] = now;
            GameManager.Instance.TrySendOnlineLobbySnapshotToPeer(peerId);
        }
    }

    private string BuildMatchStartToken(Lobby lobby, OnlineMatchRoster roster)
    {
        string token = lobby.Id.Value.ToString();
        if (roster?.Peers == null)
        {
            return token;
        }

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer == null)
            {
                continue;
            }

            token += $":{peer.PlayerSlot}-{peer.SteamId.Value}";
        }

        return token;
    }

    private OnlineMatchRoster BuildRoster(Lobby lobby)
    {
        List<SteamId> members = new List<SteamId>();
        foreach (Friend member in lobby.Members)
        {
            if (member.Id.IsValid)
            {
                members.Add(member.Id);
            }
        }

        if (members.Count == 0)
        {
            return null;
        }

        Dictionary<SteamId, int> assignedSlots = BuildAssignedSlots(lobby, members);
        for (int i = 0; i < members.Count; i++)
        {
            if (!assignedSlots.ContainsKey(members[i]))
            {
                if (debugLogs)
                {
                    Debug.Log($"[SteamLobbyManager] Waiting for slot metadata for member {members[i].Value}.");
                }
                return null;
            }
        }

        members.Sort((a, b) => assignedSlots[a].CompareTo(assignedSlots[b]));

        OnlineMatchRoster roster = new OnlineMatchRoster
        {
            HostSteamId = lobby.Owner.Id
        };

        for (int i = 0; i < members.Count; i++)
        {
            SteamId memberId = members[i];
            int playerSlot = assignedSlots[memberId];
            roster.Peers.Add(new OnlineMatchPeerInfo
            {
                SteamId = memberId,
                PlayerSlot = playerSlot
            });

            if (memberId == SteamClient.SteamId)
            {
                roster.LocalPlayerSlot = playerSlot;
            }
        }

        return roster;
    }

    private Dictionary<SteamId, int> BuildAssignedSlots(Lobby lobby, List<SteamId> members)
    {
        Dictionary<SteamId, int> assignedSlots = new Dictionary<SteamId, int>();
        HashSet<int> usedSlots = new HashSet<int>();

        if (lobby.Owner.Id.IsValid && members.Contains(lobby.Owner.Id))
        {
            assignedSlots[lobby.Owner.Id] = 0;
            usedSlots.Add(0);
        }

        for (int i = 0; i < members.Count; i++)
        {
            SteamId memberId = members[i];
            if (memberId == lobby.Owner.Id)
            {
                continue;
            }

            string slotText = lobby.GetData(GetSlotKey(memberId));
            if (int.TryParse(slotText, out int slot) && slot > 0 && slot < TargetOnlineLobbySize && !usedSlots.Contains(slot))
            {
                assignedSlots[memberId] = slot;
                usedSlots.Add(slot);
            }
        }

        if (lobby.Owner.Id != SteamClient.SteamId)
        {
            return assignedSlots;
        }

        for (int i = 0; i < members.Count; i++)
        {
            SteamId memberId = members[i];
            if (assignedSlots.ContainsKey(memberId))
            {
                continue;
            }

            int slot = GetFirstOpenSlot(usedSlots);
            if (slot < 0)
            {
                continue;
            }

            assignedSlots[memberId] = slot;
            usedSlots.Add(slot);
            lobby.SetData(GetSlotKey(memberId), slot.ToString());
        }

        return assignedSlots;
    }

    private int GetFirstOpenSlot(HashSet<int> usedSlots)
    {
        for (int slot = 1; slot < TargetOnlineLobbySize; slot++)
        {
            if (!usedSlots.Contains(slot))
            {
                return slot;
            }
        }

        return -1;
    }

    private string GetSlotKey(SteamId steamId)
    {
        return $"{LobbySlotKeyPrefix}{steamId.Value}";
    }

    private List<SteamId> GetNewRosterPeers(OnlineMatchRoster roster)
    {
        List<SteamId> newPeers = new List<SteamId>();
        if (roster?.Peers == null)
        {
            return newPeers;
        }

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer != null && peer.SteamId != SteamClient.SteamId && !activeMatchPeerIds.Contains(peer.SteamId))
            {
                newPeers.Add(peer.SteamId);
            }
        }

        return newPeers;
    }

    private void RememberRosterPeers(OnlineMatchRoster roster)
    {
        activeMatchPeerIds.Clear();
        if (roster?.Peers == null)
        {
            return;
        }

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer != null)
            {
                activeMatchPeerIds.Add(peer.SteamId);
            }
        }
    }

    private void HandleLobbyCreated(Result result, Lobby lobby)
    {
        lastLobbyCreateResult = result;
        lastLobbyCreated = lobby;

        if (debugLogs)
        {
            Debug.Log($"[SteamLobbyManager] Lobby created callback. Result={result} LobbyId={lobby.Id.Value}");
        }
    }

    private void OnApplicationQuit()
    {
        Shutdown();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Shutdown();
            Instance = null;
        }
    }
}
