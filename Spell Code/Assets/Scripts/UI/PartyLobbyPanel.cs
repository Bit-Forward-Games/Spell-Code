using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "Friends Lobby" screen. Four slots: Host Profile is always the host, the three Friend Profile
/// buttons read "Invite Friend" until somebody takes them and then show that player's Steam name.
/// "Game Modes" opens the mode chooser ("Multiplayer Gamemodes Panel 2") and the pick lands in the
/// "Selected GameMode" label. "Start Match" starts an online match with whoever is standing there --
/// two players is enough, all four is not required.
///
/// Wiring (public, Button.OnClick-ready):
///   Friend Profile buttons  -> OnSlotPressed with 1, 2, 3   (0 is the host's own slot)
///   "Start Match"           -> StartMatch()
///   "Game Modes"            -> OpenGameModeMenu()
///   Back / cancel           -> LeaveLobby()
///   each mode button in Panel 2 -> its own OnlineGameModeOption.Select()
///
/// The panel opens itself the moment a party lobby exists, so it comes up on the host after the
/// deferred MainMenu transition and on a guest as soon as they accept the invite -- no wiring needed
/// for that, and it closes itself when the match starts.
/// </summary>
public class PartyLobbyPanel : OnlineMenuPanel
{
    /// <summary>One lobby slot's widgets. Leave anything you do not use unassigned.</summary>
    [System.Serializable]
    public class SlotWidgets
    {
        [Tooltip("The slot button. Only the host's empty slots are interactable.")]
        public Button button;

        [Tooltip("Shows the occupant's Steam name, or the empty-slot prompt.")]
        public TextMeshProUGUI nameLabel;

        [Tooltip("Optional. Shown only while the slot is empty.")]
        public GameObject emptyState;

        [Tooltip("Optional. Shown only while the slot is occupied.")]
        public GameObject occupiedState;

        [Tooltip("Optional. Shown while a player is connecting and has no confirmed slot yet.")]
        public GameObject connectingState;
    }

    [Header("Slots (element 0 = Host Profile, 1-3 = Friend Profiles)")]
    [SerializeField] private SlotWidgets[] slots = new SlotWidgets[4];

    [Tooltip("Text an empty Friend Profile button shows to the host.")]
    [SerializeField] private string emptySlotText = "Invite Friend";

    [Tooltip("Text an empty slot shows to a guest, who cannot invite.")]
    [SerializeField] private string emptySlotTextForGuests = "Waiting...";

    [Tooltip("Text shown while a player is mid-join.")]
    [SerializeField] private string connectingSlotText = "Connecting...";

    [Header("Start Match")]
    [SerializeField] private Button startMatchButton;
    [SerializeField] private TextMeshProUGUI startMatchLabel;
    [SerializeField] private string startMatchText = "Start Match";
    [SerializeField] private string waitingForPlayersText = "Waiting for players";
    [SerializeField] private string waitingForHostText = "Waiting for host";
    [SerializeField] private string startingText = "Starting...";

    [Header("Game Modes")]
    [Tooltip("The 'Game Modes' button in this lobby.")]
    [SerializeField] private Button gameModeButton;

    [Tooltip("The 'Selected GameMode' label. Shows the host's current pick.")]
    [SerializeField] private TextMeshProUGUI selectedGameModeLabel;

    [Tooltip("'Multiplayer Gamemodes Panel 2' -- the mode chooser this lobby opens.")]
    [SerializeField] private GameObject gameModePanel;

    [Tooltip("Optional. Selectable to focus when the mode chooser opens.")]
    [SerializeField] private GameObject gameModePanelFirstSelected;

    [Header("Status")]
    [Tooltip("Optional. Shows e.g. '2/4 players'.")]
    [SerializeField] private TextMeshProUGUI statusLabel;

    private bool gameModeMenuOpen;

    // Last mode id this panel drew. Picking a mode changes it, which is how the chooser knows to
    // close itself -- the mode buttons talk to SteamLobbyManager, not to this panel.
    private string lastSeenGameModeId;

    protected override void Awake()
    {
        base.Awake();

        if (gameModePanel != null)
        {
            gameModePanel.SetActive(false);
        }
    }

