using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "Matchmaking" screen behind VS The World. The player picks 2-player lobbies, 4-player
/// lobbies, or both; the chosen buttons recolour, and "Find Match" stays disabled until at least one
/// size is picked. Starting the search hands off to SteamLobbyManager, which transitions to MainMenu
/// and waits there exactly the way Quick Match already does -- this panel closes on the way out and
/// the "Finding match..." status on the HUD takes over.
///
/// Wiring (public, Button.OnClick-ready):
///   "2 player lobby"    -> ToggleTwoPlayerLobby()
///   "4 player lobby"    -> ToggleFourPlayerLobby()
///   "Find Match Button" -> FindMatch()
///   Back / cancel       -> Back()
///
/// Picking both means "either is fine": the search queries each size in turn, and a lobby this
/// client ends up hosting advertises both so a stricter searcher can still find it.
/// </summary>
public class QuickMatchPanel : OnlineMenuPanel
{
    /// <summary>Widgets for one lobby-size button. Leave anything you do not use unassigned.</summary>
    [System.Serializable]
    public class SizeToggleWidgets
    {
        [Tooltip("Lobby size this button represents (2 or 4).")]
        public int size = 2;

        public Button button;

        [Tooltip("Graphic recoloured on selection -- usually the button's own Image.")]
        public Graphic tintTarget;

        [Tooltip("Optional. Label recoloured alongside the button.")]
        public TextMeshProUGUI label;

        [Tooltip("Optional. Shown only while this size is selected (tick, glow, border...).")]
        public GameObject selectedState;
    }

    [Header("Lobby size")]
    [SerializeField]
    private SizeToggleWidgets[] sizeToggles =
    {
        new SizeToggleWidgets { size = 2 },
        new SizeToggleWidgets { size = 4 },
    };

    [Tooltip("Colour for a selected lobby-size button.")]
    [SerializeField] private Color selectedTint = new Color(1f, 1f, 1f, 1f);

    [Tooltip("Colour for an unselected lobby-size button.")]
    [SerializeField] private Color unselectedTint = new Color(1f, 1f, 1f, 0f);

    [SerializeField] private Color selectedLabelTint = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color unselectedLabelTint = new Color(1f, 1f, 1f, 0f);

    [Header("Find Match")]
    [SerializeField] private Button findMatchButton;
    [SerializeField] private TextMeshProUGUI findMatchLabel;
    [SerializeField] private string findMatchText = "Find Match";

    [Tooltip("Shown on the button while no lobby size is picked.")]
    [SerializeField] private string noSelectionText = "Pick size";

    [Header("Status")]
    [Tooltip("Optional. Shows 'Finding 2 OR 4-player match...' while a search is in flight.")]
    [SerializeField] private TextMeshProUGUI statusLabel;

    [Header("Back target")]
    [Tooltip("Optional. The OnlinePlayMenu COMPONENT (on pfb_GameManager), not the Online Modes object. Reopened when backing out.")]
    [SerializeField] private OnlinePlayMenu returnToOnlineModes;

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // A match that started another way (an accepted invite) takes priority over this menu.
        if (IsOnlineMatchLive)
        {
            Close();
            return;
        }

        RefreshSizeToggles();
        RefreshFindMatchButton();
        RefreshStatus();

        MaintainFreeze();
        MaintainFocus();

        PollMenuInput(Back);
    }

    /// <summary>"VS The World" entry point.</summary>
    public void OpenMatchmakingMenu()
    {
        Open();
    }

    protected override void OnOpened()
    {
        RefreshSizeToggles();
        RefreshFindMatchButton();
        RefreshStatus();
    }

    // ----------------------------------------------------------------------------------------
    // Button handlers
    // ----------------------------------------------------------------------------------------

    public void ToggleTwoPlayerLobby()
    {
        ToggleLobbySize(2);
    }

    public void ToggleFourPlayerLobby()
    {
        ToggleLobbySize(4);
    }

    /// <summary>Toggles one lobby size on or off. Both may be on at once.</summary>
    public void ToggleLobbySize(int size)
    {
        // Deliberately does NOT require a SteamLobbyManager: this is a local preference, so the
        // buttons stay usable in the Editor where Steam never initialises.
        SteamLobbyManager.ToggleQuickMatchSize(size);
        RefreshSizeToggles();
        RefreshFindMatchButton();
    }

    /// <summary>
    /// "Find Match". Does nothing until a lobby size is picked (the button is also non-interactable
    /// in that state, this is the second line of defence for a controller-driven confirm).
    /// SteamLobbyManager takes the player to MainMenu and they wait there as they do today.
    /// </summary>
    public void FindMatch()
    {
        SteamLobbyManager lobby = Lobby;
        if (lobby == null)
        {
            Debug.LogWarning("[QuickMatchPanel] Find Match pressed, but SteamLobbyManager was not found.");
            return;
        }

        if (!SteamLobbyManager.HasQuickMatchSizeSelection)
        {
            return;
        }

        // Same cleanup the other online entries do before a scene transition: the door's gamemode
        // selector holds timeScale at 0 and scopes UI input to whoever opened it.
        OnlinePlayMenu onlineModes = ResolveController(ref returnToOnlineModes);
        if (onlineModes != null)
        {
            onlineModes.CloseForOnlineEntry();
        }
        else
        {
            TempUI?.CloseGamemodesMenuForOnlineEntry();
        }

        Close();
        lobby.StartQuickMatch();
    }

    /// <summary>Cancels an in-flight search and leaves the matchmaking lobby.</summary>
    public void CancelMatchmaking()
    {
        Lobby?.CancelMatchmaking();
    }

    /// <summary>Back button / cancel press: returns to Online Modes.</summary>
    public void Back()
    {
        Close();

        OnlinePlayMenu onlineModes = ResolveController(ref returnToOnlineModes);
        if (onlineModes != null)
        {
            onlineModes.ReopenFromSubMenu();
        }
    }

    // ----------------------------------------------------------------------------------------
    // Presentation
    // ----------------------------------------------------------------------------------------

    private void RefreshSizeToggles()
    {
        for (int i = 0; i < sizeToggles.Length; i++)
        {
            SizeToggleWidgets toggle = sizeToggles[i];
            if (toggle == null)
            {
                continue;
            }

            bool selected = SteamLobbyManager.IsQuickMatchSizeSelected(toggle.size);

            if (toggle.tintTarget != null)
            {
                toggle.tintTarget.color = selected ? selectedTint : unselectedTint;
            }

            if (toggle.button != null)
            {
                ColorBlock colors = toggle.button.colors;
                colors.normalColor = selected ? selectedTint : unselectedTint;
                toggle.button.colors = colors;
            }

            // if (toggle.label != null)
            // {
            //     toggle.label.color = selected ? selectedLabelTint : unselectedLabelTint;
            // }

            SetActiveIfPresent(toggle.selectedState, selected);
        }
    }

    private void RefreshFindMatchButton()
    {
        bool canStart = SteamLobbyManager.HasQuickMatchSizeSelection;

        if (findMatchButton != null)
        {
            findMatchButton.interactable = canStart;
        }

        if (findMatchLabel != null)
        {
            findMatchLabel.text = canStart ? findMatchText : noSelectionText;
        }
    }

    private void RefreshStatus()
    {
        if (statusLabel == null)
        {
            return;
        }

        SteamLobbyManager lobby = Lobby;
        statusLabel.text = lobby != null && lobby.IsSearchingForMatch
            ? $"Finding {lobby.SearchingMatchSizesLabel}-player match..."
            : string.Empty;
    }

    private static void SetActiveIfPresent(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
