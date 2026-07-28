using UnityEngine;

/// <summary>
/// Routes the multiplayer door's menu stack:
///
///   Multiplayer Gamemodes Panel   (Online Play / Local Play)
///        |-- Online Play  -->  Online Modes   (VS Friends / VS The World)
///                                   |-- VS Friends    -->  Friends Lobby
///                                   |-- VS The World  -->  Matchmaking
///
/// Each step closes the panel it came from and opens the next, so only one is ever up.
///
/// Wiring (every handler is public and parameterless, for Button.OnClick):
///   "Online Play"    -> OpenOnlineModes()
///   "VS Friends"     -> ChooseVsFriends()
///   "VS The World"   -> ChooseVsTheWorld()
///   Back / cancel    -> Back()
///
/// Put this component on a persistent object (next to Pause on pfb_GameManager) and point
/// panelRoot at the "Online Modes" instance -- never at this component's own GameObject, see
/// OnlineMenuPanel for why.
/// </summary>
public class OnlinePlayMenu : OnlineMenuPanel
{
    [Header("Where this menu came from")]
    [Tooltip("The 'Multiplayer Gamemodes Panel' holding Online Play / Local Play. Hidden while Online Modes is up, restored on Back.")]
    [SerializeField] private GameObject multiplayerGamemodesPanel;

    [Tooltip("Optional. Selectable to focus in the multiplayer panel after backing out.")]
    [SerializeField] private GameObject multiplayerGamemodesPanelFirstSelected;

    [Header("Sub menu controllers")]
    // NOTE: these want the COMPONENTS, which live on pfb_GameManager -- not the Friends Lobby /
    // Matchmaking objects themselves (those go in each controller's own panelRoot field). Drag
    // pfb_GameManager in and Unity picks the right component off it.
    [Tooltip("The PartyLobbyPanel COMPONENT (on pfb_GameManager), not the Friends Lobby object.")]
    [SerializeField] private PartyLobbyPanel friendsLobbyController;

    [Tooltip("The QuickMatchPanel COMPONENT (on pfb_GameManager), not the Matchmaking object.")]
    [SerializeField] private QuickMatchPanel matchmakingController;

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // Every online entry leaves this scene. If a match got underway another way (an invite was
        // accepted while this menu was up) the menu has no business still being on screen.
        if (IsOnlineMatchLive)
        {
            Close();
            return;
        }

        PollMenuInput(Back);
    }

    /// <summary>"Online Play" button on the Multiplayer Gamemodes Panel. Opens Online Modes.</summary>
    public void OpenOnlineModes()
    {
        // Deliberately noisy: "the button does nothing" is otherwise indistinguishable from "the
        // OnClick never fired". If this line is absent from the Console, the click never arrived.
        Debug.Log($"[OnlinePlayMenu] Online Play pressed. panelRoot={(panelRoot != null ? panelRoot.name : "MISSING")}", this);

        // multiplayerGamemodesMenuOpened stays SET while this panel is up. It is not just a
        // bookkeeping flag: Pause re-evaluates UI device scoping from it every frame and calls
        // RestoreUiInputDevices() the moment no menu flag is set -- which hands the sticks straight
        // back to the character (it walks around, and the menu stops responding). PlayerController
        // and Pause also gate the pause button and the code-mode prompt on it. TempUIScript.Update
        // skips its own confirm/back handling while OnlineMenuPanel.OpenPanelCount > 0, which is what
        // stops the double-fire this used to clear the flag to avoid.
        if (multiplayerGamemodesPanel != null)
        {
            multiplayerGamemodesPanel.SetActive(false);
        }

        Open();
    }

    /// <summary>
    /// VS Friends. Closes Online Modes, creates the party lobby and heads for MainMenu, where the
    /// four slots live -- that is the only scene the online lobby simulates in, so SteamLobbyManager
    /// makes the trip first and creates the lobby on arrival. The Friends Lobby panel notices the
    /// lobby and opens itself, on the host and on every guest who accepts an invite.
    /// </summary>
    public void ChooseVsFriends()
    {
        // Entry log first, unconditionally: it is the only way to tell "the handler ran and bailed"
        // apart from "the click never got here", and those have completely different causes.
        Debug.Log("[OnlinePlayMenu] VS Friends pressed.", this);

        if (!IsSteamReady())
        {
            // LogError, not LogWarning -- a warning is easy to miss or filter out of the Console,
            // and without a message this looks identical to a dead button.
            Debug.LogError(
                "[OnlinePlayMenu] VS Friends needs Steam, which is unavailable. " +
                $"SteamLobbyManager={(SteamLobbyManager.Instance != null ? "present" : "MISSING")}, " +
                $"SteamClient.IsValid={Steamworks.SteamClient.IsValid}. " +
                "In the Unity Editor this is expected: SteamManager.Awake disables itself under " +
                "UNITY_EDITOR, so it never runs SteamClient.Init or creates the SteamLobbyManager. " +
                "Test this in a player build.",
                this);
            return;
        }

        CloseForOnlineEntry();
        SteamLobbyManager.Instance.HostPartyLobby();
    }

    /// <summary>
    /// VS The World. Closes Online Modes and opens Matchmaking. No search starts here -- the player
    /// picks lobby sizes first and presses Find Match.
    /// </summary>
    public void ChooseVsTheWorld()
    {
        Debug.Log("[OnlinePlayMenu] VS The World pressed.", this);

        QuickMatchPanel matchmaking = ResolveController(ref matchmakingController);
        if (matchmaking == null)
        {
            Debug.LogError("[OnlinePlayMenu] VS The World selected, but no QuickMatchPanel component exists. Add one to this GameObject.", this);
            return;
        }

        Close();
        matchmaking.OpenMatchmakingMenu();
    }

    /// <summary>Back button / cancel press: returns to the Multiplayer Gamemodes Panel.</summary>
    public void Back()
    {
        Close();

        if (multiplayerGamemodesPanel != null)
        {
            multiplayerGamemodesPanel.SetActive(true);
            FocusSelectable(multiplayerGamemodesPanelFirstSelected);
        }
    }

    /// <summary>Reopens Online Modes when a sub menu backs out of itself.</summary>
    public void ReopenFromSubMenu()
    {
        Open();
    }

    /// <summary>
    /// Tears the whole door-menu stack down for an entry that is about to leave this scene.
    /// CloseGamemodesMenuForOnlineEntry is what releases timeScale and the player-scoped UI input
    /// the door menu took; it no-ops unless a gamemode-menu flag is set, which it still is.
    /// </summary>
    public void CloseForOnlineEntry()
    {
        TempUI?.CloseGamemodesMenuForOnlineEntry();
        Close();
    }

    /// <summary>Exposed so the Friends Lobby panel can hand control back here.</summary>
    public PartyLobbyPanel FriendsLobby => ResolveController(ref friendsLobbyController);

    /// <summary>Exposed so the Matchmaking panel can hand control back here.</summary>
    public QuickMatchPanel Matchmaking => ResolveController(ref matchmakingController);
}