    private void Update()
    {
        SteamLobbyManager lobby = Lobby;

        // MainMenu is the only scene the online lobby simulates in, so it is the only place this
        // panel may raise itself. Without the scene gate a lobby that outlives a match could pop the
        // panel up on the End screen -- and OnlineMenuPanel.Open re-enables inactive ancestors to
        // make itself visible, which would drag the whole HUD back on with it.
        bool inLobbyScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu";
        bool shouldBeOpen = inLobbyScene && lobby != null && lobby.IsInPartyLobby && !IsOnlineMatchLive;

        // Opens on the host once the deferred MainMenu transition has created the lobby, and on a
        // guest the moment their invite join lands. Closes when the match starts (the sim needs the
        // screen and real time back) or when anyone leaves the lobby.
        if (shouldBeOpen != IsOpen)
        {
            SetOpen(shouldBeOpen);
        }

        if (!IsOpen)
        {
            return;
        }

        RefreshSlots();
        RefreshStartButton();
        RefreshGameMode();
        RefreshStatus();

        // After the refreshes, so interactable states are current before focus is chosen.
        MaintainFreeze();
        MaintainFocus();

        LogLobbyDiagnostics();

        PollMenuInput(HandleCancel);
    }

    // Throttled state dump. Three symptoms (character walking, dead navigation, a stuck status
    // label) all come down to values that are invisible from outside, so print them once a second
    // rather than guess which one is causing the others.
    private float nextDiagnosticTime;

