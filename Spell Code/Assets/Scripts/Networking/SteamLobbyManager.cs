using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;
using Steamworks.Data;

public class SteamLobbyManager : MonoBehaviour
{
    private const int TargetOnlineLobbySize = 4;
    private const int MinimumOnlineLobbyStartSize = 2;
    private const string MatchReadyKey = "matchReady";
    private const string MatchStartTokenKey = "matchStartToken";
    private const string LobbySlotKeyPrefix = "slot_";

    // Matchmaking (Quick Match)
    // BUMP NetcodeVersion whenever the wire/serialize/state-hash format changes. Matchmaking only
    // pairs clients whose "ver" matches, so an out-of-date player can never be matched into a
    // byte-incompatible match and desync on start (same reason both PCs must run the same build).
    private const string NetcodeVersion = "scz-5"; // scz-5: Dev-New merge changed sim rules (vibe-coding cast/cooldown) + core sub-hash field sets


    private const string MatchmakingKey = "mm";
    private const string VersionKey = "ver";
    private const string SizeKey = "size";

    public static SteamLobbyManager Instance { get; private set; }

    private Lobby? currentLobby;
    private bool isHostingFlow;
    private bool isMatchmaking;
    private bool isShuttingDown;
    private Result lastLobbyCreateResult = Result.None;
    private Lobby? lastLobbyCreated;
    private uint hostFlowVersion;
    private bool startedCurrentLobbyMatch;
    private string currentMatchStartToken = string.Empty;
    private readonly HashSet<SteamId> activeMatchPeerIds = new HashSet<SteamId>();
    private readonly Dictionary<SteamId, float> pendingLobbySnapshotPeers = new Dictionary<SteamId, float>();
    private const float LobbySnapshotResendSeconds = 1f;

    // A lobby join requested while the player is outside MainMenu is deferred across the clean
    // return-to-lobby teardown (ExecuteOrder66 destroys this manager), so these are static to
    // survive it; the rebuilt SteamLobbyManager consumes them in TryResumePendingOnlineJoin.
    private static SteamId? pendingJoinLobbyId;
    private static SteamId? pendingJoinInviterId;
    private static bool launchConnectChecked;

    // A host+invite requested outside MainMenu (e.g. the solo lobby's online door) is deferred
    // the same way: transition to MainMenu first, then host and open the overlay there, so the
    // friend always connects into the scene the online lobby actually simulates in. Static for
    // the same ExecuteOrder66-survival reason as the pending-join fields above.
    private static bool pendingHostInviteRequested;

    [SerializeField] private bool debugLogs = true;
    [SerializeField] private KeyCode inviteOverlayKey = KeyCode.F6;

    public bool IsInLobby => currentLobby.HasValue;
    public bool IsHostingFlow => isHostingFlow;

    public bool OpenInviteOverlayOrHost()
    {
        if (isShuttingDown || !SteamClient.IsValid)
        {
            Debug.LogError("Steam is not running or is shutting down. Cannot open invite overlay.");
            return false;
        }

        // The online lobby only simulates in MainMenu (the join side enforces the same rule via
        // pendingJoinLobbyId). Hosting from any other scene defers: transition to MainMenu first,
        // then TryResumePendingHostInvite re-runs this once the rebuilt scene and Steam are ready,
        // so the lobby is created and the invite overlay opened where the friend will connect.
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            pendingHostInviteRequested = true;
            Debug.Log($"[SteamLobbyManager] Host+invite requested outside MainMenu (scene='{SceneManager.GetActiveScene().name}'). Returning to the lobby scene first.");
            GameManager.Instance?.ExecuteOrder66("MainMenu");
            return true;
        }

        if (currentLobby.HasValue)
        {
            if (TryOpenInviteOverlay())
            {
                return true;
            }

            if (!SameSteamId(currentLobby.Value.Owner.Id, SteamClient.SteamId))
            {
                return false;
            }
        }

        if (isHostingFlow)
        {
            if (debugLogs)
            {
                Debug.Log("[SteamLobbyManager] Invite request ignored; lobby creation is already in progress.");
            }
            return true;
        }

