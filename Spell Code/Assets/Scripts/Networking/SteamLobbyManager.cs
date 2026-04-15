using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;

public class SteamLobbyManager : MonoBehaviour
{
    private const int TargetOnlineLobbySize = 4;

    public static SteamLobbyManager Instance { get; private set; }

    private Lobby? currentLobby;
    private bool isHostingFlow;
    private bool isShuttingDown;
    private Result lastLobbyCreateResult = Result.None;
    private Lobby? lastLobbyCreated;
    private uint hostFlowVersion;

    [SerializeField] private bool debugLogs = true;

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

        if (GameManager.Instance.isOnlineMatchActive)
        {
            return;
        }

        OnlineMatchRoster roster = BuildRoster(lobby);
        if (roster == null || roster.PlayerCount < TargetOnlineLobbySize)
        {
            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Waiting for lobby to fill before starting. Members={roster?.PlayerCount ?? 0}/{TargetOnlineLobbySize}");
            }
            return;
        }

        GameManager.Instance.StartOnlineMatch(roster);
        isHostingFlow = false;
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

        members.Sort((a, b) => a.Value.CompareTo(b.Value));
        if (members.Count == 0)
        {
            return null;
        }

        OnlineMatchRoster roster = new OnlineMatchRoster
        {
            HostSteamId = lobby.Owner.Id
        };

        for (int i = 0; i < members.Count; i++)
        {
            SteamId memberId = members[i];
            roster.Peers.Add(new OnlineMatchPeerInfo
            {
                SteamId = memberId,
                PlayerSlot = i
            });

            if (memberId == SteamClient.SteamId)
            {
                roster.LocalPlayerSlot = i;
            }
        }

        return roster;
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