    private void LogLobbyDiagnostics()
    {
        if (Time.unscaledTime < nextDiagnosticTime)
        {
            return;
        }
        nextDiagnosticTime = Time.unscaledTime + 1f;

        SteamLobbyManager lobby = Lobby;
        UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;

        int interactableSlots = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].button != null && slots[i].button.interactable)
            {
                interactableSlots++;
            }
        }

        Debug.Log(
            $"[PartyLobbyPanel] open={IsOpen} panels={OpenPanelCount} timeScale={Time.timeScale} " +
            $"freeze={freezeGameWhileOpen} isHost={(lobby != null && lobby.IsPartyHost)} " +
            $"members={(lobby != null ? lobby.PartyMemberCount : 0)} interactableSlots={interactableSlots} " +
            $"selected={(selected != null ? selected.name : "NONE")} " +
            $"joining={(lobby != null && lobby.IsJoiningMatch)} starting={(lobby != null && lobby.IsStartingMatch)}");
    }

    protected override void OnOpened()
    {
        CloseGameModeMenuInternal();
        lastSeenGameModeId = null;

        RefreshSlots();
        RefreshStartButton();
        RefreshGameMode();
        RefreshStatus();
    }

    protected override void OnClosed()
    {
        CloseGameModeMenuInternal();
    }

    // ----------------------------------------------------------------------------------------
    // Button handlers
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Friend Profile button. On an empty slot the host opens the Steam invite overlay. Steam has no
    /// concept of inviting into a particular slot, so the index is presentational -- whoever accepts
    /// takes the first free one.
    /// </summary>
    public void OnSlotPressed(int slotIndex)
    {
        SteamLobbyManager lobby = Lobby;
        if (lobby == null || !lobby.IsPartyHost)
        {
            return;
        }

        if (lobby.TryGetPartySlot(slotIndex, out SteamLobbyManager.PartySlotInfo _))
        {
            // Occupied. Steam lobbies have no clean kick, so this is a deliberate no-op rather than
            // a half-working action.
            return;
        }

        // Pass the slot so the button decides the invited player's number: slot 1 = P2, 2 = P3, 3 = P4.
        lobby.InviteToParty(slotIndex);
    }

    /// <summary>Host-only "Start Match".</summary>
    public void StartMatch()
    {
        SteamLobbyManager lobby = Lobby;
        if (lobby == null || !lobby.CanStartPartyMatch)
        {
            return;
        }

        lobby.StartPartyMatch();
    }

    /// <summary>"Game Modes" button. Host only -- a guest cannot change the rules.</summary>
    public void OpenGameModeMenu()
    {
        SteamLobbyManager lobby = Lobby;
        if (gameModePanel == null || lobby == null || !lobby.IsPartyHost)
        {
            return;
        }

        gameModeMenuOpen = true;
        gameModePanel.SetActive(true);
        FocusSelectable(gameModePanelFirstSelected);
    }

    /// <summary>Closes the mode chooser and returns focus to the Game Modes button.</summary>
    public void CloseGameModeMenu()
    {
        CloseGameModeMenuInternal();

        if (gameModeButton != null)
        {
            FocusSelectable(gameModeButton.gameObject);
        }
    }

    public void ToggleGameModeMenu()
    {
        if (gameModeMenuOpen)
        {
            CloseGameModeMenu();
        }
        else
        {
            OpenGameModeMenu();
        }
    }

    /// <summary>Leaves the party lobby. Update then closes this panel because the lobby is gone.</summary>
    public void LeaveLobby()
    {
        Lobby?.LeaveParty();
    }

    private void HandleCancel()
    {
        if (gameModeMenuOpen)
        {
            CloseGameModeMenu();
            return;
        }

        LeaveLobby();
    }

    private void CloseGameModeMenuInternal()
    {
        gameModeMenuOpen = false;
        if (gameModePanel != null)
        {
            gameModePanel.SetActive(false);
        }
    }

    // ----------------------------------------------------------------------------------------
    // Presentation
    // ----------------------------------------------------------------------------------------

    private void RefreshSlots()
    {
        SteamLobbyManager lobby = Lobby;
        bool isHost = lobby != null && lobby.IsPartyHost;

        for (int i = 0; i < slots.Length; i++)
        {
            SlotWidgets widgets = slots[i];
            if (widgets == null)
            {
                continue;
            }

            SteamLobbyManager.PartySlotInfo slot = default;
            bool occupied = lobby != null && lobby.TryGetPartySlot(i, out slot);

            if (widgets.nameLabel != null)
            {
                if (!occupied)
                {
                    widgets.nameLabel.text = isHost ? emptySlotText : emptySlotTextForGuests;
                }
                else if (slot.IsProvisional)
                {
                    widgets.nameLabel.text = connectingSlotText;
                }
                else
                {
                    widgets.nameLabel.text = slot.DisplayName;
                }
            }

            SetActiveIfPresent(widgets.emptyState, !occupied);
            SetActiveIfPresent(widgets.occupiedState, occupied);
            SetActiveIfPresent(widgets.connectingState, occupied && slot.IsProvisional);

            if (widgets.button != null)
            {
                // Only the host has anything to do with a slot, and only with an empty one.
                widgets.button.interactable = isHost && !occupied;
            }
        }
    }

    private void RefreshStartButton()
    {
        SteamLobbyManager lobby = Lobby;
        bool isHost = lobby != null && lobby.IsPartyHost;
        bool canStart = lobby != null && lobby.CanStartPartyMatch;

        if (startMatchButton != null)
        {
            // A guest sees the button but can never press it; they are waiting on the host.
            startMatchButton.interactable = canStart;
        }

        if (startMatchLabel == null)
        {
            return;
        }

        if (!isHost)
        {
            startMatchLabel.text = waitingForHostText;
        }
        else if (canStart)
        {
            startMatchLabel.text = startMatchText;
        }
        else if (lobby != null && !lobby.IsPartyLobbyWaitingForHostStart)
        {
            startMatchLabel.text = startingText;
        }
        else
        {
            // Host is alone: an online match still needs a second machine in it.
            startMatchLabel.text = waitingForPlayersText;
        }
    }

    private void RefreshGameMode()
    {
        SteamLobbyManager lobby = Lobby;
        if (lobby == null)
        {
            return;
        }

        OnlineGameModeSelection mode = lobby.PartyGameMode;

        if (selectedGameModeLabel != null && selectedGameModeLabel.text != mode.DisplayName)
        {
            selectedGameModeLabel.text = mode.DisplayName;
        }

        // A mode button publishes straight to the lobby, so the pick shows up here as a changed id.
        // That is the cue to dismiss the chooser -- the host picked, the label updated, done.
        if (lastSeenGameModeId != null && lastSeenGameModeId != mode.Id && gameModeMenuOpen)
        {
            CloseGameModeMenu();
        }
        lastSeenGameModeId = mode.Id;

        if (gameModeButton != null)
        {
            // Guests see the host's pick but cannot change it.
            gameModeButton.interactable = lobby.IsPartyHost;
        }

        // Let each authored mode button show whether it is the current pick.
        System.Collections.Generic.IReadOnlyList<OnlineGameModeOption> options = OnlineGameModeRegistry.All;
        for (int i = 0; i < options.Count; i++)
        {
            OnlineGameModeOption option = options[i];
            if (option != null)
            {
                option.SetSelectedVisual(option.ModeId == mode.Id);
            }
        }
    }

    private void RefreshStatus()
    {
        if (statusLabel == null)
        {
            return;
        }

        SteamLobbyManager lobby = Lobby;
        statusLabel.text = lobby == null
            ? string.Empty
            : $"{lobby.PartyMemberCount}/{lobby.PartySlotCount} players";
    }

    private static void SetActiveIfPresent(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
