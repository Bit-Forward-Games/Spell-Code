using System;
using UnityEngine;
using Steamworks;
using Steamworks.Data;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    private Lobby? currentLobby;
    private bool isHostingFlow;

    public bool IsInLobby => currentLobby.HasValue;
    public bool IsHostingFlow => isHostingFlow;

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
        SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
    }

    private void OnDisable()
    {
        SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined -= HandleLobbyMemberJoined;
        SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
    }

    public async void HostAndInvite()
    {
        if (!SteamClient.IsValid)
        {
            Debug.LogError("Steam is not running. Cannot host online match.");
            return;
        }

        if (isHostingFlow)
        {
            return;
        }

        isHostingFlow = true;
        LeaveLobbyInternal();

        try
        {
            Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(2);
            if (!lobby.HasValue)
            {
                Debug.LogError("Failed to create Steam lobby.");
                isHostingFlow = false;
                return;
            }

            currentLobby = lobby.Value;
            currentLobby.Value.SetFriendsOnly();
            currentLobby.Value.SetJoinable(true);
            currentLobby.Value.SetData("hostId", SteamClient.SteamId.Value.ToString());

            SteamFriends.OpenGameInviteOverlay(currentLobby.Value.Id);
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
        if (!SteamClient.IsValid)
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
        currentLobby = lobby;

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager not found; cannot start online match.");
            return;
        }

        if (GameManager.Instance.isOnlineMatchActive)
        {
            return;
        }

        if (lobby.Owner.Id == SteamClient.SteamId)
        {
            return;
        }

        SteamId hostId = lobby.Owner.Id;
        GameManager.Instance.StartOnlineMatch(localIndex: 1, remoteIndex: 0, opponentId: hostId);
    }

    private void HandleLobbyMemberJoined(Lobby lobby, Friend friend)
    {
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

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager not found; cannot start online match.");
            return;
        }

        if (GameManager.Instance.isOnlineMatchActive)
        {
            return;
        }

        GameManager.Instance.StartOnlineMatch(localIndex: 0, remoteIndex: 1, opponentId: friend.Id);
        isHostingFlow = false;
    }
}
