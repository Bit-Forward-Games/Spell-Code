using System;
using System.Collections.Concurrent;
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
    // Steam lobby data is eventually propagated. In rare cases the host can see its own SetData
    // writes and enter the match before a guest receives the ready/token update. Party starts also
    // travel over lobby chat and are retried until every frozen-roster peer acknowledges starting.
    private const string PartyStartChatPrefix = "SCZ_PARTY_START|";
    private const string PartyStartAckChatPrefix = "SCZ_PARTY_START_ACK|";
    private const string PartyKickChatPrefix = "SCZ_PARTY_KICK|";
    private const float PartyStartResendSeconds = 0.5f;
    private const float PartyStartDeliveryTimeoutSeconds = 30f;
    // Holds the match-start TOKEN of the roster that actually went live, written by the host the
    // moment its match starts and never rewritten afterwards. Empty means no match is running.
    //
    // It has to be the token, not a bare "1". Every member of a party lobby starts from the same
    // press of Start Match, so the host publishes this at the same instant the guests are still
    // deciding whether to start -- a bare flag makes every guest think it arrived late and wait for
    // a snapshot the host has no reason to send. Comparing tokens instead answers the real question:
    // "is the running match the same roster I am in, or one that formed before me?"
    private const string MatchRunningKey = "matchRunning";
    private const string LobbySlotKeyPrefix = "slot_";

    // Matchmaking (Quick Match)
    // BUMP NetcodeVersion whenever the wire/serialize/state-hash format changes. Matchmaking only
    // pairs clients whose "ver" matches, so an out-of-date player can never be matched into a
    // byte-incompatible match and desync on start (same reason both PCs must run the same build).
    private const string NetcodeVersion = "scz-23"; // scz-23: Dev-New merge. CheckAllSpellConditionsOfProcCon now
                                                    // forwards the DEFENDER to SpellData.CheckCondition instead of the
                                                    // acting player, changing what many spells receive on
                                                    // hit/hurt/parry/block/kill; ProcCondition gained OnCrit/OnSweetSpot
                                                    // (appended, so existing values are stable); new Combo Demon + Hot
                                                    // Streak passives and projectiles; StockStability/Bailout/CashOut/
                                                    // CoinToss/GetAJob/LoadedDice/QuarterReport/UseTheCard/LetItRide/
                                                    // LuckyBreak all changed

    private const string MatchmakingKey = "mm";
    private const string VersionKey = "ver";
    private const string SizeKey = "size";

    // Online lobby flavour, published by whoever creates the lobby and read by every member.
    // Absent  -> legacy host+invite lobby: auto-starts as soon as a second member arrives.
    // "party" -> VS Friends: fills up to 4 slots and waits for the host to press Start Match.
    // "quick" -> VS the World: auto-starts, and the accepted-size keys below apply.
    private const string LobbyModeKey = "lobbyMode";
    private const string LobbyModePartyValue = "party";
    private const string LobbyModeQuickMatchValue = "quick";

    // Host's chosen game mode (authored in the scene as OnlineGameModeOption components). Every peer
    // applies the values it reads here, so the choice is identical across the match without adding
    // anything to the wire format. The label rides along so a guest can show the host's mode name
    // even if that mode does not exist in its own build.
    private const string GameModeKey = "gameMode";
    private const string GameModeNameKey = "gameModeName";

    // Quick Match accepted sizes. A searcher can accept 2-player matches, 4-player matches, or both,
    // so a single "size" value cannot express the request: each accepted size gets its own
    // "mmsize_<n>" = "1" lobby key, and a searcher queries one key per size it accepts. Members
    // publish their own accepted set as MEMBER data so the host can narrow the lobby to a size
    // everyone agreed to.
    private const string SizeFlagKeyPrefix = "mmsize_";
    private const string MemberSizePrefsKey = "mmsizes";

    public static SteamLobbyManager Instance { get; private set; }

    private Lobby? currentLobby;
    private bool isHostingFlow;
    private bool isMatchmaking;
    private bool isShuttingDown;
    private Result lastLobbyCreateResult = Result.None;
    private Lobby? lastLobbyCreated;
    private uint hostFlowVersion;
    private SteamId? activeHostedLobbyId;
    // Facepunch exposes the lobby owner through a live native getter, but it has no owner-changed
    // callback. Track it per lobby and compare it in Update so Steam's automatic owner transfer can
    // be adopted on Unity's main thread after the previous host leaves or disconnects.
    private SteamId? observedLobbyOwnerLobbyId;
    private SteamId? observedLobbyOwnerId;
    private bool startingHostedMatch;
    private bool startedCurrentLobbyMatch;
    private string currentMatchStartToken = string.Empty;
    private string loggedDeferredStartToken = string.Empty;
    private readonly HashSet<SteamId> activeMatchPeerIds = new HashSet<SteamId>();
    private readonly Dictionary<SteamId, float> pendingLobbySnapshotPeers = new Dictionary<SteamId, float>();
    private SteamId? partyStartCommitLobbyId;
    private string partyStartCommitToken = string.Empty;
    private bool partyStartBroadcastActive;
    private float nextPartyStartBroadcastTime;
    private float partyStartBroadcastExpiresAt;
    private int partyStartBroadcastAttempt;
    private readonly HashSet<SteamId> partyStartAcknowledgedPeers = new HashSet<SteamId>();
    private readonly ConcurrentQueue<PartyLobbyChatMessage> pendingPartyLobbyChatMessages =
        new ConcurrentQueue<PartyLobbyChatMessage>();
    private SteamId? lastJoinableLobbyId;
    private bool? lastPublishedJoinableState;
    private const float LobbySnapshotResendSeconds = 1f;
    // A fast Steam join can begin and complete between two UI frames. Keep its status presentation
    // alive briefly so the label renders without delaying the actual match start.
    private const float MatchStatusMinimumVisibleSeconds = 0.25f;
    private float startingMatchStatusVisibleUntil;
    private int startingMatchStatusVisibleThroughFrame = -1;
    private bool onlineEntryTransitionInProgress;

    // A lobby join requested while the player is outside MainMenu is deferred across the clean
    // return-to-lobby teardown (ExecuteOrder66 destroys this manager), so these are static to
    // survive it; the rebuilt SteamLobbyManager consumes them in TryResumePendingOnlineJoin.
    private static SteamId? pendingJoinLobbyId;
    private static SteamId? pendingJoinInviterId;
    // True from the moment the player accepts a Steam lobby invite until GameManager has actually
    // started that online match. Static so the status survives a deferred MainMenu rebuild.
    private static bool joiningMatchRequested;
    private static float joiningMatchStatusVisibleUntil;
    private static int joiningMatchStatusVisibleThroughFrame = -1;
    private static bool launchConnectChecked;

    // A host+invite requested outside MainMenu (e.g. the solo lobby's online door) is deferred
    // the same way: transition to MainMenu first, then host and open the overlay there, so the
    // friend always connects into the scene the online lobby actually simulates in. Static for
    // the same ExecuteOrder66-survival reason as the pending-join fields above.
    private static bool pendingHostInviteRequested;

    // Quick Match requested outside MainMenu (the solo lobby's multiplayer door) defers the same way
    // as host/join, transition to MainMenu first, then run the search there so the match starts in
    // the scene the online lobby actually simulates in. Static for the same ExecuteOrder66-survival
    // reason as the pending-join/host fields above.
    private static bool pendingMatchmakingRequested;

    // Size (2-4) of the Quick Match currently being searched for, set the moment Find Match is pressed.
    // Static for the same ExecuteOrder66-survival reason, the deferred MainMenu transition can rebuild
    // the UI, so the "finding match" label must read the size from here rather than from TempUIScript's
    // own matchmakingSize (an instance field that would reset to the 2-player default).
    private static int matchmakingSearchSize;

    // VS the World: which match sizes the local player is willing to be matched into. Both may be
    // selected at once. Static for the same ExecuteOrder66-survival reason as the fields above -- the
    // selection is made in SoloLobby and consumed after the deferred MainMenu transition.
    public static readonly int[] QuickMatchSizes = { 2, 4 };
    private static readonly bool[] quickMatchSizeSelected = { true, false };

    // Sizes actually being searched for by the in-flight Quick Match, in query order.
    private static readonly List<int> pendingMatchmakingSizes = new List<int>();

    // VS Friends (party lobby). A party lobby is created and then HELD: the host invites friends into
    // the 4 slots and the match only becomes ready once they press Start Match. partyStartRequested is
    // the host-side latch for that press; guests never write lobby data and simply wait for matchReady.
    private static bool pendingPartyHostRequested;
    private bool partyStartRequested;

    // Set synchronously by CreatePartyLobbyAsync. The party hold must NOT depend solely on reading
    // "lobbyMode" back out of Steam: that is an async round-trip, and if it ever returns empty for a
    // frame the host publishes matchReady and every peer is yanked into a match the host never
    // started. This local flag makes the host's own hold unconditional -- and the host is the only
    // client that can arm a match, so it is sufficient on its own.
    private bool hostCreatedPartyLobby;

    // Slot the host most recently opened the invite overlay for, claimed by the next member to join.
    // -1 = no reservation, take the first free slot. See InviteToParty(int).
    private int pendingInviteSlot = -1;
    private float pendingInviteSlotExpiresAt;
    private const float PendingInviteSlotLifetimeSeconds = 300f;
    private OnlineGameModeSelection localPartyGameMode = OnlineGameModeSelection.Default;

    // Per-frame cache for the slot readout the party UI polls. Rebuilt at most once a frame because
    // every entry costs a Steam lobby-data lookup and four slot widgets ask for it every Update.
    private readonly PartySlotInfo[] partySlotCache = new PartySlotInfo[TargetOnlineLobbySize];
    private int partySlotCacheFrame = -1;

    // Host-side resolved Quick Match capacity: the largest size every current member said they accept.
    // -1 until it can be resolved. Caps who may still join so a 2-players-only searcher is never
    // dragged into a lobby that keeps growing toward 4.
    private int resolvedQuickMatchBucket = -1;
    private bool quickMatchBucketFullyKnown;
    // Unscaled time the bucket first went unresolved, so an unpublished member cannot wedge the lobby
    // shut forever. 0 == currently resolved.
    private float quickMatchBucketUnknownSince;
    private const float QuickMatchPreferenceGraceSeconds = 3f;

    /// <summary>One party-lobby slot as the VS Friends UI wants to draw it.</summary>
    public struct PartySlotInfo
    {
        public bool IsOccupied;
        public SteamId SteamId;
        public string DisplayName;
        public bool IsHost;
        public bool IsLocalPlayer;
        /// <summary>True while the host has not published this member's slot index yet.</summary>
        public bool IsProvisional;
    }

    // Steam callbacks are normally dispatched on Unity's main thread in this project, but the
    // client is also configured with an async callback loop. Queue control messages so match setup
    // always runs from Update and never from an event-dispatch context.
    private struct PartyLobbyChatMessage
    {
        public SteamId LobbyId;
        public SteamId SenderId;
        public string Message;
    }

    [SerializeField] private bool debugLogs = true;
    [SerializeField] private KeyCode inviteOverlayKey = KeyCode.F6;

    public bool IsInLobby => currentLobby.HasValue;
    public bool IsHostingFlow => isHostingFlow;
    public bool IsJoiningMatch =>
        joiningMatchRequested
        || Time.unscaledTime < joiningMatchStatusVisibleUntil
        || Time.frameCount <= joiningMatchStatusVisibleThroughFrame;
    // Latched by the validated member-joined callback for a lobby this client actually created.
    // The live member-count gate lets Quick Match return to "finding" if that guest leaves pre-start.
    // A party lobby is excluded outright: it sits there filling slots until the host presses Start, so
    // "STARTING MATCH..." would be a lie for the whole time the VS Friends panel is up.
    public bool IsStartingMatch =>
        !isShuttingDown
        && SteamClient.IsValid
        && activeHostedLobbyId.HasValue
        && currentLobby.HasValue
        && currentLobby.Value.Id == activeHostedLobbyId.Value
        && SameSteamId(currentLobby.Value.Owner.Id, SteamClient.SteamId)
        && (!IsPartyLobbyWaitingForHostStart || IsPartyMatchStartRequested)
        && (Time.unscaledTime < startingMatchStatusVisibleUntil
            || Time.frameCount <= startingMatchStatusVisibleThroughFrame
            || (startingHostedMatch
                && currentLobby.Value.MemberCount >= MinimumOnlineLobbyStartSize
                && !(GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive)));

    // True while a Quick Match search is in flight: either DEFERRED (Find Match was pressed outside
    // MainMenu and we're transitioning there) or actively querying / hosting / waiting for opponents.
    // Goes false on CancelMatchmaking and on failure. Also gated on the match not being live, because
    // isMatchmaking is never cleared when the match actually starts -- without that gate a "finding
    // match" label would ride the persistent HUD into Gameplay.
    public bool IsSearchingForMatch =>
        (pendingMatchmakingRequested || isMatchmaking)
        && !IsStartingMatch
        && !(GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive);

    // Size (2-4) of the in-flight Quick Match search. Only meaningful while IsSearchingForMatch.
    public int SearchingMatchSize => matchmakingSearchSize;

    // ------------------------------------------------------------------------------------------
    // VS FRIENDS -- party lobby
    //
    // Flow: HostPartyLobby() (from the solo lobby door, so it defers into MainMenu first) creates a
    // friends-only lobby that does NOT auto-start. The UI draws four slots from TryGetPartySlot();
    // pressing an empty one calls InviteToParty() to open the Steam overlay. SetPartyGameMode()
    // publishes the host's pick. StartPartyMatch() is what finally arms the existing matchReady
    // handshake, so the match begins with exactly the players standing in the lobby at that moment.
    // ------------------------------------------------------------------------------------------

    /// <summary>True while this client is in a lobby created by the VS Friends flow.</summary>
    public bool IsInPartyLobby => currentLobby.HasValue && IsPartyLobby(currentLobby.Value);

    /// <summary>True when this client owns the party lobby (i.e. occupies slot 1 and may press Start).</summary>
    public bool IsPartyHost =>
        IsInPartyLobby
        && SteamClient.IsValid
        && hostCreatedPartyLobby
        && SameSteamId(currentLobby.Value.Owner.Id, SteamClient.SteamId);

    /// <summary>True while a party lobby is still gathering players and no match has been started.</summary>
    public bool IsPartyLobbyWaitingForHostStart =>
        IsInPartyLobby
        && !(GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive)
        && currentLobby.Value.GetData(MatchReadyKey) != "1";

    /// <summary>True on the host immediately after Start Match is pressed, before lobby data propagates.</summary>
    public bool IsPartyMatchStartRequested =>
        IsInPartyLobby
        && partyStartRequested
        && !(GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive);

    /// <summary>
    /// True from the moment VS Friends is chosen until the party match actually starts. Covers the
    /// gap the Friends Lobby panel cannot: the deferred MainMenu transition and the async lobby
    /// creation, during which no panel is open yet and nothing else marks the player as "entering an
    /// online match" -- which is exactly when the code-mode prompt used to pop up over the lobby.
    /// Goes false once the match is live, so the prompt appears normally after Start Match.
    /// </summary>
    public bool IsPartyEntryPending =>
        !(GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive)
        && (pendingPartyHostRequested || IsInPartyLobby);

    /// <summary>Members currently sitting in the party lobby, host included.</summary>
    public int PartyMemberCount => currentLobby.HasValue ? currentLobby.Value.MemberCount : 0;

    /// <summary>Maximum number of party slots (also the number the UI should draw).</summary>
    public int PartySlotCount => TargetOnlineLobbySize;

    /// <summary>
    /// Whether the Start Match button should be interactable. The host may start with any roster they
    /// have gathered, but an "online match" still needs a second machine in it -- StartOnlineMatch
    /// refuses a one-player roster outright, so a solo party lobby has nothing to start.
    /// </summary>
    public bool CanStartPartyMatch =>
        IsPartyHost
        && !partyStartRequested
        && PartyMemberCount >= MinimumOnlineLobbyStartSize
        && !(GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive);

    /// <summary>Currently selected game mode. Guests read the host's pick straight out of lobby data.</summary>
    public OnlineGameModeSelection PartyGameMode
    {
        get
        {
            if (currentLobby.HasValue)
            {
                string publishedId = currentLobby.Value.GetData(GameModeKey);
                if (!string.IsNullOrEmpty(publishedId))
                {
                    return OnlineGameModeSelection.Resolve(
                        publishedId,
                        currentLobby.Value.GetData(GameModeNameKey));
                }
            }

            return localPartyGameMode;
        }
    }

    /// <summary>
    /// VS Friends entry point. Creates the party lobby the host will invite friends into. Safe to call
    /// from any scene: like every other online entry it transitions to MainMenu first, because that is
    /// the only scene the online lobby simulates in.
    /// </summary>
    public bool HostPartyLobby()
    {
        if (isShuttingDown || !SteamClient.IsValid)
        {
            Debug.LogError("[SteamLobbyManager] Steam is not running or is shutting down. Cannot host a party lobby.");
            return false;
        }

        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            pendingPartyHostRequested = true;
            // Accepting one online entry cancels the others; otherwise a stale deferred flow fires
            // after this one lands and fights over currentLobby.
            pendingHostInviteRequested = false;
            pendingMatchmakingRequested = false;

            GameManager.Instance?.tempUI?.CloseGamemodesMenuForOnlineEntry();

            Debug.Log($"[SteamLobbyManager] Party lobby requested outside MainMenu (scene='{SceneManager.GetActiveScene().name}'). Transitioning to the lobby scene first.");
            TransitionToMainMenuForOnlineEntry();
            return true;
        }

        if (IsInPartyLobby)
        {
            return true; // already sitting in one
        }

        if (isHostingFlow)
        {
            if (debugLogs)
            {
                Debug.Log("[SteamLobbyManager] Party host request ignored; lobby creation is already in progress.");
            }
            return true;
        }

        CreatePartyLobbyAsync();
        return true;
    }

    /// <summary>
    /// Empty-slot button handler: opens the Steam invite overlay so the host can pick a friend. With
    /// no requested slot, whoever accepts takes the first free guest slot.
    /// </summary>
    public bool InviteToParty()
    {
        return InviteToParty(-1);
    }

    /// <summary>
    /// Opens the Steam invite overlay and RESERVES the given slot for whoever accepts, so the button
    /// the host pressed decides that player's number (slot 1 = P2, slot 2 = P3, slot 3 = P4).
    ///
    /// Steam itself has no concept of inviting into a slot -- an invite is just an invite -- so the
    /// reservation is held locally by the host and consumed by the next member to arrive. Pressing a
    /// different slot before anyone accepts simply moves the reservation. If someone joins without an
    /// invite (Steam's "Join Game" on a friend), there is no reservation and they take the first free
    /// slot, which is the old behaviour.
    /// </summary>
    public bool InviteToParty(int slotIndex)
    {
        if (!IsPartyHost
            || partyStartRequested
            || (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive))
        {
            return false;
        }

        if (PartyMemberCount >= TargetOnlineLobbySize)
        {
            if (debugLogs)
            {
                Debug.Log("[SteamLobbyManager] Invite ignored; the party lobby is already full.");
            }
            return false;
        }

        // Slot 0 is the host and can never be reserved for a guest.
        pendingInviteSlot = (slotIndex > 0 && slotIndex < TargetOnlineLobbySize) ? slotIndex : -1;
        pendingInviteSlotExpiresAt = pendingInviteSlot >= 0
            ? Time.unscaledTime + PendingInviteSlotLifetimeSeconds
            : 0f;
        if (debugLogs)
        {
            Debug.Log($"[SteamLobbyManager] Opening invite overlay. ReservedSlot={pendingInviteSlot} (P{pendingInviteSlot + 1})");
        }

        bool overlayOpened = TryOpenInviteOverlay();
        if (!overlayOpened)
        {
            pendingInviteSlot = -1;
            pendingInviteSlotExpiresAt = 0f;
        }
        return overlayOpened;
    }

    /// <summary>Host-only. Publishes the chosen game mode so every member starts on the same rules.</summary>
    public bool SetPartyGameMode(string gameModeId, string gameModeDisplayName)
    {
        OnlineGameModeSelection selection = OnlineGameModeSelection.Resolve(gameModeId, gameModeDisplayName);
        localPartyGameMode = selection;

        if (!IsPartyHost
            || partyStartRequested
            || (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive))
        {
            return false;
        }

        currentLobby.Value.SetData(GameModeKey, selection.Id);
        currentLobby.Value.SetData(GameModeNameKey, selection.DisplayName);
        if (debugLogs)
        {
            Debug.Log($"[SteamLobbyManager] Party game mode set to '{selection.Id}' ({selection.DisplayName}).");
        }
        return true;
    }

    /// <summary>Overload for callers that only have the id; the label is looked up or falls back to it.</summary>
    public bool SetPartyGameMode(string gameModeId)
    {
        return SetPartyGameMode(gameModeId, null);
    }

    /// <summary>
    /// Host-only "Start Match" button. Arms the existing matchReady/token handshake for the roster
    /// standing in the lobby right now; TryStartOnlineMatchFromLobby (which runs every Update while a
    /// lobby is held) does the rest on every peer.
    /// </summary>
    public bool StartPartyMatch()
    {
        if (!IsPartyHost)
        {
            return false;
        }

        if (PartyMemberCount < MinimumOnlineLobbyStartSize)
        {
            Debug.LogWarning("[SteamLobbyManager] Start Match ignored; an online match needs at least one other player in the lobby.");
            return false;
        }

        OnlineMatchRoster roster = BuildRoster(currentLobby.Value);
        if (GameManager.Instance == null
            || roster == null
            || !GameManager.Instance.CanStartOrRefreshOnlineLobby(roster))
        {
            Debug.LogWarning("[SteamLobbyManager] Start Match ignored; the party roster is not ready yet.");
            return false;
        }

        partyStartRequested = true;
        // Party membership is mutable only while gathering in the lobby. Freezing it before the
        // ready/token handshake prevents an in-flight invite from changing different peers' rollback
        // slot layouts on different frames.
        currentLobby.Value.SetJoinable(false);
        startingHostedMatch = true;
        startingMatchStatusVisibleUntil = Mathf.Max(
            startingMatchStatusVisibleUntil,
            Time.unscaledTime + MatchStatusMinimumVisibleSeconds);
        startingMatchStatusVisibleThroughFrame = Mathf.Max(
            startingMatchStatusVisibleThroughFrame,
            Time.frameCount + 1);
        Debug.Log($"[SteamLobbyManager] Host started the party match. Members={PartyMemberCount} GameMode='{PartyGameMode.Id}'.");
        TryStartOnlineMatchFromLobby(currentLobby.Value);
        return true;
    }

    /// <summary>
    /// Host-only removal of one joined party member. Steam's lobby API does not expose a kick
    /// operation, so the owner sends an authenticated control message and the selected client leaves
    /// itself after verifying that the sender is still the current lobby owner.
    /// </summary>
    public bool KickPartyMember(SteamId memberId)
    {
        if (!IsPartyHost
            || !memberId.IsValid
            || SameSteamId(memberId, SteamClient.SteamId)
            || !IsCurrentLobbyMember(memberId))
        {
            return false;
        }

        Lobby lobby = currentLobby.Value;
        if (IsPartyRosterFrozen(lobby))
        {
            Debug.LogWarning("[SteamLobbyManager] Kick ignored; the party roster is already starting or live.");
            return false;
        }

        bool sent = lobby.SendChatString(PartyKickChatPrefix + memberId.Value);
        if (sent)
        {
            Debug.Log($"[SteamLobbyManager] Party kick requested. Member={memberId.Value}.");
        }
        else
        {
            Debug.LogWarning($"[SteamLobbyManager] Steam did not queue the party kick for member {memberId.Value}.");
        }

        return sent;
    }

    /// <summary>
    /// Host-only ownership handoff to one confirmed party member. The selected player's former
    /// positive slot is assigned to the old host before Steam moves the selected member to P1.
    /// </summary>
    public bool TransferPartyHost(SteamId memberId)
    {
        if (!IsPartyHost
            || !memberId.IsValid
            || SameSteamId(memberId, SteamClient.SteamId))
        {
            return false;
        }

        Lobby lobby = currentLobby.Value;
        if (IsPartyRosterFrozen(lobby))
        {
            Debug.LogWarning("[SteamLobbyManager] Host transfer ignored; the party roster is already starting or live.");
            return false;
        }

        Friend targetMember = default;
        bool targetFound = false;
        foreach (Friend member in lobby.Members)
        {
            if (SameSteamId(member.Id, memberId))
            {
                targetMember = member;
                targetFound = true;
                break;
            }
        }

        if (!targetFound)
        {
            Debug.LogWarning($"[SteamLobbyManager] Host transfer ignored; member {memberId.Value} is no longer in the lobby.");
            return false;
        }

        string targetSlotText = lobby.GetData(GetSlotKey(memberId));
        if (!int.TryParse(targetSlotText, out int targetSlot)
            || targetSlot <= 0
            || targetSlot >= TargetOnlineLobbySize)
        {
            Debug.LogWarning($"[SteamLobbyManager] Host transfer ignored; member {memberId.Value} has no confirmed party slot.");
            return false;
        }

        // Slot 0 is defined by lobby ownership, not by this metadata. Give the outgoing host the
        // selected member's old slot first so a sparse P3/P4 handoff swaps those two players instead
        // of compacting the old host into an earlier empty slot.
        if (!lobby.SetData(GetSlotKey(SteamClient.SteamId), targetSlot.ToString()))
        {
            Debug.LogWarning("[SteamLobbyManager] Steam did not accept the outgoing host's replacement slot.");
            return false;
        }

        // Facepunch's public setter discards Steam's native boolean result. UpdateLobbyOwnerState
        // verifies the live Owner every frame and completes the existing migration repair once Steam
        // exposes the handoff on each client.
        lobby.Owner = targetMember;
        partySlotCacheFrame = -1;

        // SetLobbyOwner is synchronous in the bundled Facepunch version even though its public
        // property setter hides the native result. Confirm through Steam's live owner getter so a
        // member leaving during this call cannot make the UI report a handoff that was rejected.
        if (!SameSteamId(lobby.Owner.Id, memberId))
        {
            lobby.SetData(GetSlotKey(SteamClient.SteamId), "0");
            Debug.LogWarning(
                $"[SteamLobbyManager] Steam rejected the host transfer to member {memberId.Value}; " +
                "the current host remains owner.");
            return false;
        }

        Debug.Log(
            $"[SteamLobbyManager] Party host transfer requested. " +
            $"PreviousOwner={SteamClient.SteamId.Value} NewOwner={memberId.Value} PreviousSlot={targetSlot}.");
        return true;
    }

    /// <summary>Back/leave button for the VS Friends panel.</summary>
    public void LeaveParty()
    {
        if (!IsInPartyLobby)
        {
            return;
        }

        ClearJoiningMatchStatus();
        LeaveLobbyInternal();
    }

    /// <summary>
    /// Reads one party slot for the UI. Slot 0 is always the host. Returns false (and an empty info)
    /// for a slot nobody occupies, which is what the UI draws as an "invite" button.
    /// </summary>
    public bool TryGetPartySlot(int slotIndex, out PartySlotInfo slot)
    {
        slot = default;
        if (slotIndex < 0 || slotIndex >= partySlotCache.Length)
        {
            return false;
        }

        RefreshPartySlotCache();
        slot = partySlotCache[slotIndex];
        return slot.IsOccupied;
    }

    // ------------------------------------------------------------------------------------------
    // VS THE WORLD -- Quick Match size selection
    //
    // The player toggles the 2-player and/or 4-player buttons and presses Start Matchmaking. Selecting
    // both means "either is fine": the search queries each accepted size, and a hosted lobby advertises
    // all of them so a stricter searcher can still find it.
    // ------------------------------------------------------------------------------------------

    // These four are STATIC on purpose. Which lobby sizes the player is willing to accept is a local
    // preference -- it is pure UI state that happens to be read later when a search starts. Making
    // them instance members meant the Matchmaking panel could not toggle anything unless a
    // SteamLobbyManager existed, which is never true in the Editor (SteamManager disables itself
    // under UNITY_EDITOR and never creates one), so the buttons were dead there for no good reason.
    // The backing array was already static; only the accessors needed fixing.

    /// <summary>True if the local player would accept a match of this size.</summary>
    public static bool IsQuickMatchSizeSelected(int size)
    {
        int index = IndexOfQuickMatchSize(size);
        return index >= 0 && quickMatchSizeSelected[index];
    }

    /// <summary>Sets (or clears) one of the size buttons.</summary>
    public static void SetQuickMatchSizeSelected(int size, bool selected)
    {
        int index = IndexOfQuickMatchSize(size);
        if (index < 0)
        {
            return;
        }

        quickMatchSizeSelected[index] = selected;
    }

    /// <summary>Size-button OnClick. Returns the new state so the UI can restyle itself.</summary>
    public static bool ToggleQuickMatchSize(int size)
    {
        int index = IndexOfQuickMatchSize(size);
        if (index < 0)
        {
            return false;
        }

        quickMatchSizeSelected[index] = !quickMatchSizeSelected[index];
        return quickMatchSizeSelected[index];
    }

    /// <summary>Whether Find Match should be interactable (at least one size chosen).</summary>
    public static bool HasQuickMatchSizeSelection
    {
        get
        {
            for (int i = 0; i < quickMatchSizeSelected.Length; i++)
            {
                if (quickMatchSizeSelected[i])
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// "Start Matchmaking" button. Searches for every selected size and, if nothing is open, hosts a
    /// lobby that accepts all of them. Like the other online entries it transitions to MainMenu first
    /// when pressed from another scene, and the player waits there exactly as they do today.
    /// </summary>
    public bool StartQuickMatch()
    {
        List<int> sizes = GetSelectedQuickMatchSizes();
        if (sizes.Count == 0)
        {
            Debug.LogWarning("[SteamLobbyManager] Start Matchmaking ignored; no match size is selected.");
            return false;
        }

        FindMatch(sizes);
        return true;
    }

    private static int IndexOfQuickMatchSize(int size)
    {
        for (int i = 0; i < QuickMatchSizes.Length; i++)
        {
            if (QuickMatchSizes[i] == size)
            {
                return i;
            }
        }

        return -1;
    }

    private static List<int> GetSelectedQuickMatchSizes()
    {
        List<int> sizes = new List<int>();
        for (int i = 0; i < QuickMatchSizes.Length; i++)
        {
            if (quickMatchSizeSelected[i])
            {
                sizes.Add(QuickMatchSizes[i]);
            }
        }

        return sizes;
    }

    private static void BeginJoiningMatchStatus()
    {
        joiningMatchRequested = true;
        joiningMatchStatusVisibleUntil = Mathf.Max(
            joiningMatchStatusVisibleUntil,
            Time.unscaledTime + MatchStatusMinimumVisibleSeconds);
        joiningMatchStatusVisibleThroughFrame = Mathf.Max(
            joiningMatchStatusVisibleThroughFrame,
            Time.frameCount + 1);
    }

    private static void ClearJoiningMatchStatus()
    {
        joiningMatchRequested = false;
        joiningMatchStatusVisibleUntil = 0f;
        joiningMatchStatusVisibleThroughFrame = -1;
    }

    public bool OpenInviteOverlayOrHost()
    {
        if (isShuttingDown || !SteamClient.IsValid)
        {
            Debug.LogError("Steam is not running or is shutting down. Cannot open invite overlay.");
            return false;
        }

        // The online lobby only simulates in MainMenu (the join side enforces the same rule via
        // pendingJoinLobbyId). Hosting from any other scene defers: transition to MainMenu first,
        // then TryResumePendingHostInvite re-runs this once the scene and Steam are ready, so the
        // lobby is created and the invite overlay opened where the friend will connect.
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            pendingHostInviteRequested = true;

            GameManager manager = GameManager.Instance;
            bool hasLocalPlayer = manager != null
                && manager.players != null
                && manager.players.Length > 0
                && manager.players[0] != null;

            if (hasLocalPlayer)
            {
                // Warm path (solo lobby's online door): the exact transition Local Play uses —
                // persistent managers and the already-spawned player survive, so the host keeps
                // their character and can run around the MainMenu lobby while waiting for the
                // invite to be accepted. A cold ExecuteOrder66 would arrive playerless until the
                // match starts.
                Debug.Log($"[SteamLobbyManager] Host+invite requested outside MainMenu (scene='{SceneManager.GetActiveScene().name}'). Taking the warm Local Play transition to the lobby scene.");
                manager.loadMainMenu();
            }
            else
            {
                // Cold fallback for contexts without a spawned local player.
                Debug.Log($"[SteamLobbyManager] Host+invite requested outside MainMenu (scene='{SceneManager.GetActiveScene().name}'). Returning to the lobby scene first.");
                manager?.ExecuteOrder66("MainMenu");
            }
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
        SteamMatchmaking.OnLobbyMemberLeave += HandleLobbyMemberLeft;
        SteamMatchmaking.OnLobbyMemberDisconnected += HandleLobbyMemberLeft;
        SteamMatchmaking.OnLobbyCreated += HandleLobbyCreated;
        SteamMatchmaking.OnChatMessage += HandleLobbyChatMessage;
        SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
    }

    private void OnDisable()
    {
        SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined -= HandleLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave -= HandleLobbyMemberLeft;
        SteamMatchmaking.OnLobbyMemberDisconnected -= HandleLobbyMemberLeft;
        SteamMatchmaking.OnLobbyCreated -= HandleLobbyCreated;
        SteamMatchmaking.OnChatMessage -= HandleLobbyChatMessage;
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
        TryResumePendingPartyHost();
        TryResumePendingMatchmaking();

        if (!currentLobby.HasValue)
        {
            return;
        }

        UpdateLobbyOwnerState(currentLobby.Value);
        ProcessPendingPartyLobbyChatMessages(currentLobby.Value);
        // A validated kick command makes the targeted client leave from inside the chat drain.
        // Do not dereference the cleared nullable lobby during the rest of this Update.
        if (!currentLobby.HasValue)
        {
            return;
        }
        MaintainPartyStartDelivery(currentLobby.Value);

        if (GameManager.Instance != null)
        {
            if (Input.GetKeyDown(inviteOverlayKey))
            {
                TryOpenInviteOverlay();
            }

            UpdateQuickMatchBucket(currentLobby.Value);
            UpdateLobbyJoinableState(currentLobby.Value);
            TryStartOnlineMatchFromLobby(currentLobby.Value);
            if (currentLobby.HasValue)
            {
                TrySendPendingLobbySnapshots(currentLobby.Value);
            }
        }
    }

    public async void HostAndInvite()
    {
        // This creates a lobby with NO lobbyMode, which means it AUTO-STARTS as soon as a second
        // member joins. That is correct for the legacy host+invite flow but catastrophic if it runs
        // during VS Friends -- the party lobby would be bypassed entirely. Loud on purpose: if this
        // appears in a VS Friends test, something is still routing through the old entry point.
        if (SteamManager.DebugToolsEnabled)
        {
            Debug.LogWarning("[SteamLobbyManager] HostAndInvite() -- creating a LEGACY auto-starting lobby (no lobbyMode). This is NOT the VS Friends party lobby.");
        }

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
            activeHostedLobbyId = currentLobby.Value.Id;
            startingHostedMatch = false;
            startingMatchStatusVisibleUntil = 0f;
            startingMatchStatusVisibleThroughFrame = -1;
            currentLobby.Value.SetFriendsOnly();
            currentLobby.Value.SetJoinable(true);
            currentLobby.Value.SetData("hostId", SteamClient.SteamId.Value.ToString());
            currentLobby.Value.SetData("targetSize", TargetOnlineLobbySize.ToString());
            currentLobby.Value.SetData(MatchReadyKey, "0");
            currentLobby.Value.SetData(MatchStartTokenKey, string.Empty);
            currentLobby.Value.SetData(MatchRunningKey, string.Empty);
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

    // UI entry point. Quick Match into a single bucket size. Kept for the existing "Find Match" button
    // and its 2/3/4 arrow selector; the VS the World panel calls StartQuickMatch() instead.
    public void FindMatch(int desiredSize)
    {
        int clamped = Mathf.Clamp(desiredSize, MinimumOnlineLobbyStartSize, TargetOnlineLobbySize);

        // Keep the new panel's toggles in step when the size is one it can express (2 or 4).
        if (IndexOfQuickMatchSize(clamped) >= 0)
        {
            for (int i = 0; i < QuickMatchSizes.Length; i++)
            {
                quickMatchSizeSelected[i] = QuickMatchSizes[i] == clamped;
            }
        }

        FindMatch(new List<int> { clamped });
    }

    // Quick Match across every size the player accepts: finds an open PUBLIC match of one of those
    // sizes + this build's NetcodeVersion and joins it, otherwise hosts one that advertises ALL of
    // them and waits. The match then starts through the existing matchReady /
    // TryStartOnlineMatchFromLobby flow (at MinimumOnlineLobbyStartSize, then drop-in fills up to the
    // bucket) -- same as invites.
    public void FindMatch(List<int> desiredSizes)
    {
        if (joiningMatchRequested)
        {
            if (debugLogs)
            {
                Debug.Log("[SteamLobbyManager] Quick Match ignored; an invite join is already in progress.");
            }
            return;
        }

        List<int> sizes = NormalizeMatchSizes(desiredSizes);
        if (sizes.Count == 0)
        {
            Debug.LogWarning("[SteamLobbyManager] Quick Match ignored; no valid match size was requested.");
            return;
        }

        // The status label reads the primary (smallest accepted) size; SearchingMatchSizes has the set.
        matchmakingSearchSize = sizes[0];
        pendingMatchmakingSizes.Clear();
        pendingMatchmakingSizes.AddRange(sizes);

        // The online lobby only simulates in MainMenu, so Quick Match — like host/join — defers there
        // first when triggered from another scene (SoloLobby's multiplayer door). Otherwise both
        // players search/host from SoloLobby, where the match can never start, and never converge.
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            pendingMatchmakingRequested = true;
            // One online entry at a time: a queued party host would otherwise fire after this lands.
            pendingPartyHostRequested = false;
            pendingHostInviteRequested = false;

            GameManager manager = GameManager.Instance;
            bool hasLocalPlayer = manager != null
                && manager.players != null
                && manager.players.Length > 0
                && manager.players[0] != null;

            if (hasLocalPlayer)
            {
                // Warm path (solo lobby's online door) same transition Local Play/host+invite use —
                // persistent managers and the spawned player survive into the MainMenu lobby.
                Debug.Log($"[SteamLobbyManager] Quick Match requested outside MainMenu (scene='{SceneManager.GetActiveScene().name}'). Taking the warm transition to the lobby scene.");
                manager.loadMainMenu();
            }
            else
            {
                Debug.Log($"[SteamLobbyManager] Quick Match requested outside MainMenu (scene='{SceneManager.GetActiveScene().name}'). Returning to the lobby scene first.");
                manager?.ExecuteOrder66("MainMenu");
            }
            return;
        }

        FindMatchAsync(sizes);
    }

    /// <summary>All sizes the in-flight Quick Match accepts, smallest first. Only meaningful while searching.</summary>
    public IReadOnlyList<int> SearchingMatchSizes => pendingMatchmakingSizes;

    /// <summary>Ready-made size fragment for the "finding match" status, e.g. "2" or "2 OR 4".</summary>
    public string SearchingMatchSizesLabel
    {
        get
        {
            if (pendingMatchmakingSizes.Count == 0)
            {
                return matchmakingSearchSize.ToString();
            }

            string joined = string.Empty;
            for (int i = 0; i < pendingMatchmakingSizes.Count; i++)
            {
                if (i > 0)
                {
                    joined += i == pendingMatchmakingSizes.Count - 1 ? " OR " : ", ";
                }

                joined += pendingMatchmakingSizes[i].ToString();
            }

            return joined;
        }
    }

    // Clamps, de-duplicates and sorts a requested size set. Smallest first so the search prefers the
    // bucket most likely to already have someone waiting in it.
    private static List<int> NormalizeMatchSizes(List<int> desiredSizes)
    {
        List<int> sizes = new List<int>();
        if (desiredSizes == null)
        {
            return sizes;
        }

        for (int i = 0; i < desiredSizes.Count; i++)
        {
            int clamped = Mathf.Clamp(desiredSizes[i], MinimumOnlineLobbyStartSize, TargetOnlineLobbySize);
            if (!sizes.Contains(clamped))
            {
                sizes.Add(clamped);
            }
        }

        sizes.Sort();
        return sizes;
    }

    // Cancel an in-progress search / leave the matchmaking lobby. Wire this to a "Cancel" button.
    public void CancelMatchmaking()
    {
        isMatchmaking = false;
        pendingMatchmakingRequested = false;
        pendingMatchmakingSizes.Clear();
        LeaveLobbyInternal();
    }

    private async void FindMatchAsync(List<int> desiredSizes)
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
            // One query per accepted size, smallest first. Steam lobby filters are exact key/value
            // matches, so "2 or 4 players" cannot be a single query -- it is one query per size flag.
            for (int i = 0; i < desiredSizes.Count; i++)
            {
                int desiredSize = desiredSizes[i];

                // Query for an open public match that accepts this size, on the same version, with a
                // free slot.
                Lobby[] results = await SteamMatchmaking.LobbyList
                    .WithKeyValue(MatchmakingKey, "1")
                    .WithKeyValue(VersionKey, NetcodeVersion)
                    .WithKeyValue(GetSizeFlagKey(desiredSize), "1")
                    .WithSlotsAvailable(1)
                    .RequestAsync();

                // Re-checked after every await: a Steam invite can land mid-search and cancel us.
                if (isShuttingDown || !isMatchmaking)
                {
                    return;
                }

                if (results == null)
                {
                    continue;
                }

                foreach (Lobby found in results)
                {
                    if (currentLobby.HasValue && found.Id == currentLobby.Value.Id) continue;
                    if (found.MemberCount <= 0 || found.MemberCount >= found.MaxMembers) continue;

                    if (debugLogs) Debug.Log($"[SteamLobbyManager] Quick Match: joining open lobby {found.Id.Value} (accepts size {desiredSize}, members {found.MemberCount}/{found.MaxMembers}).");
                    JoinRequestedLobbyAsync(found.Id, default);
                    return;
                }
            }

            // Nothing open -> host a public match that accepts every selected size and wait.
            if (debugLogs) Debug.Log($"[SteamLobbyManager] Quick Match: no open match found for sizes [{string.Join(",", desiredSizes)}], hosting one.");
            CreateMatchmakingLobbyAsync(desiredSizes);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobbyManager] Matchmaking failed: {e.Message}");
            isMatchmaking = false;
        }
    }

    // Creates a public, tagged lobby other matchmakers can find. Mirrors HostAndInvite but SetPublic +
    // matchmaking tags, and no invite overlay (matchmade players find it by query). The lobby is sized
    // to the LARGEST accepted size and advertises a flag per accepted size, so a stricter searcher
    // (2-players-only) can still find a "2 or 4" host; UpdateQuickMatchBucket then narrows the lobby
    // to a size everybody actually agreed to.
    private async void CreateMatchmakingLobbyAsync(List<int> sizes)
    {
        isHostingFlow = true;
        isShuttingDown = false;
        hostFlowVersion++;
        uint currentHostFlowVersion = hostFlowVersion;
        LeaveLobbyInternal();

        int maxSize = MinimumOnlineLobbyStartSize;
        for (int i = 0; i < sizes.Count; i++)
        {
            maxSize = Mathf.Max(maxSize, sizes[i]);
        }

        try
        {
            Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxSize);
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
            activeHostedLobbyId = currentLobby.Value.Id;
            startingHostedMatch = false;
            startingMatchStatusVisibleUntil = 0f;
            startingMatchStatusVisibleThroughFrame = -1;
            currentLobby.Value.SetPublic();        // searchable by other matchmakers (vs SetFriendsOnly)
            currentLobby.Value.SetJoinable(true);
            currentLobby.Value.SetData(MatchmakingKey, "1");
            currentLobby.Value.SetData(VersionKey, NetcodeVersion);
            currentLobby.Value.SetData(LobbyModeKey, LobbyModeQuickMatchValue);
            currentLobby.Value.SetData(GameModeKey, OnlineGameModeSelection.DefaultId);
            currentLobby.Value.SetData(GameModeNameKey, OnlineGameModeSelection.DefaultDisplayName);
            currentLobby.Value.SetData(SizeKey, maxSize.ToString());
            for (int i = 0; i < sizes.Count; i++)
            {
                currentLobby.Value.SetData(GetSizeFlagKey(sizes[i]), "1");
            }
            currentLobby.Value.SetData("hostId", SteamClient.SteamId.Value.ToString());
            currentLobby.Value.SetData("targetSize", maxSize.ToString());
            currentLobby.Value.SetData(MatchReadyKey, "0");
            currentLobby.Value.SetData(MatchStartTokenKey, string.Empty);
            currentLobby.Value.SetData(MatchRunningKey, string.Empty);
            currentLobby.Value.SetData(GetSlotKey(SteamClient.SteamId), "0");
            startedCurrentLobbyMatch = false;
            currentMatchStartToken = string.Empty;
            resolvedQuickMatchBucket = maxSize;
            quickMatchBucketFullyKnown = false;
            PublishLocalQuickMatchPreferences(currentLobby.Value);

            if (debugLogs) Debug.Log($"[SteamLobbyManager] Hosting public matchmaking lobby {currentLobby.Value.Id.Value} (accepts [{string.Join(",", sizes)}], max {maxSize}, ver {NetcodeVersion}). Waiting for opponents.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception while creating matchmaking lobby: {e.Message}");
            isHostingFlow = false;
            isMatchmaking = false;
        }
    }

    // Creates the VS Friends party lobby: friends-only, four slots, and deliberately NOT auto-starting.
    // No invite overlay here -- the panel's slot buttons open it, so the host can look at the lobby
    // first and pick a game mode before pulling anyone in.
    private async void CreatePartyLobbyAsync()
    {
        isHostingFlow = true;
        isShuttingDown = false;
        hostFlowVersion++;
        uint currentHostFlowVersion = hostFlowVersion;
        LeaveLobbyInternal();

        try
        {
            Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(TargetOnlineLobbySize);
            if (isShuttingDown || currentHostFlowVersion != hostFlowVersion || !SteamClient.IsValid)
            {
                if (lobby.HasValue) lobby.Value.Leave();
                isHostingFlow = false;
                return;
            }
            if (!lobby.HasValue && lastLobbyCreateResult == Result.OK && lastLobbyCreated.HasValue)
            {
                lobby = lastLobbyCreated;
            }
            if (!lobby.HasValue)
            {
                Debug.LogError($"Failed to create party lobby. Result={lastLobbyCreateResult}");
                isHostingFlow = false;
                return;
            }

            currentLobby = lobby.Value;
            activeHostedLobbyId = currentLobby.Value.Id;
            startingHostedMatch = false;
            startingMatchStatusVisibleUntil = 0f;
            startingMatchStatusVisibleThroughFrame = -1;
            partyStartRequested = false;
            hostCreatedPartyLobby = true;

            // The host is not joining anything -- it is hosting. joiningMatchRequested is static and
            // survives scene rebuilds, so any stale "JOINING MATCH..." left over from an earlier
            // invite would otherwise sit on screen for the whole party lobby.
            ClearJoiningMatchStatus();

            // Open on the first mode authored in the chooser panel, so the lobby's "Selected
            // GameMode" label starts on something the menu can actually show. Falls back to the
            // built-in default when no modes have been authored yet.
            localPartyGameMode = OnlineGameModeRegistry.FirstOrDefault();

            currentLobby.Value.SetFriendsOnly();
            currentLobby.Value.SetJoinable(true);
            currentLobby.Value.SetData(LobbyModeKey, LobbyModePartyValue);
            currentLobby.Value.SetData(VersionKey, NetcodeVersion);
            currentLobby.Value.SetData(GameModeKey, localPartyGameMode.Id);
            currentLobby.Value.SetData(GameModeNameKey, localPartyGameMode.DisplayName);
            currentLobby.Value.SetData("hostId", SteamClient.SteamId.Value.ToString());
            currentLobby.Value.SetData("targetSize", TargetOnlineLobbySize.ToString());
            currentLobby.Value.SetData(MatchReadyKey, "0");
            currentLobby.Value.SetData(MatchStartTokenKey, string.Empty);
            currentLobby.Value.SetData(MatchRunningKey, string.Empty);
            currentLobby.Value.SetData(GetSlotKey(SteamClient.SteamId), "0");
            startedCurrentLobbyMatch = false;
            currentMatchStartToken = string.Empty;
            partySlotCacheFrame = -1;

            Debug.Log($"[SteamLobbyManager] Party lobby {currentLobby.Value.Id.Value} created. Waiting for the host to invite friends and press Start.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception while creating party lobby: {e.Message}");
            isHostingFlow = false;
        }
    }

    public void LeaveLobby()
    {
        ClearJoiningMatchStatus();
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
        activeHostedLobbyId = null;
        startingHostedMatch = false;
        startingMatchStatusVisibleUntil = 0f;
        startingMatchStatusVisibleThroughFrame = -1;
        startedCurrentLobbyMatch = false;
        currentMatchStartToken = string.Empty;
        loggedDeferredStartToken = string.Empty;
        activeMatchPeerIds.Clear();
        pendingLobbySnapshotPeers.Clear();
        ResetPartyStartDeliveryState();
        lastJoinableLobbyId = null;
        lastPublishedJoinableState = null;
        observedLobbyOwnerLobbyId = null;
        observedLobbyOwnerId = null;

        // Party/Quick Match state belongs to the lobby that just went away. Leaving partyStartRequested
        // latched would arm the NEXT party lobby's match the instant a second member walked in.
        partyStartRequested = false;
        hostCreatedPartyLobby = false;
        pendingInviteSlot = -1;
        pendingInviteSlotExpiresAt = 0f;
        partySlotCacheFrame = -1;
        resolvedQuickMatchBucket = -1;
        quickMatchBucketFullyKnown = false;
        quickMatchBucketUnknownSince = 0f;
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
                    BeginJoiningMatchStatus();
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

        BeginJoiningMatchStatus();
        // Cancel any lobby creation/query that was already in flight before this invite arrived.
        // Its completion checks hostFlowVersion/isMatchmaking and must not overwrite the joined lobby.
        hostFlowVersion++;
        isHostingFlow = false;
        activeHostedLobbyId = null;
        startingHostedMatch = false;
        startingMatchStatusVisibleUntil = 0f;
        startingMatchStatusVisibleThroughFrame = -1;
        isMatchmaking = false;
        pendingMatchmakingRequested = false;
        pendingHostInviteRequested = false;
        pendingPartyHostRequested = false;

        // The mode selectors freeze the game and scope UI input to the player who opened them.
        // Dismiss either selector synchronously before joining or beginning a deferred scene
        // transition. The helper ignores a normal pause menu, whose existing online-start cleanup
        // remains responsible for resuming it once the match is ready.
        GameManager.Instance?.tempUI?.CloseGamemodesMenuForOnlineEntry();

        // The online lobby only simulates in MainMenu. If the invite is accepted from anywhere
        // else (training room, tutorial, a leftover match scene), joining in place fails
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            pendingJoinLobbyId = lobby.Id;
            pendingJoinInviterId = friendId;
            Debug.Log($"[SteamLobbyManager] Invite accepted outside MainMenu (scene='{SceneManager.GetActiveScene().name}'). Returning to the lobby scene before joining lobby {lobby.Id.Value}.");
            TransitionToMainMenuForOnlineEntry();
            return;
        }

        JoinRequestedLobbyAsync(lobby.Id, friendId, true);
    }

    // Joins a requested lobby and kicks off the online match handshake. Split out from the invite
    // callback so a join deferred across a MainMenu transition can resume through the same path.
    private async void JoinRequestedLobbyAsync(SteamId lobbyId, SteamId inviterId, bool showJoiningStatus = false)
    {
        if (isShuttingDown || !SteamClient.IsValid)
        {
            if (showJoiningStatus)
            {
                ClearJoiningMatchStatus();
            }
            return;
        }

        if (showJoiningStatus)
        {
            BeginJoiningMatchStatus();
            isMatchmaking = false;
        }

        // Accepting an invite supersedes any queued host+invite, party host or matchmaking intent;
        // without this, a deferred flow could fire after the join and fight over the lobby state.
        pendingHostInviteRequested = false;
        pendingMatchmakingRequested = false;
        pendingPartyHostRequested = false;

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
            else if (showJoiningStatus)
            {
                ClearJoiningMatchStatus();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
            if (showJoiningStatus)
            {
                ClearJoiningMatchStatus();
            }
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
            TransitionToMainMenuForOnlineEntry();
            return;
        }

        SteamId lobbyId = pendingJoinLobbyId.Value;
        SteamId inviterId = pendingJoinInviterId ?? default;
        pendingJoinLobbyId = null;
        pendingJoinInviterId = null;
        onlineEntryTransitionInProgress = false;

        Debug.Log($"[SteamLobbyManager] Resuming deferred lobby join in MainMenu. LobbyId={lobbyId.Value}.");
        // This MainMenu visit exists only to connect: skip the title panel (it otherwise shows
        // until players[0] spawns), matching how Local Play arrives without it. Set the panel
        // directly — SetMenuActive would also run the first-launch tutorial check.
        if (GameManager.Instance.MainMenuScreen != null)
        {
            GameManager.Instance.MainMenuScreen.SetActive(false);
        }
        JoinRequestedLobbyAsync(lobbyId, inviterId, true);
    }

    // Called only after GameManager has completed StartOnlineMatch and set isOnlineMatchActive.
    // This also covers late/drop-in joins that start from a lobby snapshot rather than the normal
    // TryStartOnlineMatchFromLobby path.
    public void NotifyOnlineMatchStarted()
    {
        joiningMatchRequested = false;
        startingHostedMatch = false;
    }

    // Transition to MainMenu for a deferred online entry while preserving the live GameManager.
    // MainMenu has no scene-owned GameManager, so ExecuteOrder66 can never be used here: an invite
    // can arrive before SoloLobby has spawned players[0], and destroying the persistent manager in
    // that window strands the joiner behind the screen cover. With no local player, load only the
    // scene/stage; StartOnlineMatch creates the complete online roster after the lobby is joined.
    private void TransitionToMainMenuForOnlineEntry()
    {
        if (onlineEntryTransitionInProgress)
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        if (manager == null || manager.sceneManager == null)
        {
            return;
        }

        bool hasLocalPlayer = manager.players != null
            && manager.players.Length > 0
            && manager.players[0] != null;

        onlineEntryTransitionInProgress = true;

        if (hasLocalPlayer)
        {
            manager.loadMainMenu();
        }
        else
        {
            Debug.Log($"[SteamLobbyManager] Online invite arrived before a local player spawned in '{SceneManager.GetActiveScene().name}'. Taking a manager-preserving transition to MainMenu.");
            manager.sceneManager.LoadScene("MainMenu");
            manager.SetStage(-1);
        }
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
        // Same as the deferred join: this arrival is for hosting, not menu browsing — hide the
        // title panel so it matches the Local Play arrival. Set the panel directly —
        // SetMenuActive would also run the first-launch tutorial check.
        if (GameManager.Instance.MainMenuScreen != null)
        {
            GameManager.Instance.MainMenuScreen.SetActive(false);
        }
        OpenInviteOverlayOrHost();
    }

    // Resumes a VS Friends party lobby requested outside MainMenu (deferred by HostPartyLobby).
    // Mirrors TryResumePendingHostInvite: fires once the rebuilt scene's managers are alive and Steam
    // is ready, so the lobby the friends join is created in the scene the match will simulate in.
    private void TryResumePendingPartyHost()
    {
        if (!pendingPartyHostRequested || !SteamClient.IsValid)
        {
            return;
        }

        if (GameManager.Instance == null || SceneManager.GetActiveScene().name != "MainMenu")
        {
            return;
        }

        pendingPartyHostRequested = false;
        onlineEntryTransitionInProgress = false;
        Debug.Log("[SteamLobbyManager] Resuming deferred party lobby host in MainMenu.");
        // Arrival is for the party lobby, not menu browsing — hide the title panel like the other
        // deferred online entries. Set the panel directly; SetMenuActive would also run the
        // first-launch tutorial check.
        if (GameManager.Instance.MainMenuScreen != null)
        {
            GameManager.Instance.MainMenuScreen.SetActive(false);
        }
        HostPartyLobby();
    }

    // Resumes a Quick Match requested outside MainMenu (deferred by FindMatch)
    // Fires once the rebuilt scene's managers are alive and Steam is ready.
    private void TryResumePendingMatchmaking()
    {
        if (!pendingMatchmakingRequested || !SteamClient.IsValid)
        {
            return;
        }

        if (GameManager.Instance == null || SceneManager.GetActiveScene().name != "MainMenu")
        {
            return;
        }

        pendingMatchmakingRequested = false;
        onlineEntryTransitionInProgress = false;
        Debug.Log($"[SteamLobbyManager] Resuming deferred Quick Match (sizes [{string.Join(",", pendingMatchmakingSizes)}]) in MainMenu.");
        // Arrival is for matchmaking, not menu browsing — hide the title panel like the deferred host/join.
        if (GameManager.Instance.MainMenuScreen != null)
        {
            GameManager.Instance.MainMenuScreen.SetActive(false);
        }
        FindMatchAsync(new List<int>(pendingMatchmakingSizes));
    }

    // ------------------------------------------------------------------------------------------
    // Lobby flavour helpers
    // ------------------------------------------------------------------------------------------

    private static bool IsPartyLobby(Lobby lobby)
    {
        return lobby.GetData(LobbyModeKey) == LobbyModePartyValue;
    }

    private void UpdateLobbyOwnerState(Lobby lobby)
    {
        if (!SteamClient.IsValid)
        {
            return;
        }

        SteamId ownerId = lobby.Owner.Id;
        if (!ownerId.IsValid)
        {
            // Steam may briefly report no owner while processing the departure. Keep the previous
            // observation and retry next frame rather than treating that transient value as a handoff.
            return;
        }

        bool isTrackedLobby = observedLobbyOwnerLobbyId.HasValue
            && observedLobbyOwnerLobbyId.Value == lobby.Id;
        if (!isTrackedLobby)
        {
            observedLobbyOwnerLobbyId = lobby.Id;
            observedLobbyOwnerId = ownerId;

            // Covers the narrow case where this client enters just as the original owner disappears:
            // the first owner we ever observe is already local, so there is no old value to compare.
            if (IsPartyLobby(lobby)
                && SameSteamId(ownerId, SteamClient.SteamId)
                && !hostCreatedPartyLobby)
            {
                RecoverPartyLobbyAfterOwnerChange(lobby, default);
            }
            return;
        }

        if (!observedLobbyOwnerId.HasValue)
        {
            observedLobbyOwnerId = ownerId;
            return;
        }

        SteamId previousOwnerId = observedLobbyOwnerId.Value;
        if (SameSteamId(previousOwnerId, ownerId))
        {
            // Lobby metadata can become visible a frame after entering. If this client was promoted
            // before the first party-flavour read succeeded, adopt the role once that data arrives.
            if (IsPartyLobby(lobby)
                && SameSteamId(ownerId, SteamClient.SteamId)
                && !hostCreatedPartyLobby)
            {
                RecoverPartyLobbyAfterOwnerChange(lobby, default);
            }
            return;
        }

        observedLobbyOwnerId = ownerId;
        partySlotCacheFrame = -1;

        if (!IsPartyLobby(lobby))
        {
            return;
        }

        if (GameManager.Instance != null
            && (GameManager.Instance.isOnlineMatchActive
                || GameManager.Instance.IsOnlineMatchInitializing))
        {
            // Changing rollback authority during a live simulation is a different protocol. Do not
            // rewrite its frozen roster here; this recovery is deliberately for the gathering lobby.
            Debug.LogWarning(
                $"[SteamLobbyManager] Lobby owner changed during an active party match. " +
                $"PreviousOwner={previousOwnerId.Value} NewOwner={ownerId.Value}; " +
                "pre-match host migration was not applied.");
            return;
        }

        RecoverPartyLobbyAfterOwnerChange(lobby, previousOwnerId);
    }

    private void RecoverPartyLobbyAfterOwnerChange(Lobby lobby, SteamId previousOwnerId)
    {
        if (GameManager.Instance != null
            && (GameManager.Instance.isOnlineMatchActive
                || GameManager.Instance.IsOnlineMatchInitializing))
        {
            // This method is also reached by the "entered as owner" and delayed-metadata fallbacks.
            // Keep the guard here so neither path can rewrite an active/bootstrapping match later.
            return;
        }

        bool localIsNewOwner = SameSteamId(lobby.Owner.Id, SteamClient.SteamId);

        // Every survivor can hold an owner-authenticated chat commit locally. Once that owner is gone,
        // the frozen roster names a departed P1 and must be abandoned before any peer evaluates it.
        partyStartRequested = false;
        startingHostedMatch = false;
        startingMatchStatusVisibleUntil = 0f;
        startingMatchStatusVisibleThroughFrame = -1;
        startedCurrentLobbyMatch = false;
        currentMatchStartToken = string.Empty;
        loggedDeferredStartToken = string.Empty;
        activeMatchPeerIds.Clear();
        pendingLobbySnapshotPeers.Clear();
        ResetPartyStartDeliveryState();
        pendingInviteSlot = -1;
        pendingInviteSlotExpiresAt = 0f;
        partySlotCacheFrame = -1;
        lastJoinableLobbyId = null;
        lastPublishedJoinableState = null;
        // The local host latch is raised only after the new owner has successfully repaired every
        // critical lobby field below. Until then the next Update retries this recovery, and host-only
        // UI/actions stay disabled instead of racing partially migrated metadata.
        hostCreatedPartyLobby = false;
        activeHostedLobbyId = localIsNewOwner ? lobby.Id : (SteamId?)null;
        isHostingFlow = localIsNewOwner;
        ClearJoiningMatchStatus();

        // Preserve the previous host's selected mode as the promoted host's local fallback too.
        localPartyGameMode = OnlineGameModeSelection.Resolve(
            lobby.GetData(GameModeKey),
            lobby.GetData(GameModeNameKey));

        if (!localIsNewOwner)
        {
            return;
        }

        List<SteamId> members = GetLobbyMemberIds(lobby);
        if (!ContainsSteamId(members, SteamClient.SteamId))
        {
            members.Add(SteamClient.SteamId);
        }

        // Before replacing the promoted member's metadata with P1, remember the positive slot they
        // occupied as a guest. An explicit Make Host handoff swaps the outgoing host into this exact
        // slot, even if an earlier P2/P3 slot is empty.
        string promotedPlayerSlotText = lobby.GetData(GetSlotKey(SteamClient.SteamId));
        bool hasPromotedPlayerSlot = int.TryParse(promotedPlayerSlotText, out int promotedPlayerSlot)
            && promotedPlayerSlot > 0
            && promotedPlayerSlot < TargetOnlineLobbySize;

        // Only the newly promoted owner may repair shared lobby metadata.
        bool sharedStateRepaired = lobby.SetFriendsOnly();
        sharedStateRepaired &= lobby.SetData("hostId", SteamClient.SteamId.Value.ToString());
        sharedStateRepaired &= lobby.SetData("targetSize", TargetOnlineLobbySize.ToString());
        sharedStateRepaired &= lobby.SetData(MatchReadyKey, "0");
        sharedStateRepaired &= lobby.SetData(MatchStartTokenKey, string.Empty);
        sharedStateRepaired &= lobby.SetData(MatchRunningKey, string.Empty);

        // A disconnected owner is gone, so its old key can be discarded. During an explicit Make
        // Host handoff the previous owner is still a member; explicitly give it the promoted
        // player's former slot so the result does not depend on lobby-data propagation order.
        if (previousOwnerId.IsValid
            && !SameSteamId(previousOwnerId, SteamClient.SteamId))
        {
            if (ContainsSteamId(members, previousOwnerId) && hasPromotedPlayerSlot)
            {
                sharedStateRepaired &=
                    lobby.SetData(GetSlotKey(previousOwnerId), promotedPlayerSlot.ToString());
            }
            else if (!ContainsSteamId(members, previousOwnerId))
            {
                sharedStateRepaired &= lobby.SetData(GetSlotKey(previousOwnerId), string.Empty);
            }
        }
        sharedStateRepaired &= lobby.SetData(GetSlotKey(SteamClient.SteamId), "0");

        if (!sharedStateRepaired)
        {
            Debug.LogWarning(
                $"[SteamLobbyManager] Steam only partially accepted party-host migration for lobby " +
                $"{lobby.Id.Value}; retrying on the next frame.");
            return;
        }

        hostCreatedPartyLobby = true;

        // The promoted player moves to P1. Everyone else retains a valid positive slot; any member
        // caught in the join/departure race is assigned the first genuinely open slot.
        BuildAssignedSlots(lobby, members);
        partySlotCacheFrame = -1;

        bool shouldBeJoinable = members.Count < TargetOnlineLobbySize;
        if (lobby.SetJoinable(shouldBeJoinable))
        {
            lastJoinableLobbyId = lobby.Id;
            lastPublishedJoinableState = shouldBeJoinable;
        }

        Debug.Log(
            $"[SteamLobbyManager] Party host migrated. PreviousOwner={previousOwnerId.Value} " +
            $"NewOwner={SteamClient.SteamId.Value} Members={members.Count} Joinable={shouldBeJoinable}. " +
            "The new host can invite friends, choose a mode, and start the match.");
    }

    private bool IsPartyRosterFrozen(Lobby lobby)
    {
        bool hasDirectCommit = TryGetPartyStartCommit(lobby, out string _);
        string publishedStartToken = lobby.GetData(MatchStartTokenKey);
        bool hasPublishedCommit = lobby.GetData(MatchReadyKey) == "1"
            && MatchStartTokenNamesLobbyOwner(lobby, publishedStartToken);
        return (IsPartyLobby(lobby) || hostCreatedPartyLobby || hasDirectCommit)
            && (partyStartRequested
                || hasPublishedCommit
                || hasDirectCommit
                || startedCurrentLobbyMatch
                || (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive));
    }

    private static bool IsQuickMatchLobby(Lobby lobby)
    {
        return lobby.GetData(LobbyModeKey) == LobbyModeQuickMatchValue
            || lobby.GetData(MatchmakingKey) == "1";
    }

    private static string GetSizeFlagKey(int size)
    {
        return $"{SizeFlagKeyPrefix}{size}";
    }

    // Publishes the local player's accepted Quick Match sizes as MEMBER data (lobby data is
    // owner-only, so this is the only channel a guest has). The host reads it back in
    // UpdateQuickMatchBucket to narrow the lobby to a size everybody agreed to.
    private void PublishLocalQuickMatchPreferences(Lobby lobby)
    {
        List<int> sizes = pendingMatchmakingSizes.Count > 0
            ? new List<int>(pendingMatchmakingSizes)
            : GetSelectedQuickMatchSizes();

        if (sizes.Count == 0)
        {
            // Nothing selected locally (e.g. joined straight from an invite): accept anything the
            // lobby can hold rather than publishing an empty set the host would have to ignore.
            for (int i = 0; i < QuickMatchSizes.Length; i++)
            {
                sizes.Add(QuickMatchSizes[i]);
            }
        }

        lobby.SetMemberData(MemberSizePrefsKey, string.Join(",", sizes));
    }

    private static List<int> ParseSizePreferences(string raw)
    {
        List<int> sizes = new List<int>();
        if (string.IsNullOrEmpty(raw))
        {
            return sizes;
        }

        string[] parts = raw.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out int size)
                && size >= MinimumOnlineLobbyStartSize
                && size <= TargetOnlineLobbySize
                && !sizes.Contains(size))
            {
                sizes.Add(size);
            }
        }

        return sizes;
    }

    // Host-side narrowing for a Quick Match lobby that advertised more than one size. The bucket is
    // the LARGEST size every current member said they accept: everyone in the lobby explicitly opted
    // into it, and it leaves the most room for the drop-in fill the match start already supports.
    // While any member's preferences are still unknown the lobby stays closed (see
    // UpdateLobbyJoinableState) so a 2-players-only searcher can't be dragged toward a 4-player match
    // by someone who joined in the same instant.
    private void UpdateQuickMatchBucket(Lobby lobby)
    {
        if (!IsQuickMatchLobby(lobby)
            || !SameSteamId(lobby.Owner.Id, SteamClient.SteamId)
            || (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive))
        {
            return;
        }

        List<int> accepted = null;
        bool allKnown = true;

        foreach (Friend member in lobby.Members)
        {
            List<int> memberSizes = ParseSizePreferences(lobby.GetMemberData(member, MemberSizePrefsKey));
            if (memberSizes.Count == 0)
            {
                allKnown = false;
                continue;
            }

            if (accepted == null)
            {
                accepted = memberSizes;
                continue;
            }

            for (int i = accepted.Count - 1; i >= 0; i--)
            {
                if (!memberSizes.Contains(accepted[i]))
                {
                    accepted.RemoveAt(i);
                }
            }
        }

        quickMatchBucketFullyKnown = allKnown;
        if (allKnown)
        {
            quickMatchBucketUnknownSince = 0f;
        }
        else if (quickMatchBucketUnknownSince <= 0f)
        {
            quickMatchBucketUnknownSince = Time.unscaledTime;
        }

        if (accepted == null || accepted.Count == 0)
        {
            // No usable intersection (or nobody has published yet): leave the bucket where lobby
            // creation put it rather than inventing a size nobody asked for.
            return;
        }

        int bucket = MinimumOnlineLobbyStartSize;
        for (int i = 0; i < accepted.Count; i++)
        {
            bucket = Mathf.Max(bucket, accepted[i]);
        }

        if (bucket == resolvedQuickMatchBucket)
        {
            return;
        }

        resolvedQuickMatchBucket = bucket;
        lobby.SetData(SizeKey, bucket.ToString());
        lobby.SetData("targetSize", bucket.ToString());
        if (debugLogs)
        {
            Debug.Log($"[SteamLobbyManager] Quick Match lobby narrowed to {bucket} players (every member accepts it).");
        }
    }

    // ------------------------------------------------------------------------------------------
    // Party slot readout for the VS Friends panel
    // ------------------------------------------------------------------------------------------

    // Rebuilt at most once per frame: four slot widgets poll this every Update and each entry costs
    // Steam lobby-data lookups.
    private void RefreshPartySlotCache()
    {
        if (partySlotCacheFrame == Time.frameCount)
        {
            return;
        }

        partySlotCacheFrame = Time.frameCount;
        Array.Clear(partySlotCache, 0, partySlotCache.Length);

        if (!currentLobby.HasValue || !SteamClient.IsValid)
        {
            return;
        }

        Lobby lobby = currentLobby.Value;
        SteamId ownerId = lobby.Owner.Id;
        List<Friend> unplaced = new List<Friend>();

        foreach (Friend member in lobby.Members)
        {
            if (!member.Id.IsValid)
            {
                continue;
            }

            // Slot 0 is the owner by definition (BuildAssignedSlots agrees), so it never needs to
            // wait on metadata. Everyone else reads the index the host published for them.
            int slot = SameSteamId(member.Id, ownerId) ? 0 : -1;
            if (slot < 0)
            {
                string slotText = lobby.GetData(GetSlotKey(member.Id));
                if (!int.TryParse(slotText, out slot) || slot <= 0 || slot >= partySlotCache.Length)
                {
                    slot = -1;
                }
            }

            if (slot < 0 || partySlotCache[slot].IsOccupied)
            {
                unplaced.Add(member);
                continue;
            }

            partySlotCache[slot] = BuildPartySlotInfo(member, ownerId, false);
        }

        // A member who has just arrived is in the lobby before the host has published their slot.
        // Show them in the first free slot, flagged provisional, so the panel does not flicker an
        // empty "Invite" button at someone who is visibly connecting.
        for (int i = 0; i < unplaced.Count; i++)
        {
            for (int slot = 0; slot < partySlotCache.Length; slot++)
            {
                if (!partySlotCache[slot].IsOccupied)
                {
                    partySlotCache[slot] = BuildPartySlotInfo(unplaced[i], ownerId, true);
                    break;
                }
            }
        }
    }

    private PartySlotInfo BuildPartySlotInfo(Friend member, SteamId ownerId, bool provisional)
    {
        bool isLocal = SteamClient.IsValid && SameSteamId(member.Id, SteamClient.SteamId);
        string name = isLocal ? SteamClient.Name : member.Name;
        if (string.IsNullOrEmpty(name))
        {
            name = member.Id.Value.ToString();
        }

        return new PartySlotInfo
        {
            IsOccupied = true,
            SteamId = member.Id,
            DisplayName = name,
            IsHost = SameSteamId(member.Id, ownerId),
            IsLocalPlayer = isLocal,
            IsProvisional = provisional
        };
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
        partySlotCacheFrame = -1;

        // Publish what sizes we accept the moment we are actually in the lobby (member data needs
        // membership). Fires for the creator as well as a joiner, so the host's own entry is covered.
        if (IsQuickMatchLobby(lobby))
        {
            PublishLocalQuickMatchPreferences(lobby);
        }

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

        partySlotCacheFrame = -1;

        if (IsPartyRosterFrozen(lobby))
        {
            Debug.LogWarning($"[SteamLobbyManager] Ignoring member {friend.Id.Value}; the party roster was frozen when Start Match was pressed.");
            return;
        }

        // A party lobby is not "starting" when someone joins -- it is filling. Latching the status
        // here would paint "STARTING MATCH..." over the VS Friends panel until the host pressed Start.
        if (activeHostedLobbyId.HasValue
            && lobby.Id == activeHostedLobbyId.Value
            && !IsPartyLobby(lobby)
            && !(GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive))
        {
            startingHostedMatch = true;
            startingMatchStatusVisibleUntil = Mathf.Max(
                startingMatchStatusVisibleUntil,
                Time.unscaledTime + MatchStatusMinimumVisibleSeconds);
            startingMatchStatusVisibleThroughFrame = Mathf.Max(
                startingMatchStatusVisibleThroughFrame,
                Time.frameCount + 1);
        }

        EnsureSlotAssignedForMember(lobby, friend.Id);
        TryStartOnlineMatchFromLobby(lobby);
    }

    private void HandleLobbyMemberLeft(Lobby lobby, Friend friend)
    {
        if (isShuttingDown
            || !currentLobby.HasValue
            || lobby.Id != currentLobby.Value.Id)
        {
            return;
        }

        partySlotCacheFrame = -1;

        // Slot metadata is keyed by Steam id. Remove it when a member leaves so accepting a later
        // invite from a different profile button cannot resurrect that member's old P-number.
        if (SameSteamId(lobby.Owner.Id, SteamClient.SteamId) && friend.Id.IsValid)
        {
            lobby.SetData(GetSlotKey(friend.Id), string.Empty);
        }
    }

    private void HandleLobbyChatMessage(Lobby lobby, Friend sender, string message)
    {
        if (string.IsNullOrEmpty(message)
            || (!message.StartsWith(PartyStartChatPrefix, StringComparison.Ordinal)
                && !message.StartsWith(PartyStartAckChatPrefix, StringComparison.Ordinal)
                && !message.StartsWith(PartyKickChatPrefix, StringComparison.Ordinal)))
        {
            return;
        }

        pendingPartyLobbyChatMessages.Enqueue(new PartyLobbyChatMessage
        {
            LobbyId = lobby.Id,
            SenderId = sender.Id,
            Message = message
        });
    }

    private void ProcessPendingPartyLobbyChatMessages(Lobby lobby)
    {
        // A bounded drain prevents a malicious member from monopolizing a frame with control-looking
        // chat. Normal party traffic is one start plus at most three acknowledgements per retry.
        int processed = 0;
        while (processed < 64
            && pendingPartyLobbyChatMessages.TryDequeue(out PartyLobbyChatMessage pending))
        {
            processed++;
            if (pending.LobbyId != lobby.Id)
            {
                continue;
            }

            if (pending.Message.StartsWith(PartyStartChatPrefix, StringComparison.Ordinal))
            {
                ProcessPartyStartChatMessage(lobby, pending.SenderId, pending.Message);
            }
            else if (pending.Message.StartsWith(PartyStartAckChatPrefix, StringComparison.Ordinal))
            {
                ProcessPartyStartAckChatMessage(lobby, pending.SenderId, pending.Message);
            }
            else if (pending.Message.StartsWith(PartyKickChatPrefix, StringComparison.Ordinal))
            {
                ProcessPartyKickChatMessage(lobby, pending.SenderId, pending.Message);
            }
        }
    }

    private void ProcessPartyKickChatMessage(Lobby lobby, SteamId senderId, string message)
    {
        // Lobby chat is writable by every member. Only the live owner may remove someone, and only
        // the Steam id named in the message is allowed to act on it.
        if (!IsPartyLobby(lobby) || !SameSteamId(senderId, lobby.Owner.Id))
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[SteamLobbyManager] Ignored party-kick control message from non-owner {senderId.Value}.");
            }
            return;
        }

        string targetText = message.Substring(PartyKickChatPrefix.Length);
        if (!ulong.TryParse(targetText, out ulong targetValue) || targetValue == 0)
        {
            Debug.LogWarning("[SteamLobbyManager] Ignored a malformed party-kick control message.");
            return;
        }

        SteamId targetId = new SteamId { Value = targetValue };
        if (!SameSteamId(targetId, SteamClient.SteamId)
            || SameSteamId(senderId, SteamClient.SteamId))
        {
            return;
        }

        Debug.Log($"[SteamLobbyManager] Removed from party lobby by host {senderId.Value}.");
        LeaveParty();
    }

    private void ProcessPartyStartChatMessage(Lobby lobby, SteamId senderId, string message)
    {
        // Only the current lobby owner is allowed to freeze/start a party roster. Lobby chat itself
        // is writable by every member, so this check is part of the protocol rather than optional.
        if (!SameSteamId(senderId, lobby.Owner.Id))
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[SteamLobbyManager] Ignored party-start control message from non-owner {senderId.Value}.");
            }
            return;
        }

        // Facepunch delivers lobby chat back to the sender too. The host already installed this
        // commit before broadcasting it and must not re-enter its own start flow through the event.
        if (SameSteamId(senderId, SteamClient.SteamId))
        {
            return;
        }

        string token = message.Substring(PartyStartChatPrefix.Length);
        OnlineMatchRoster committedRoster = BuildRosterFromMatchStartToken(lobby, token);
        if (committedRoster == null
            || !SameSteamId(committedRoster.HostSteamId, lobby.Owner.Id))
        {
            Debug.LogWarning("[SteamLobbyManager] Ignored an invalid party-start roster from the lobby owner.");
            return;
        }

        bool isNewCommit = !TryGetPartyStartCommit(lobby, out string existingToken)
            || existingToken != token;
        partyStartCommitLobbyId = lobby.Id;
        partyStartCommitToken = token;

        if (isNewCommit && debugLogs)
        {
            Debug.Log($"[SteamLobbyManager] Received party-start commit from host. Members={committedRoster.PlayerCount} LocalSlot={committedRoster.LocalPlayerSlot}.");
        }

        bool wasAlreadyStarted = GameManager.Instance != null
            && GameManager.Instance.isOnlineMatchActive
            && startedCurrentLobbyMatch
            && currentMatchStartToken == token;
        TryStartOnlineMatchFromLobby(lobby);
        if (wasAlreadyStarted
            && GameManager.Instance != null
            && GameManager.Instance.isOnlineMatchActive
            && startedCurrentLobbyMatch
            && currentMatchStartToken == token)
        {
            SendPartyStartAcknowledgement(lobby, token);
        }
    }

    private void ProcessPartyStartAckChatMessage(Lobby lobby, SteamId senderId, string message)
    {
        if (!SameSteamId(lobby.Owner.Id, SteamClient.SteamId)
            || SameSteamId(senderId, SteamClient.SteamId)
            || !TryGetPartyStartCommit(lobby, out string committedToken))
        {
            return;
        }

        string acknowledgedToken = message.Substring(PartyStartAckChatPrefix.Length);
        if (acknowledgedToken != committedToken
            || !MatchStartTokenContainsSteamId(committedToken, senderId))
        {
            return;
        }

        if (partyStartAcknowledgedPeers.Add(senderId) && debugLogs)
        {
            Debug.Log($"[SteamLobbyManager] Party-start acknowledged. Peer={senderId.Value}.");
        }
    }

    private bool TryGetPartyStartCommit(Lobby lobby, out string token)
    {
        token = string.Empty;
        if (!partyStartCommitLobbyId.HasValue
            || partyStartCommitLobbyId.Value != lobby.Id
            || string.IsNullOrEmpty(partyStartCommitToken)
            || !MatchStartTokenNamesLobbyOwner(lobby, partyStartCommitToken))
        {
            return false;
        }

        token = partyStartCommitToken;
        return true;
    }

    private void BeginPartyStartDelivery(Lobby lobby, string token)
    {
        if (TryGetPartyStartCommit(lobby, out string existingToken) && existingToken == token)
        {
            return;
        }

        partyStartCommitLobbyId = lobby.Id;
        partyStartCommitToken = token;
        partyStartAcknowledgedPeers.Clear();
        partyStartAcknowledgedPeers.Add(SteamClient.SteamId);
        partyStartBroadcastActive = true;
        partyStartBroadcastExpiresAt = Time.unscaledTime + PartyStartDeliveryTimeoutSeconds;
        nextPartyStartBroadcastTime = Time.unscaledTime;
        partyStartBroadcastAttempt = 0;
        BroadcastPartyStartCommit(lobby);
    }

    private void MaintainPartyStartDelivery(Lobby lobby)
    {
        if (!partyStartBroadcastActive)
        {
            return;
        }

        if (!TryGetPartyStartCommit(lobby, out string token)
            || !SameSteamId(lobby.Owner.Id, SteamClient.SteamId))
        {
            partyStartBroadcastActive = false;
            return;
        }

        if (AreAllPartyStartPeersAcknowledged(lobby, token))
        {
            partyStartBroadcastActive = false;
            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Party-start delivered to every guest after {partyStartBroadcastAttempt} broadcast(s).");
            }
            return;
        }

        float now = Time.unscaledTime;
        if (now >= partyStartBroadcastExpiresAt)
        {
            partyStartBroadcastActive = false;
            Debug.LogWarning($"[SteamLobbyManager] Party-start delivery timed out. Missing acknowledgements from [{GetMissingPartyStartAcknowledgements(lobby, token)}].");
            return;
        }

        if (now >= nextPartyStartBroadcastTime)
        {
            BroadcastPartyStartCommit(lobby);
        }
    }

    private void BroadcastPartyStartCommit(Lobby lobby)
    {
        if (!TryGetPartyStartCommit(lobby, out string token)
            || !SameSteamId(lobby.Owner.Id, SteamClient.SteamId))
        {
            partyStartBroadcastActive = false;
            return;
        }

        partyStartBroadcastAttempt++;
        nextPartyStartBroadcastTime = Time.unscaledTime + PartyStartResendSeconds;

        // Re-publish both metadata fields even when the host's local cache already contains them.
        // The original intermittent failure was precisely the host seeing those values while a guest
        // never observed that update. Ready remains the last metadata write/commit marker.
        bool tokenPublished = lobby.SetData(MatchStartTokenKey, token);
        bool readyPublished = lobby.SetData(MatchReadyKey, "1");
        bool chatPublished = lobby.SendChatString(PartyStartChatPrefix + token);

        if (partyStartBroadcastAttempt == 1 && debugLogs)
        {
            Debug.Log($"[SteamLobbyManager] Broadcasting party-start commit. Members={lobby.MemberCount} LobbyData={tokenPublished && readyPublished} Chat={chatPublished}.");
        }
        else if ((!tokenPublished || !readyPublished || !chatPublished)
            && (partyStartBroadcastAttempt <= 3 || partyStartBroadcastAttempt % 10 == 0))
        {
            Debug.LogWarning($"[SteamLobbyManager] Party-start retry {partyStartBroadcastAttempt} was only partially queued. Token={tokenPublished} Ready={readyPublished} Chat={chatPublished}.");
        }
    }

    private bool AreAllPartyStartPeersAcknowledged(Lobby lobby, string token)
    {
        OnlineMatchRoster roster = BuildRosterFromMatchStartToken(lobby, token);
        if (roster?.Peers == null)
        {
            return false;
        }

        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer != null
                && !SameSteamId(peer.SteamId, SteamClient.SteamId)
                && !partyStartAcknowledgedPeers.Contains(peer.SteamId))
            {
                return false;
            }
        }

        return true;
    }

    private string GetMissingPartyStartAcknowledgements(Lobby lobby, string token)
    {
        OnlineMatchRoster roster = BuildRosterFromMatchStartToken(lobby, token);
        if (roster?.Peers == null)
        {
            return "invalid roster";
        }

        List<string> missing = new List<string>();
        for (int i = 0; i < roster.Peers.Count; i++)
        {
            OnlineMatchPeerInfo peer = roster.Peers[i];
            if (peer != null
                && !SameSteamId(peer.SteamId, SteamClient.SteamId)
                && !partyStartAcknowledgedPeers.Contains(peer.SteamId))
            {
                missing.Add(peer.SteamId.Value.ToString());
            }
        }

        return string.Join(",", missing);
    }

    private void SendPartyStartAcknowledgement(Lobby lobby, string token)
    {
        if (SameSteamId(lobby.Owner.Id, SteamClient.SteamId)
            || string.IsNullOrEmpty(token))
        {
            return;
        }

        if (!lobby.SendChatString(PartyStartAckChatPrefix + token) && debugLogs)
        {
            Debug.LogWarning("[SteamLobbyManager] Could not queue the party-start acknowledgement; the next host retry will try again.");
        }
    }

    private void ResetPartyStartDeliveryState()
    {
        partyStartCommitLobbyId = null;
        partyStartCommitToken = string.Empty;
        partyStartBroadcastActive = false;
        nextPartyStartBroadcastTime = 0f;
        partyStartBroadcastExpiresAt = 0f;
        partyStartBroadcastAttempt = 0;
        partyStartAcknowledgedPeers.Clear();
        while (pendingPartyLobbyChatMessages.TryDequeue(out PartyLobbyChatMessage _))
        {
        }
    }

    private void TryStartOnlineMatchFromLobby(Lobby lobby)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager not found; cannot start online match.");
            return;
        }

        bool hasDirectPartyCommit = TryGetPartyStartCommit(lobby, out string directPartyStartToken);
        bool isPartyLobby = IsPartyLobby(lobby) || hostCreatedPartyLobby || hasDirectPartyCommit;

        // VS Friends has no drop-in phase: once Start is pressed, every peer keeps the exact sparse
        // P1-P4 roster that was armed. Rebuilding players during live rollback would attach the new
        // slot at different simulation frames on different machines.
        if (GameManager.Instance.isOnlineMatchActive && IsPartyRosterFrozen(lobby))
        {
            return;
        }

        string publishedReady = lobby.GetData(MatchReadyKey);
        string publishedStartToken = lobby.GetData(MatchStartTokenKey);
        bool hasPublishedPartyCommit = publishedReady == "1"
            && MatchStartTokenNamesLobbyOwner(lobby, publishedStartToken);
        string frozenPartyStartToken = hasDirectPartyCommit
            ? directPartyStartToken
            : publishedStartToken;
        bool useFrozenPartyRoster = isPartyLobby
            && (hasDirectPartyCommit || hasPublishedPartyCommit);

        // Once the host commits a party start, derive the roster from that immutable token instead
        // of live lobby membership. This closes the small Steam propagation race where an already
        // accepted invite appears after SetJoinable(false) and would otherwise wedge existing guests
        // on a different roster.
        OnlineMatchRoster roster = useFrozenPartyRoster
            ? BuildRosterFromMatchStartToken(lobby, frozenPartyStartToken)
            : BuildRoster(lobby);
        if (roster == null)
        {
            if (useFrozenPartyRoster
                && !MatchStartTokenContainsSteamId(frozenPartyStartToken, SteamClient.SteamId))
            {
                Debug.LogWarning("[SteamLobbyManager] This party match already started with a frozen roster; leaving the closed lobby.");
                ClearJoiningMatchStatus();
                LeaveLobbyInternal();
            }
            return;
        }

        // VS Friends: the lobby is deliberately held open. The host gathers up to four players and
        // decides when to go, so nothing may publish matchReady until StartPartyMatch sets this latch.
        // Guests need no equivalent check: they only ever read matchReady, they never write it.
        bool holdForPartyStart = isPartyLobby && !partyStartRequested;

        string expectedMatchStartToken = useFrozenPartyRoster
            ? frozenPartyStartToken
            : BuildMatchStartToken(lobby, roster);
        if (GameManager.Instance.isOnlineMatchActive)
        {
            // NOTE: matchRunning is deliberately NOT refreshed here. It must keep naming the roster
            // that went live, so a drop-in joiner (for whom the host is about to publish a NEW
            // matchReady token) can tell its token apart from the running one and wait for a
            // snapshot instead of cold-starting into a simulation already in progress.

            if (!holdForPartyStart && SameSteamId(lobby.Owner.Id, SteamClient.SteamId) && roster.PlayerCount >= MinimumOnlineLobbyStartSize)
            {
                string currentReady = lobby.GetData(MatchReadyKey);
                string currentToken = lobby.GetData(MatchStartTokenKey);
                if (currentReady != "1" || currentToken != expectedMatchStartToken)
                {
                    lobby.SetData(MatchStartTokenKey, expectedMatchStartToken);
                    lobby.SetData(MatchReadyKey, "1");
                }
            }

            List<SteamId> newPeers = GetNewRosterPeers(roster);
            if (newPeers.Count == 0 || !GameManager.Instance.CanStartOrRefreshOnlineLobby(roster))
            {
                return;
            }

            if (GameManager.Instance.TryRefreshOnlineLobbyRoster(roster))
            {
                // Drop-in joiner: make sure they land on the same rules as everyone already playing.
                GameManager.Instance.ApplyOnlineGameMode(lobby.GetData(GameModeKey), lobby.GetData(GameModeNameKey));
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

        if (!holdForPartyStart && SameSteamId(lobby.Owner.Id, SteamClient.SteamId) && roster.PlayerCount >= MinimumOnlineLobbyStartSize)
        {
            if (isPartyLobby)
            {
                // The tripwire for the party hold: if a party lobby ever prints this WITHOUT the host
                // pressing Start, the hold is broken and the fields below name which check failed.
                // Debug branches only, which is where this would be caught anyway.
                if (SteamManager.DebugToolsEnabled)
                {
                    Debug.Log($"[SteamLobbyManager] Arming match. Members={roster.PlayerCount} lobbyMode='{lobby.GetData(LobbyModeKey)}' hostCreatedParty={hostCreatedPartyLobby} partyStartRequested={partyStartRequested}");
                }
                lobby.SetJoinable(false);
                BeginPartyStartDelivery(lobby, expectedMatchStartToken);
            }
            else
            {
                string currentReady = lobby.GetData(MatchReadyKey);
                string currentToken = lobby.GetData(MatchStartTokenKey);
                if (currentReady != "1" || currentToken != expectedMatchStartToken)
                {
                    lobby.SetData(MatchStartTokenKey, expectedMatchStartToken);
                    // Ready is the commit marker. Publish it last so guests never observe "ready"
                    // with a stale/empty token and construct different initial rosters.
                    lobby.SetData(MatchReadyKey, "1");
                }
            }
        }

        string matchReady = lobby.GetData(MatchReadyKey);
        string matchStartToken = lobby.GetData(MatchStartTokenKey);
        if (isPartyLobby && TryGetPartyStartCommit(lobby, out string directCommitToken))
        {
            // The owner-authenticated lobby-chat commit is the redundant equivalent of ready=1 +
            // matchStartToken. Prefer it when Steam's eventually-consistent lobby data is stale.
            matchReady = "1";
            matchStartToken = directCommitToken;
        }

        if (roster.PlayerCount < MinimumOnlineLobbyStartSize || matchReady != "1" || matchStartToken != expectedMatchStartToken)
        {
            // A party guest is settled, not mid-handshake: they sit in the lobby panel watching slots
            // fill until the host presses Start. Drop the "JOINING MATCH..." status or it would pulse
            // over the panel for the entire wait.
            if (IsPartyLobby(lobby))
            {
                ClearJoiningMatchStatus();
            }
            else if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Waiting for at least one guest before starting. Members={roster?.PlayerCount ?? 0}/{MinimumOnlineLobbyStartSize}");
            }
            return;
        }

        if (startedCurrentLobbyMatch && currentMatchStartToken == matchStartToken)
        {
            return;
        }

        // A guest must not cold-start into a match that is ALREADY running -- it would begin from a
        // different initial state and desync instantly; the host's lobby snapshot is what brings a
        // late arrival in.
        //
        // Execution only reaches here once matchStartToken == expectedMatchStartToken, i.e. the host
        // armed THIS exact roster, this guest included. So the only question left is whether the
        // running match is that same roster or an earlier, smaller one:
        //   * empty                      -> nothing running yet. Cold-start (normal first start).
        //   * equal to our token         -> the running match IS this roster; we were part of the
        //                                   press of Start Match. Cold-start alongside everyone else.
        //   * different from our token   -> a match formed before us is live and the host has since
        //                                   re-armed to include us. Wait for the snapshot.
        string runningMatchToken = lobby.GetData(MatchRunningKey);
        bool joiningAlreadyRunningMatch = !string.IsNullOrEmpty(runningMatchToken)
            && runningMatchToken != matchStartToken;

        if (!SameSteamId(lobby.Owner.Id, SteamClient.SteamId) && joiningAlreadyRunningMatch)
        {
            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Waiting for host lobby snapshot before joining active roster. Members={roster.PlayerCount} running='{runningMatchToken}' mine='{matchStartToken}'");
            }
            return;
        }

        // The mode id is part of the immutable start token. Use that committed value rather than a
        // separately propagated lobby field so the fallback path cannot start on stale rules.
        string committedGameModeId = GetGameModeIdFromMatchStartToken(lobby, matchStartToken);
        string committedGameModeName = lobby.GetData(GameModeKey) == committedGameModeId
            ? lobby.GetData(GameModeNameKey)
            : null;
        GameManager.Instance.ApplyOnlineGameMode(committedGameModeId, committedGameModeName);
        GameManager.Instance.StartOnlineMatch(roster);

        // StartOnlineMatch can legitimately defer when scene-owned networking objects (notably the
        // RollbackManager) have not finished rebuilding yet. Never latch success before it actually
        // activates, or this client will ignore the commit forever and remain in the party panel.
        if (!GameManager.Instance.isOnlineMatchActive)
        {
            if (debugLogs && loggedDeferredStartToken != matchStartToken)
            {
                Debug.Log("[SteamLobbyManager] Party/lobby start prerequisites are not ready yet; retrying on a later frame.");
                loggedDeferredStartToken = matchStartToken;
            }
            return;
        }

        startedCurrentLobbyMatch = true;
        currentMatchStartToken = matchStartToken;
        loggedDeferredStartToken = string.Empty;
        RememberRosterPeers(roster);
        isHostingFlow = false;
        if (isPartyLobby)
        {
            SendPartyStartAcknowledgement(lobby, matchStartToken);
        }

        // Record WHICH roster went live, as early as possible, so anyone who walks in from here on
        // sees a token that differs from their own and waits for a snapshot instead of cold-starting
        // into a running simulation. Peers that started from this same token are unaffected.
        if (SameSteamId(lobby.Owner.Id, SteamClient.SteamId))
        {
            lobby.SetData(MatchRunningKey, matchStartToken);
        }
    }

    private void UpdateLobbyJoinableState(Lobby lobby)
    {
        if (!SameSteamId(lobby.Owner.Id, SteamClient.SteamId) || GameManager.Instance == null)
        {
            return;
        }

        bool joinable = !IsPartyRosterFrozen(lobby)
            && GameManager.Instance.IsOnlineLobbyAcceptingAdditionalPlayers();

        // Quick Match lobbies that advertised several sizes must stop taking joins once the agreed
        // bucket is full, and must stay shut while any member's accepted sizes are still unknown --
        // otherwise a player who only wanted 2-player matches can be filled past that in the window
        // before their preferences arrive. Steam drops non-joinable lobbies from search results too,
        // so this doubles as removing a full lobby from matchmaking.
        if (joinable
            && IsQuickMatchLobby(lobby)
            && !(GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive))
        {
            // Grace period so a member whose preferences never arrive degrades to "let people in"
            // instead of wedging the lobby shut and stranding the search forever.
            bool preferencesSettled = quickMatchBucketFullyKnown
                || (quickMatchBucketUnknownSince > 0f
                    && Time.unscaledTime - quickMatchBucketUnknownSince > QuickMatchPreferenceGraceSeconds);

            joinable = preferencesSettled
                && resolvedQuickMatchBucket > 0
                && lobby.MemberCount < resolvedQuickMatchBucket;
        }

        if (lastJoinableLobbyId.HasValue
            && lastJoinableLobbyId.Value == lobby.Id
            && lastPublishedJoinableState.HasValue
            && lastPublishedJoinableState.Value == joinable)
        {
            return;
        }

        if (lobby.SetJoinable(joinable))
        {
            lastJoinableLobbyId = lobby.Id;
            lastPublishedJoinableState = joinable;
        }
        else if (debugLogs)
        {
            Debug.LogWarning($"[SteamLobbyManager] Steam did not accept joinable={joinable} for lobby {lobby.Id.Value}; retrying.");
        }
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
        // The game mode is part of the token so a host who changes modes after arming the lobby
        // invalidates the old ready state instead of leaving a guest to start on the previous rules.
        string token = $"{lobby.Id.Value}|{OnlineGameModeSelection.Resolve(lobby.GetData(GameModeKey), null).Id}|r";
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

    private string GetGameModeIdFromMatchStartToken(Lobby lobby, string token)
    {
        string expectedPrefix = $"{lobby.Id.Value}|";
        int rosterMarker = string.IsNullOrEmpty(token)
            ? -1
            : token.LastIndexOf("|r:", StringComparison.Ordinal);
        if (token != null
            && token.StartsWith(expectedPrefix, StringComparison.Ordinal)
            && rosterMarker > expectedPrefix.Length)
        {
            string committedModeId = token.Substring(
                expectedPrefix.Length,
                rosterMarker - expectedPrefix.Length);
            if (!string.IsNullOrEmpty(committedModeId))
            {
                return committedModeId;
            }
        }

        return OnlineGameModeSelection.Resolve(lobby.GetData(GameModeKey), null).Id;
    }

    private bool MatchStartTokenNamesLobbyOwner(Lobby lobby, string token)
    {
        string expectedPrefix = $"{lobby.Id.Value}|";
        if (string.IsNullOrEmpty(token)
            || !token.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        int rosterMarker = token.LastIndexOf("|r:", StringComparison.Ordinal);
        if (rosterMarker < expectedPrefix.Length || rosterMarker >= token.Length - 3)
        {
            return false;
        }

        string[] encodedPeers = token.Substring(rosterMarker + 3)
            .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < encodedPeers.Length; i++)
        {
            int separator = encodedPeers[i].IndexOf('-');
            if (separator <= 0
                || separator >= encodedPeers[i].Length - 1
                || !int.TryParse(encodedPeers[i].Substring(0, separator), out int playerSlot)
                || playerSlot != 0
                || !ulong.TryParse(encodedPeers[i].Substring(separator + 1), out ulong hostSteamIdValue))
            {
                continue;
            }

            SteamId encodedHostId = hostSteamIdValue;
            return SameSteamId(encodedHostId, lobby.Owner.Id);
        }

        return false;
    }

    private OnlineMatchRoster BuildRosterFromMatchStartToken(Lobby lobby, string token)
    {
        string expectedPrefix = $"{lobby.Id.Value}|";
        if (string.IsNullOrEmpty(token)
            || !token.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        int rosterMarker = token.LastIndexOf("|r:", StringComparison.Ordinal);
        if (rosterMarker < expectedPrefix.Length || rosterMarker >= token.Length - 3)
        {
            return null;
        }

        string[] encodedPeers = token.Substring(rosterMarker + 3)
            .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
        OnlineMatchRoster roster = new OnlineMatchRoster
        {
            LocalPlayerSlot = -1
        };
        HashSet<ulong> usedSteamIds = new HashSet<ulong>();
        HashSet<int> usedSlots = new HashSet<int>();

        for (int i = 0; i < encodedPeers.Length; i++)
        {
            int separator = encodedPeers[i].IndexOf('-');
            if (separator <= 0
                || separator >= encodedPeers[i].Length - 1
                || !int.TryParse(encodedPeers[i].Substring(0, separator), out int playerSlot)
                || !ulong.TryParse(encodedPeers[i].Substring(separator + 1), out ulong steamIdValue)
                || playerSlot < 0
                || playerSlot >= TargetOnlineLobbySize
                || steamIdValue == 0UL
                || !usedSlots.Add(playerSlot)
                || !usedSteamIds.Add(steamIdValue))
            {
                Debug.LogWarning($"[SteamLobbyManager] Ignoring malformed frozen party roster token '{token}'.");
                return null;
            }

            SteamId steamId = steamIdValue;
            roster.Peers.Add(new OnlineMatchPeerInfo
            {
                SteamId = steamId,
                PlayerSlot = playerSlot
            });

            if (playerSlot == 0)
            {
                roster.HostSteamId = steamId;
            }

            if (SameSteamId(steamId, SteamClient.SteamId))
            {
                roster.LocalPlayerSlot = playerSlot;
            }
        }

        roster.Peers.Sort((a, b) => a.PlayerSlot.CompareTo(b.PlayerSlot));
        if (!roster.HostSteamId.IsValid
            || roster.LocalPlayerSlot < 0
            || roster.Peers.Count < MinimumOnlineLobbyStartSize
            || roster.Peers.Count > TargetOnlineLobbySize)
        {
            // A late joiner is intentionally absent from the committed token and therefore cannot
            // enter this match. Existing members still decode and start from the same frozen roster.
            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Local player is not part of frozen party roster '{token}'.");
            }
            return null;
        }

        return roster;
    }

    private bool MatchStartTokenContainsSteamId(string token, SteamId steamId)
    {
        if (!steamId.IsValid || string.IsNullOrEmpty(token))
        {
            return false;
        }

        int rosterMarker = token.LastIndexOf("|r:", StringComparison.Ordinal);
        if (rosterMarker < 0 || rosterMarker >= token.Length - 3)
        {
            return false;
        }

        string[] encodedPeers = token.Substring(rosterMarker + 3)
            .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < encodedPeers.Length; i++)
        {
            int separator = encodedPeers[i].IndexOf('-');
            if (separator > 0
                && separator < encodedPeers[i].Length - 1
                && ulong.TryParse(encodedPeers[i].Substring(separator + 1), out ulong encodedSteamId)
                && encodedSteamId == steamId.Value)
            {
                return true;
            }
        }

        return false;
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
                string ownerSlotKey = GetSlotKey(lobby.Owner.Id);
                if (lobby.GetData(ownerSlotKey) != "0")
                {
                    lobby.SetData(ownerSlotKey, "0");
                }
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

            int slot = TakeSlotForNewMember(usedSlots);
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

    // Honours the slot the host invited from, if it is still free, then falls back to the first open
    // one. Only ever reached on the host (BuildAssignedSlots returns early for guests), so the
    // reservation cannot be consumed by the wrong machine.
    private int TakeSlotForNewMember(HashSet<int> usedSlots)
    {
        // A reservation belongs to exactly one arriving member. Consume it even if the slot became
        // unavailable while the overlay was open, otherwise it could incorrectly capture a later
        // join and move that player away from the button the host used for them.
        int reserved = Time.unscaledTime <= pendingInviteSlotExpiresAt
            ? pendingInviteSlot
            : -1;
        pendingInviteSlot = -1;
        pendingInviteSlotExpiresAt = 0f;

        if (reserved > 0
            && reserved < TargetOnlineLobbySize
            && !usedSlots.Contains(reserved))
        {
            if (debugLogs)
            {
                Debug.Log($"[SteamLobbyManager] Placing new member in reserved slot {reserved} (P{reserved + 1}).");
            }
            return reserved;
        }

        return GetFirstOpenSlot(usedSlots);
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
        // Static join presentation survives scene rebuilds, but must not leak into the next Editor
        // play session when domain reload is disabled.
        ClearJoiningMatchStatus();
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