        HostAndInvite();
        return true;
    }

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

        if (!SameSteamId(currentLobby.Value.Owner.Id, SteamClient.SteamId))
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

        // If Steam launched to accept an invite while the game was closed, it appended
        // "+connect_lobby <id>" to the command line. Seed the deferred join from it now; the
        // existing TryResumePendingOnlineJoin (Update) completes it once we're in MainMenu.
        CheckLaunchConnectLobby();
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
        if (isShuttingDown)
        {
            return;
        }

        TryResumePendingOnlineJoin();
        TryResumePendingHostInvite();

        if (!currentLobby.HasValue)
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
            currentLobby.Value.SetData(GetSlotKey(SteamClient.SteamId), "0");
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

    // Matchmaking (Quick Match)

    // UI entry point. Quick Match into a host-chosen bucket size (2..TargetOnlineLobbySize): finds an
    // open PUBLIC match of that size + this build's NetcodeVersion and joins it, otherwise hosts one
    // and waits. The match then starts through the existing matchReady / TryStartOnlineMatchFromLobby
    // flow (at MinimumOnlineLobbyStartSize, then drop-in fills up to the bucket) -- same as invites.
    public void FindMatch(int desiredSize)
    {
        FindMatchAsync(Mathf.Clamp(desiredSize, MinimumOnlineLobbyStartSize, TargetOnlineLobbySize));
    }

    // Cancel an in-progress search / leave the matchmaking lobby. Wire this to a "Cancel" button.
    public void CancelMatchmaking()
    {
        isMatchmaking = false;
        LeaveLobbyInternal();
    }

    private async void FindMatchAsync(int desiredSize)
    {
        if (isShuttingDown || !SteamClient.IsValid)
        {
            Debug.LogError("Steam is not running or shutting down. Cannot matchmake.");
            return;
        }
        if (isHostingFlow || isMatchmaking)
        {
            return; // already hosting or searching
        }

        isMatchmaking = true;
        try
        {
            // Query for an open public match of the same size + version with a free slot.
            Lobby[] results = await SteamMatchmaking.LobbyList
                .WithKeyValue(MatchmakingKey, "1")
                .WithKeyValue(VersionKey, NetcodeVersion)
                .WithKeyValue(SizeKey, desiredSize.ToString())
                .WithSlotsAvailable(1)
                .RequestAsync();

            if (isShuttingDown || !isMatchmaking)
            {
                return;
            }

            if (results != null)
            {
                foreach (Lobby found in results)
                {
                    if (currentLobby.HasValue && found.Id == currentLobby.Value.Id) continue;
                    if (found.MemberCount <= 0 || found.MemberCount >= found.MaxMembers) continue;

                    if (debugLogs) Debug.Log($"[SteamLobbyManager] Quick Match: joining open lobby {found.Id.Value} (size {desiredSize}, members {found.MemberCount}/{found.MaxMembers}).");
                    JoinRequestedLobbyAsync(found.Id, default);
                    return;
                }
            }

            // Nothing open -> host a public match of this size and wait for an opponent.
            if (debugLogs) Debug.Log($"[SteamLobbyManager] Quick Match: no open size-{desiredSize} match found, hosting one.");
            CreateMatchmakingLobbyAsync(desiredSize);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobbyManager] Matchmaking failed: {e.Message}");
            isMatchmaking = false;
        }
    }

    // Creates a public, tagged, host-sized lobby other matchmakers can find. Mirrors HostAndInvite but
    // SetPublic + matchmaking tags, and no invite overlay (matchmade players find it by query).
    private async void CreateMatchmakingLobbyAsync(int size)
    {
        isHostingFlow = true;
        isShuttingDown = false;
        hostFlowVersion++;
        uint currentHostFlowVersion = hostFlowVersion;
        LeaveLobbyInternal();

        try
        {
            Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(size);
            if (isShuttingDown || currentHostFlowVersion != hostFlowVersion || !SteamClient.IsValid)
            {
                if (lobby.HasValue) lobby.Value.Leave();
                isHostingFlow = false;
                isMatchmaking = false;
                return;
            }
            if (!lobby.HasValue && lastLobbyCreateResult == Result.OK && lastLobbyCreated.HasValue)
            {
                lobby = lastLobbyCreated;
            }
            if (!lobby.HasValue)
            {
                Debug.LogError($"Failed to create matchmaking lobby. Result={lastLobbyCreateResult}");
                isHostingFlow = false;
                isMatchmaking = false;
                return;
            }

            currentLobby = lobby.Value;
            currentLobby.Value.SetPublic();        // searchable by other matchmakers (vs SetFriendsOnly)
            currentLobby.Value.SetJoinable(true);
            currentLobby.Value.SetData(MatchmakingKey, "1");
            currentLobby.Value.SetData(VersionKey, NetcodeVersion);
            currentLobby.Value.SetData(SizeKey, size.ToString());
            currentLobby.Value.SetData("hostId", SteamClient.SteamId.Value.ToString());
            currentLobby.Value.SetData("targetSize", size.ToString());
            currentLobby.Value.SetData(MatchReadyKey, "0");
            currentLobby.Value.SetData(MatchStartTokenKey, string.Empty);
            currentLobby.Value.SetData(GetSlotKey(SteamClient.SteamId), "0");
            startedCurrentLobbyMatch = false;
            currentMatchStartToken = string.Empty;

            if (debugLogs) Debug.Log($"[SteamLobbyManager] Hosting public matchmaking lobby {currentLobby.Value.Id.Value} (size {size}, ver {NetcodeVersion}). Waiting for opponents.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception while creating matchmaking lobby: {e.Message}");
            isHostingFlow = false;
            isMatchmaking = false;
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

    // When a friend clicks "Join Game" / accepts an invite while our game is NOT running, Steam
    // launches the executable with "+connect_lobby <lobbyId>" appended to the command line. We read
    // it once at startup and queue the join through the same deferred path used for in-game invites,
    // so TryResumePendingOnlineJoin finishes it once MainMenu is loaded and Steam is initialized.
    private static void CheckLaunchConnectLobby()
    {
        if (launchConnectChecked)
        {
            return;
        }
        launchConnectChecked = true;

        try
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "+connect_lobby"
                    && ulong.TryParse(args[i + 1], out ulong lobbyRaw)
                    && lobbyRaw != 0)
                {
                    pendingJoinLobbyId = new SteamId { Value = lobbyRaw };
                    pendingJoinInviterId = null;
                    Debug.Log($"[SteamLobbyManager] Launched from a Steam invite (+connect_lobby {lobbyRaw}). Queued join for when MainMenu and Steam are ready.");
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobbyManager] Failed to parse launch command line for +connect_lobby: {e.Message}");
        }
    }

    private void HandleGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        if (isShuttingDown || !SteamClient.IsValid)
        {
            return;
        }

        // The online lobby only simulates in MainMenu. If the invite is accepted from anywhere
        // else (training room, tutorial, a leftover match scene), joining in place fails
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            pendingJoinLobbyId = lobby.Id;
            pendingJoinInviterId = friendId;
            Debug.Log($"[SteamLobbyManager] Invite accepted outside MainMenu (scene='{SceneManager.GetActiveScene().name}'). Returning to the lobby scene before joining lobby {lobby.Id.Value}.");
            GameManager.Instance?.ExecuteOrder66("MainMenu");
            return;
        }

        JoinRequestedLobbyAsync(lobby.Id, friendId);
    }

    // Joins a requested lobby and kicks off the online match handshake. Split out from the invite
    // callback so a join deferred across a MainMenu transition can resume through the same path.
    private async void JoinRequestedLobbyAsync(SteamId lobbyId, SteamId inviterId)
    {
        if (isShuttingDown || !SteamClient.IsValid)
        {
            return;
        }

        // Accepting an invite supersedes any queued host+invite intent; without this, the
        // deferred host flow could fire after the join and fight over the lobby state.
        pendingHostInviteRequested = false;

        try
        {
            if (currentLobby.HasValue && currentLobby.Value.Id != lobbyId)
            {
                hostFlowVersion++;
                LeaveLobbyInternal();
            }

            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Joining requested lobby. LobbyId={lobbyId.Value} Inviter={inviterId.Value}");
            }

            Lobby? joined = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
            if (joined.HasValue)
            {
                currentLobby = joined.Value;
                startedCurrentLobbyMatch = false;
                currentMatchStartToken = string.Empty;

                if (debugLogs)
                {
                    Debug.Log($"[SteamLobbyManager] Joined lobby. LobbyId={joined.Value.Id.Value} Owner={joined.Value.Owner.Id.Value}");
                }

                TryStartOnlineMatchFromLobby(joined.Value);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
        }
    }

    // Resumes a lobby join that was deferred while the player was outside MainMenu. Fires once the
    // SteamLobbyManager rebuilt by the freshly loaded lobby scene is alive and Steam is ready.
    private void TryResumePendingOnlineJoin()
    {
        if (!pendingJoinLobbyId.HasValue || !SteamClient.IsValid)
        {
            return;
        }

        if (GameManager.Instance == null)
        {
            return;
        }

        // A pending join can be seeded outside MainMenu without anyone kicking the transition:
        // +connect_lobby at launch now lands in SoloLobby (the new boot scene) instead of
        // MainMenu. Kick the same clean transition the in-game invite path uses; re-entrancy is
        // naturally guarded because ExecuteOrder66 nulls GameManager.Instance immediately.
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            Debug.Log($"[SteamLobbyManager] Pending lobby join outside MainMenu (scene='{SceneManager.GetActiveScene().name}'). Returning to the lobby scene first.");
            GameManager.Instance.ExecuteOrder66("MainMenu");
            return;
        }

        SteamId lobbyId = pendingJoinLobbyId.Value;
        SteamId inviterId = pendingJoinInviterId ?? default;
        pendingJoinLobbyId = null;
        pendingJoinInviterId = null;

        Debug.Log($"[SteamLobbyManager] Resuming deferred lobby join in MainMenu. LobbyId={lobbyId.Value}.");
        JoinRequestedLobbyAsync(lobbyId, inviterId);
    }

    // Resumes a host+invite that was deferred while the player was outside MainMenu (e.g. the
    // solo lobby's online door). Mirrors TryResumePendingOnlineJoin: fires once the rebuilt
    // scene's managers are alive and Steam is ready.
    private void TryResumePendingHostInvite()
    {
        if (!pendingHostInviteRequested || !SteamClient.IsValid)
        {
            return;
        }

        if (GameManager.Instance == null || SceneManager.GetActiveScene().name != "MainMenu")
        {
            return;
        }

        pendingHostInviteRequested = false;
        Debug.Log("[SteamLobbyManager] Resuming deferred host+invite in MainMenu.");
        OpenInviteOverlayOrHost();
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

        if (!SameSteamId(lobby.Owner.Id, SteamClient.SteamId))
        {
            return;
        }

        if (SameSteamId(friend.Id, SteamClient.SteamId))
        {
            return;
        }

        if (debugLogs)
        {
            Debug.Log($"[SteamLobbyManager] Lobby member joined. Member={friend.Id.Value} LobbyId={lobby.Id.Value}");
        }

        EnsureSlotAssignedForMember(lobby, friend.Id);
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
            if (SameSteamId(lobby.Owner.Id, SteamClient.SteamId) && roster.PlayerCount >= MinimumOnlineLobbyStartSize)
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
                if (SameSteamId(lobby.Owner.Id, SteamClient.SteamId))
                {
                    QueueLobbySnapshotPeers(newPeers);
                    GameManager.Instance.TrySendOnlineLobbyRosterUpdateToExistingPeers(roster, newPeers);
                }
            }
            return;
        }

        bool canStartOrRefresh = GameManager.Instance.CanStartOrRefreshOnlineLobby(roster);
        if (!canStartOrRefresh)
        {
            return;
        }

        if (SameSteamId(lobby.Owner.Id, SteamClient.SteamId) && roster.PlayerCount >= MinimumOnlineLobbyStartSize)
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

        if (!SameSteamId(lobby.Owner.Id, SteamClient.SteamId) && roster.PlayerCount > MinimumOnlineLobbyStartSize)
        {
            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Waiting for host lobby snapshot before joining active roster. Members={roster.PlayerCount}");
            }
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
        if (!SameSteamId(lobby.Owner.Id, SteamClient.SteamId) || GameManager.Instance == null)
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
            if (SameSteamId(member.Id, steamId))
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

    public bool IsLobbySnapshotPendingForPeer(SteamId peerId)
    {
        return peerId.IsValid && pendingLobbySnapshotPeers.ContainsKey(peerId);
    }

    private void QueueLobbySnapshotPeer(SteamId peerId)
    {
        if (!peerId.IsValid || SameSteamId(peerId, SteamClient.SteamId))
        {
            return;
        }

        pendingLobbySnapshotPeers[peerId] = -LobbySnapshotResendSeconds;
        if (debugLogs)
        {
            Debug.Log($"[SteamLobbyManager] Queued lobby snapshot. Peer={peerId.Value}");
        }
    }

    private void QueueLobbySnapshotPeers(OnlineMatchRoster roster)
    {
        if (roster?.Peers == null)
        {
            return;
        }

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer != null)
            {
                QueueLobbySnapshotPeer(peer.SteamId);
            }
        }
    }

    private void QueueLobbySnapshotPeers(List<SteamId> peers)
    {
        if (peers == null)
        {
            return;
        }

        for (int i = 0; i < peers.Count; i++)
        {
            QueueLobbySnapshotPeer(peers[i]);
        }
    }

    private void TrySendPendingLobbySnapshots(Lobby lobby)
    {
        if (pendingLobbySnapshotPeers.Count == 0
            || !SameSteamId(lobby.Owner.Id, SteamClient.SteamId)
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
            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Sending lobby snapshot. Peer={peerId.Value}");
            }
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
        List<SteamId> members = GetLobbyMemberIds(lobby);
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

            if (SameSteamId(memberId, SteamClient.SteamId))
            {
                roster.LocalPlayerSlot = playerSlot;
            }
        }

        return roster;
    }

    private List<SteamId> GetLobbyMemberIds(Lobby lobby)
    {
        List<SteamId> members = new List<SteamId>();
        foreach (Friend member in lobby.Members)
        {
            if (member.Id.IsValid && !ContainsSteamId(members, member.Id))
            {
                members.Add(member.Id);
            }
        }

        return members;
    }

    private void EnsureSlotAssignedForMember(Lobby lobby, SteamId memberId)
    {
        if (!SameSteamId(lobby.Owner.Id, SteamClient.SteamId) || !memberId.IsValid)
        {
            return;
        }

        List<SteamId> members = GetLobbyMemberIds(lobby);
        if (!ContainsSteamId(members, lobby.Owner.Id))
        {
            members.Add(lobby.Owner.Id);
        }

        if (!ContainsSteamId(members, memberId))
        {
            members.Add(memberId);
        }

        Dictionary<SteamId, int> assignedSlots = BuildAssignedSlots(lobby, members);
        if (debugLogs && assignedSlots.TryGetValue(memberId, out int slot))
        {
            Debug.Log($"[SteamLobbyManager] Assigned lobby slot. Member={memberId.Value} Slot={slot}");
        }
    }

    private Dictionary<SteamId, int> BuildAssignedSlots(Lobby lobby, List<SteamId> members)
    {
        Dictionary<SteamId, int> assignedSlots = new Dictionary<SteamId, int>();
        HashSet<int> usedSlots = new HashSet<int>();

        if (lobby.Owner.Id.IsValid && ContainsSteamId(members, lobby.Owner.Id))
        {
            assignedSlots[lobby.Owner.Id] = 0;
            usedSlots.Add(0);
            if (SameSteamId(lobby.Owner.Id, SteamClient.SteamId))
            {
                lobby.SetData(GetSlotKey(lobby.Owner.Id), "0");
            }
        }

        for (int i = 0; i < members.Count; i++)
        {
            SteamId memberId = members[i];
            if (SameSteamId(memberId, lobby.Owner.Id))
            {
                continue;
            }

            bool isOwner = SameSteamId(memberId, lobby.Owner.Id);
            string slotText = lobby.GetData(GetSlotKey(memberId));
            if (int.TryParse(slotText, out int slot)
                && slot >= 0
                && slot < TargetOnlineLobbySize
                && !usedSlots.Contains(slot)
                && (slot > 0 || isOwner))
            {
                assignedSlots[memberId] = slot;
                usedSlots.Add(slot);
            }
        }

        if (!SameSteamId(lobby.Owner.Id, SteamClient.SteamId))
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

    private bool ContainsSteamId(List<SteamId> steamIds, SteamId steamId)
    {
        for (int i = 0; i < steamIds.Count; i++)
        {
            if (SameSteamId(steamIds[i], steamId))
            {
                return true;
            }
        }

        return false;
    }

    private bool SameSteamId(SteamId a, SteamId b)
    {
        return a.IsValid && b.IsValid && a.Value == b.Value;
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
            if (peer != null && !SameSteamId(peer.SteamId, SteamClient.SteamId) && !IsActiveMatchPeer(peer.SteamId))
            {
                newPeers.Add(peer.SteamId);
            }
        }

        return newPeers;
    }

    private bool IsActiveMatchPeer(SteamId steamId)
    {
        foreach (SteamId activePeerId in activeMatchPeerIds)
        {
            if (SameSteamId(activePeerId, steamId))
            {
                return true;
            }
        }

        return false;
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
