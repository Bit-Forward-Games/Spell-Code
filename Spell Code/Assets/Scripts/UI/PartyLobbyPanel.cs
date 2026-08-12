using System;
using System.Collections.Generic;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The "Friends Lobby" screen. Four slots: Host Profile is always the host, the three Friend Profile
/// buttons read "Invite Friend" until somebody takes them and then show that player's Steam name.
/// Confirming a joined friend's slot as host opens Player Options to kick them or transfer ownership.
/// "Game Modes" opens the mode chooser ("Multiplayer Gamemodes Panel 2") and the pick lands in the
/// "Selected GameMode" label. "Start Match" starts an online match with whoever is standing there --
/// two players is enough, all four is not required.
///
/// Wiring (public, Button.OnClick-ready):
///   Friend Profile buttons  -> OnSlotPressed with 1, 2, 3   (empty invites; occupied opens options)
///   "Start Match"           -> StartMatch()
///   "Game Modes"            -> OpenGameModeMenu()
///   Back / cancel           -> LeaveLobby()
///   mode buttons in Panel 2 are borrowed from the offline flow at runtime; their offline callbacks
///   are restored as soon as the party chooser closes
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
        [Tooltip("The slot button. Every profile remains selectable; only a host confirming an empty friend slot performs an invite.")]
        public Button button;

        [Tooltip("Shows the occupant's Steam name, or the empty-slot prompt.")]
        public TextMeshProUGUI nameLabel;

        [Tooltip("Optional. Shown only while the slot is empty.")]
        public GameObject emptyState;

        [Tooltip("Optional. Shown only while the slot is occupied.")]
        public GameObject occupiedState;

        [Tooltip("Optional. Character portrait shown once this exact slot assignment is confirmed. The matching 'P# Character Art' child is found automatically when unassigned.")]
        public GameObject characterArt;

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
    [Tooltip("The 'Gamemodes' button in this lobby.")]
    [SerializeField] private Button gameModeButton;

    [Tooltip("The text inside the Gamemodes button. Auto-resolved from the button when unassigned.")]
    [SerializeField] private TextMeshProUGUI gameModeButtonLabel;

    [Tooltip("The 'Selected GameMode' label. Shows the host's current pick.")]
    [SerializeField] private TextMeshProUGUI selectedGameModeLabel;

    [Tooltip("'Multiplayer Gamemodes Panel 2' -- the mode chooser this lobby opens.")]
    [SerializeField] private GameObject gameModePanel;

    [Tooltip("Optional. Selectable to focus when the mode chooser opens.")]
    [SerializeField] private GameObject gameModePanelFirstSelected;

    [Header("Player Options")]
    [Tooltip("The modal shown when the host confirms an occupied friend slot. Auto-resolved by name when unassigned.")]
    [SerializeField] private GameObject playerOptionsPanel;

    [Tooltip("Removes the selected member from the party. Auto-resolved by name when unassigned.")]
    [SerializeField] private Button kickPlayerButton;

    [Tooltip("Transfers lobby ownership to the selected member. Auto-resolved by name when unassigned.")]
    [SerializeField] private Button makeHostButton;

    [Header("Status")]
    [Tooltip("Optional. Shows e.g. '2/4 players'.")]
    [SerializeField] private TextMeshProUGUI statusLabel;

    private bool gameModeMenuOpen;
    private bool partyPanelHiddenForGameMode;
    private bool playerOptionsOpen;
    private SteamId selectedPlayerId;
    private int selectedPlayerSlotIndex = -1;
    private const string GameModeButtonText = "Gamemodes";

    private sealed class GameModeButtonBinding
    {
        public Button Button;
        public Button.ButtonClickedEvent OriginalOnClick;
    }

    private readonly List<GameModeButtonBinding> gameModeButtonBindings =
        new List<GameModeButtonBinding>();

    // Panel 2 is shared with Local Play, so its serialized buttons deliberately keep their offline
    // callbacks. These stable ids are used only while the same visuals are borrowed by a party.
    private static readonly Dictionary<string, OnlineGameModeSelection> PartyModesByOptionRoot =
        new Dictionary<string, OnlineGameModeSelection>(StringComparer.OrdinalIgnoreCase)
        {
            { "Normal Mode Option", new OnlineGameModeSelection("normal", "Normal mode") },
            { "Elimination Mode Option", new OnlineGameModeSelection("elimination", "Elimination") },
            { "Fighting Game Mode Option", new OnlineGameModeSelection("fighting-game", "Fighting Game") },
            { "Chaos Mode Option", new OnlineGameModeSelection("chaos", "Chaos") },
            { "Turbo Mode Option", new OnlineGameModeSelection("turbo", "Turbo") },
        };

    // Last mode id this panel drew. This also catches a host-side mode change made by some future
    // authored OnlineGameModeOption rather than through SelectPartyGameMode below.
    private string lastSeenGameModeId;

    protected override void Awake()
    {
        base.Awake();
        ResolveSlotCharacterArt();
        ResolveGameModeLabels();
        // No lobby exists this early, so seed the caption with the default mode. RefreshGameMode
        // overwrites it with the real pick as soon as the panel opens.
        RefreshGameModeButtonLabel(OnlineGameModeSelection.Default);
        ResolvePlayerOptions();

        if (gameModePanel != null)
        {
            gameModePanel.SetActive(false);
        }

        if (playerOptionsPanel != null)
        {
            playerOptionsPanel.SetActive(false);
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

        RefreshPlayerOptions();
        RefreshSlots();
        RefreshStartButton();
        RefreshGameMode();
        RefreshStatus();

        // After the refreshes, so interactable states are current before focus is chosen.
        MaintainFreeze();
        if (gameModeMenuOpen)
        {
            MaintainGameModeMenuFocus();
        }
        else if (playerOptionsOpen)
        {
            MaintainPlayerOptionsFocus();
        }
        else
        {
            MaintainFocus();
        }

        LogLobbyDiagnostics();

        PollMenuInput(HandleCancel);
    }

    // Throttled state dump. Three symptoms (character walking, dead navigation, a stuck status
    // label) all come down to values that are invisible from outside, so print them once a second
    // rather than guess which one is causing the others.
    private float nextDiagnosticTime;

    private void LogLobbyDiagnostics()
    {
        // Editor + the private beta branches only; a shipping player never sees this.
        if (!SteamManager.DebugToolsEnabled)
        {
            return;
        }

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
        CloseGameModeMenuInternal(false);
        ClosePlayerOptionsInternal(false);
        lastSeenGameModeId = null;

        RefreshSlots();
        RefreshStartButton();
        RefreshGameMode();
        RefreshStatus();
    }

    protected override void OnClosed()
    {
        // OnlineMenuPanel.Close already disabled panelRoot before this hook. Do not reactivate it
        // while tearing the whole party screen down.
        CloseGameModeMenuInternal(false);
        ClosePlayerOptionsInternal(false);
    }

    private void OnDestroy()
    {
        if (kickPlayerButton != null)
        {
            kickPlayerButton.onClick.RemoveListener(KickSelectedPlayer);
        }

        if (makeHostButton != null)
        {
            makeHostButton.onClick.RemoveListener(MakeSelectedPlayerHost);
        }
    }

    // ----------------------------------------------------------------------------------------
    // Button handlers
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Friend Profile button. On an empty slot the host opens the Steam invite overlay and reserves
    /// that exact P-number for the next member who accepts.
    /// </summary>
    public void OnSlotPressed(int slotIndex)
    {
        SteamLobbyManager lobby = Lobby;
        if (lobby == null || !lobby.IsPartyHost)
        {
            return;
        }

        if (lobby.TryGetPartySlot(slotIndex, out SteamLobbyManager.PartySlotInfo slot))
        {
            // The host and a still-provisional arrival cannot be managed. A confirmed joined friend
            // opens the modal with that Steam id captured, so later slot changes cannot target the
            // wrong member.
            if (slotIndex > 0
                && !slot.IsHost
                && !slot.IsLocalPlayer
                && !slot.IsProvisional)
            {
                OpenPlayerOptions(slotIndex, slot);
            }
            return;
        }

        // Pass the slot so the button decides the invited player's number: slot 1 = P2, 2 = P3, 3 = P4.
        lobby.InviteToParty(slotIndex);
    }

    /// <summary>Closes the occupied-slot actions without leaving the party.</summary>
    public void ClosePlayerOptions()
    {
        ClosePlayerOptionsInternal(true);
    }

    /// <summary>Host-only removal of the member whose occupied slot opened the modal.</summary>
    public void KickSelectedPlayer()
    {
        SteamLobbyManager lobby = Lobby;
        if (!playerOptionsOpen
            || lobby == null
            || !selectedPlayerId.IsValid
            || !lobby.KickPartyMember(selectedPlayerId))
        {
            return;
        }

        ClosePlayerOptionsInternal(true);
    }

    /// <summary>Host-only transfer of lobby ownership to the selected member.</summary>
    public void MakeSelectedPlayerHost()
    {
        SteamLobbyManager lobby = Lobby;
        if (!playerOptionsOpen
            || lobby == null
            || !selectedPlayerId.IsValid
            || !lobby.TransferPartyHost(selectedPlayerId))
        {
            return;
        }

        ClosePlayerOptionsInternal(false);
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
        if (gameModeMenuOpen || gameModePanel == null || lobby == null || !lobby.IsPartyHost)
        {
            return;
        }

        InstallPartyGameModeHandlers();

        gameModeMenuOpen = true;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // Friends Lobby was added after Panel 2 under the shared GameModesPanel, so it otherwise
        // renders and raycast-blocks on top of the chooser. Treat the chooser as a real subview:
        // keep this controller/IsOpen alive, but temporarily replace its visible panel.
        partyPanelHiddenForGameMode = panelRoot != null
            && !gameModePanel.transform.IsChildOf(panelRoot.transform)
            && panelRoot.activeSelf;
        if (partyPanelHiddenForGameMode)
        {
            panelRoot.SetActive(false);
        }

        gameModePanel.SetActive(true);
        FocusSelectable(gameModePanelFirstSelected);
    }

    /// <summary>Closes the mode chooser and returns focus to the Game Modes button.</summary>
    public void CloseGameModeMenu()
    {
        CloseGameModeMenuInternal(true);

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
        if (playerOptionsOpen)
        {
            ClosePlayerOptions();
            return;
        }

        if (gameModeMenuOpen)
        {
            CloseGameModeMenu();
            return;
        }

        LeaveLobby();
    }

    private void ResolvePlayerOptions()
    {
        if (playerOptionsPanel == null && panelRoot != null)
        {
            Transform[] descendants = panelRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null && descendants[i].name == "Player Options")
                {
                    playerOptionsPanel = descendants[i].gameObject;
                    break;
                }
            }
        }

        if (playerOptionsPanel == null)
        {
            Debug.LogError("[PartyLobbyPanel] The Player Options panel could not be resolved.", this);
            return;
        }

        Button[] optionButtons = playerOptionsPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < optionButtons.Length; i++)
        {
            Button button = optionButtons[i];
            if (button == null)
            {
                continue;
            }

            if (kickPlayerButton == null && button.gameObject.name == "Kick Player Button")
            {
                kickPlayerButton = button;
            }
            else if (makeHostButton == null && button.gameObject.name == "Make Host Button")
            {
                makeHostButton = button;
            }
        }

        if (kickPlayerButton == null || makeHostButton == null)
        {
            Debug.LogError("[PartyLobbyPanel] Player Options needs both Kick Player and Make Host buttons.", this);
            return;
        }

        kickPlayerButton.onClick.RemoveListener(KickSelectedPlayer);
        kickPlayerButton.onClick.AddListener(KickSelectedPlayer);
        makeHostButton.onClick.RemoveListener(MakeSelectedPlayerHost);
        makeHostButton.onClick.AddListener(MakeSelectedPlayerHost);

        // The authored buttons use Explicit navigation but currently have no links. Keep the modal
        // self-contained so up/down cannot escape to the lobby controls underneath it.
        Navigation kickNavigation = kickPlayerButton.navigation;
        kickNavigation.mode = Navigation.Mode.Explicit;
        kickNavigation.selectOnUp = makeHostButton;
        kickNavigation.selectOnDown = makeHostButton;
        kickPlayerButton.navigation = kickNavigation;

        Navigation hostNavigation = makeHostButton.navigation;
        hostNavigation.mode = Navigation.Mode.Explicit;
        hostNavigation.selectOnUp = kickPlayerButton;
        hostNavigation.selectOnDown = kickPlayerButton;
        makeHostButton.navigation = hostNavigation;
    }

    private void OpenPlayerOptions(int slotIndex, SteamLobbyManager.PartySlotInfo slot)
    {
        if (playerOptionsPanel == null || kickPlayerButton == null || makeHostButton == null)
        {
            return;
        }

        selectedPlayerId = slot.SteamId;
        selectedPlayerSlotIndex = slotIndex;
        playerOptionsOpen = true;
        playerOptionsPanel.SetActive(true);
        SetMainLobbyControlsInteractable(false);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        FocusSelectable(kickPlayerButton.gameObject);
    }

    private void ClosePlayerOptionsInternal(bool restoreSlotFocus)
    {
        int previousSlotIndex = selectedPlayerSlotIndex;
        playerOptionsOpen = false;
        selectedPlayerId = default;
        selectedPlayerSlotIndex = -1;

        if (EventSystem.current != null && playerOptionsPanel != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(playerOptionsPanel.transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        if (playerOptionsPanel != null)
        {
            playerOptionsPanel.SetActive(false);
        }

        if (restoreSlotFocus
            && IsOpen
            && previousSlotIndex >= 0
            && previousSlotIndex < slots.Length
            && slots[previousSlotIndex] != null
            && slots[previousSlotIndex].button != null)
        {
            FocusSelectable(slots[previousSlotIndex].button.gameObject);
        }
    }

    private void RefreshPlayerOptions()
    {
        if (!playerOptionsOpen)
        {
            return;
        }

        SteamLobbyManager lobby = Lobby;
        bool targetStillValid = lobby != null
            && lobby.IsPartyHost
            && !lobby.IsPartyMatchStartRequested
            && selectedPlayerId.IsValid
            && selectedPlayerSlotIndex > 0
            && lobby.TryGetPartySlot(selectedPlayerSlotIndex, out SteamLobbyManager.PartySlotInfo slot)
            && slot.IsOccupied
            && !slot.IsHost
            && !slot.IsLocalPlayer
            && !slot.IsProvisional
            && slot.SteamId.Value == selectedPlayerId.Value;

        if (!targetStillValid)
        {
            ClosePlayerOptionsInternal(false);
            return;
        }

        if (kickPlayerButton != null)
        {
            kickPlayerButton.interactable = true;
        }

        if (makeHostButton != null)
        {
            makeHostButton.interactable = true;
        }
    }

    private void SetMainLobbyControlsInteractable(bool interactable)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].button != null)
            {
                slots[i].button.interactable = interactable;
            }
        }

        if (startMatchButton != null)
        {
            startMatchButton.interactable = interactable;
        }

        if (gameModeButton != null)
        {
            gameModeButton.interactable = interactable;
        }
    }

    private void MaintainPlayerOptionsFocus()
    {
        if (playerOptionsPanel == null || !playerOptionsPanel.activeInHierarchy)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return;
        }

        GameObject current = eventSystem.currentSelectedGameObject;
        if (current != null && current.transform.IsChildOf(playerOptionsPanel.transform))
        {
            Selectable currentSelectable = current.GetComponent<Selectable>();
            if (currentSelectable != null && currentSelectable.IsInteractable())
            {
                return;
            }
        }

        if (kickPlayerButton != null
            && kickPlayerButton.gameObject.activeInHierarchy
            && kickPlayerButton.IsInteractable())
        {
            eventSystem.SetSelectedGameObject(kickPlayerButton.gameObject);
            return;
        }

        if (makeHostButton != null
            && makeHostButton.gameObject.activeInHierarchy
            && makeHostButton.IsInteractable())
        {
            eventSystem.SetSelectedGameObject(makeHostButton.gameObject);
        }
    }

    private void CloseGameModeMenuInternal(bool restorePartyPanel)
    {
        gameModeMenuOpen = false;

        if (EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null
                && gameModePanel != null
                && selected.transform.IsChildOf(gameModePanel.transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        if (gameModePanel != null)
        {
            gameModePanel.SetActive(false);
        }

        RestoreOfflineGameModeHandlers();

        if (restorePartyPanel
            && IsOpen
            && partyPanelHiddenForGameMode
            && panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        partyPanelHiddenForGameMode = false;
    }

    // ----------------------------------------------------------------------------------------
    // Presentation
    // ----------------------------------------------------------------------------------------

    private void ResolveSlotCharacterArt()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            SlotWidgets widgets = slots[i];
            if (widgets == null || widgets.characterArt != null || widgets.button == null)
            {
                continue;
            }

            // The Friends Lobby prefab already keeps each portrait directly under its profile
            // button, so no fragile cross-prefab scene references are required here.
            Transform characterArt = widgets.button.transform.Find($"P{i + 1} Character Art");
            if (characterArt != null)
            {
                widgets.characterArt = characterArt.gameObject;
            }
        }
    }

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
            // A joining member is briefly displayed in a provisional fallback slot while the
            // host's exact P-number metadata propagates. Wait for confirmation so a P3/P4 invite
            // cannot flash P2's portrait first.
            SetActiveIfPresent(widgets.characterArt, occupied && !slot.IsProvisional);
            SetActiveIfPresent(widgets.connectingState, occupied && slot.IsProvisional);

            if (widgets.button != null)
            {
                // Profiles remain navigable for hosts and guests. OnSlotPressed is still the action
                // authority. Disable them only while the host's occupied-slot modal owns focus.
                widgets.button.interactable = !playerOptionsOpen;
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
            startMatchButton.interactable = !playerOptionsOpen && canStart;
        }

        if (startMatchLabel == null)
        {
            return;
        }

        if (!isHost)
        {
            startMatchLabel.text = waitingForHostText;
            startMatchLabel.fontSize = 35;
        }
        else if (canStart)
        {
            startMatchLabel.text = startMatchText;
            startMatchLabel.fontSize = 60; // Restore the default font size in case it was shrunk for the waiting-for-players state.
        }
        else if (lobby != null
            && (lobby.IsPartyMatchStartRequested || !lobby.IsPartyLobbyWaitingForHostStart))
        {
            startMatchLabel.text = startingText;
        }
        else
        {
            // Host is alone: an online match still needs a second machine in it.
            startMatchLabel.text = waitingForPlayersText;
            startMatchLabel.fontSize = 35; // Shrink to fit the longer string. The other three states are short enough to use the default font size.
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
        ResolveGameModeLabels();
        RefreshGameModeButtonLabel(mode);

        // The merged layout dropped the standalone "Selected GameMode" field -- the button itself is
        // the readout now. Only write this when it resolved to a genuinely separate object, or both
        // writes land on the same TMP and fight over it every frame.
        if (selectedGameModeLabel != null
            && selectedGameModeLabel != gameModeButtonLabel
            && selectedGameModeLabel.text != mode.DisplayName)
        {
            selectedGameModeLabel.text = mode.DisplayName;
        }

        // Usually SelectPartyGameMode closes immediately. Keep this change detector for authored
        // mode controls that publish to the lobby without calling back into this panel.
        if (lastSeenGameModeId != null && lastSeenGameModeId != mode.Id && gameModeMenuOpen)
        {
            CloseGameModeMenu();
        }
        lastSeenGameModeId = mode.Id;

        if (gameModeButton != null)
        {
            // Guests see the host's pick but cannot change it.
            gameModeButton.interactable = !playerOptionsOpen && lobby.IsPartyHost;
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

    private void ResolveGameModeLabels()
    {
        if (gameModeButtonLabel == null && gameModeButton != null)
        {
            gameModeButtonLabel = gameModeButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        bool selectedLabelIsButtonLabel = selectedGameModeLabel != null
            && gameModeButton != null
            && selectedGameModeLabel.transform.IsChildOf(gameModeButton.transform);
        if (selectedGameModeLabel != null && !selectedLabelIsButtonLabel)
        {
            return;
        }

        // Repairs the old SoloLobby Inspector assignment too: it pointed this field at the button's
        // child TMP instead of the standalone "Selected GameMode" object.
        TextMeshProUGUI[] labels = panelRoot != null
            ? panelRoot.GetComponentsInChildren<TextMeshProUGUI>(true)
            : Array.Empty<TextMeshProUGUI>();
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].gameObject.name == "Selected GameMode")
            {
                selectedGameModeLabel = labels[i];
                return;
            }
        }
    }

    /// <summary>
    /// The Game Modes button doubles as the "currently chosen gamemode" readout now that the panel
    /// no longer carries a separate label for it, so it shows the mode name rather than static text.
    /// DisplayName is never empty in practice (OnlineGameModeSelection degrades an unknown or blank
    /// mode to "Normal mode"), but fall back to the old caption so the button cannot render blank.
    /// </summary>
    private void RefreshGameModeButtonLabel(OnlineGameModeSelection mode)
    {
        if (gameModeButtonLabel == null)
        {
            return;
        }

        string label = !string.IsNullOrEmpty(mode.DisplayName) ? mode.DisplayName : GameModeButtonText;
        if (gameModeButtonLabel.text != label)
        {
            gameModeButtonLabel.text = label;
        }
    }

    private void InstallPartyGameModeHandlers()
    {
        RestoreOfflineGameModeHandlers();

        Button[] buttons = gameModePanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !TryResolvePartyMode(button, out OnlineGameModeSelection mode))
            {
                continue;
            }

            GameModeButtonBinding binding = new GameModeButtonBinding
            {
                Button = button,
                OriginalOnClick = button.onClick,
            };

            OnlineGameModeSelection capturedMode = mode;
            Button.ButtonClickedEvent partyClick = new Button.ButtonClickedEvent();
            partyClick.AddListener(() => SelectPartyGameMode(capturedMode));
            button.onClick = partyClick;
            gameModeButtonBindings.Add(binding);
        }

        if (gameModeButtonBindings.Count == 0)
        {
            Debug.LogError(
                "[PartyLobbyPanel] No game mode buttons were found in the assigned chooser panel.",
                this);
        }
    }

    private void RestoreOfflineGameModeHandlers()
    {
        for (int i = 0; i < gameModeButtonBindings.Count; i++)
        {
            GameModeButtonBinding binding = gameModeButtonBindings[i];
            if (binding != null && binding.Button != null)
            {
                binding.Button.onClick = binding.OriginalOnClick;
            }
        }

        gameModeButtonBindings.Clear();
    }

    private void SelectPartyGameMode(OnlineGameModeSelection mode)
    {
        SteamLobbyManager lobby = Lobby;
        if (!gameModeMenuOpen
            || lobby == null
            || !lobby.SetPartyGameMode(mode.Id, mode.DisplayName))
        {
            return;
        }

        // Close explicitly rather than waiting for RefreshGameMode to see an id change. That also
        // closes correctly when the host selects the mode that was already active.
        lastSeenGameModeId = mode.Id;
        CloseGameModeMenu();
    }

    private bool TryResolvePartyMode(Button button, out OnlineGameModeSelection mode)
    {
        OnlineGameModeOption authoredOption = button.GetComponent<OnlineGameModeOption>();
        if (authoredOption != null)
        {
            mode = authoredOption.Selection;
            return true;
        }

        Transform optionRoot = button.transform;
        while (optionRoot.parent != null && optionRoot.parent != gameModePanel.transform)
        {
            optionRoot = optionRoot.parent;
        }

        if (optionRoot.parent != gameModePanel.transform)
        {
            mode = default;
            return false;
        }

        if (PartyModesByOptionRoot.TryGetValue(optionRoot.name, out mode))
        {
            return true;
        }

        mode = default;
        return false;
    }

    private void MaintainGameModeMenuFocus()
    {
        if (gameModePanel == null || !gameModePanel.activeInHierarchy)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return;
        }

        GameObject current = eventSystem.currentSelectedGameObject;
        if (current != null && current.transform.IsChildOf(gameModePanel.transform))
        {
            Selectable currentSelectable = current.GetComponent<Selectable>();
            if (currentSelectable != null && currentSelectable.IsInteractable())
            {
                return;
            }
        }

        Selectable preferred = gameModePanelFirstSelected != null
            ? gameModePanelFirstSelected.GetComponent<Selectable>()
            : null;
        if (preferred != null && preferred.gameObject.activeInHierarchy && preferred.IsInteractable())
        {
            eventSystem.SetSelectedGameObject(preferred.gameObject);
            return;
        }

        Selectable[] candidates = gameModePanel.GetComponentsInChildren<Selectable>(false);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null
                && candidates[i].gameObject.activeInHierarchy
                && candidates[i].IsInteractable())
            {
                eventSystem.SetSelectedGameObject(candidates[i].gameObject);
                return;
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
