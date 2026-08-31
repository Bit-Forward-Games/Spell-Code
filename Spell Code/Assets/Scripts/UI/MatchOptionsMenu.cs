using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controller half of the custom match rules panel. Owns the rule values and drives MatchOptionsUI,
/// the same split TrainingOptionsMachine/TrainingOptionsUI uses.
///
/// Flow: the "Game Options" button on Multiplayer Gamemodes Panel 2 calls OpenMenu(). This panel then
/// owns confirm/back until Back returns to that panel, where the player picks Normal and plays with
/// whatever they set here.
///
/// Lives on the "Match Options" prefab instance, which is parented under pfb_GameManager, so the
/// values survive the SoloLobby -> MainMenu transition with the rest of that hierarchy.
/// </summary>
[RequireComponent(typeof(MatchOptionsUI))]
public class MatchOptionsMenu : MonoBehaviour
{
    // Row order MUST match MatchOptionsUI.RowNames.
    private enum Row
    {
        MatchType,
        RamToWin,
        RamAddedPerRound,
        StartingLives,
        LivesAddedPerRound
    }

    private const int RowCount = 5;

    // Defaults mirror the GameManager constants this panel writes into, so "untouched" means
    // "exactly what the game did before this panel existed".
    public const int DefaultRamToWin = 400;
    public const int DefaultRamPerRound = 100;
    public const int DefaultStartingLives = 1;
    public const int DefaultLivesPerRound = 1;

    [Header("Panel this returns to on Back")]
    [Tooltip("Multiplayer Gamemodes Panel 2. Re-shown when the player backs out of this panel.")]
    [SerializeField] private GameObject returnPanel;

    [Tooltip("Button to re-focus on the return panel, normally the Game Options button itself.")]
    [SerializeField] private GameObject returnPanelSelection;

    [Header("Value ranges")]
    [SerializeField] private int ramToWinMin = 100;
    [SerializeField] private int ramToWinMax = 1000;
    [SerializeField] private int ramToWinStep = 100;

    [SerializeField] private int ramPerRoundMin = 0;
    [SerializeField] private int ramPerRoundMax = 500;
    [SerializeField] private int ramPerRoundStep = 50;

    [SerializeField] private int startingLivesMin = 1;
    [SerializeField] private int startingLivesMax = 9;

    [SerializeField] private int livesPerRoundMin = 0;
    [SerializeField] private int livesPerRoundMax = 5;

    [Header("Cursor repeat")]
    [Tooltip("Seconds between repeats while a direction is held. GetPausePlayerNavigation reports held state, not edges.")]
    [SerializeField] private float navRepeatTime = 0.2f;

    // Current rule values.
    private GameManager.WinCon matchType = GameManager.WinCon.RAMRush;
    private int ramToWin = DefaultRamToWin;
    private int ramPerRound = DefaultRamPerRound;
    private int startingLives = DefaultStartingLives;
    private int livesPerRound = DefaultLivesPerRound;

    private int selectedRow;
    private float navCooldown;
    private Vector2 lastNav;

    private MatchOptionsUI ui;

    /// <summary>
    /// Resolved lazily, NOT in Awake. The panel is authored inactive, and Awake does not run on an
    /// inactive GameObject -- so the Game Options button can invoke OpenMenu() before Awake has ever
    /// happened, and a field cached only in Awake would still be null at that point.
    /// </summary>
    private MatchOptionsUI Ui => ui != null ? ui : (ui = GetComponent<MatchOptionsUI>());

    /// <summary>
    /// True while this panel owns confirm/back. TempUIScript checks this and returns early, the same
    /// way it does for OnlineMenuPanel.OpenPanelCount -- otherwise its handler would fire the focused
    /// gamemode button underneath us, and a single Back would collapse the whole door menu instead of
    /// stepping back one level to Panel 2.
    /// </summary>
    public static bool IsOpen { get; private set; }

    /// <summary>
    /// Statics survive a play session when Enter Play Mode Options disables domain reload, and a
    /// stranded true here would leave the gamemodes menu permanently unable to take input.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsOpen = false;
    }

    // No Awake here on purpose. It would only run the first time the panel is activated -- which is
    // OpenMenu itself -- and hiding the panel there would close it the instant it opened. The prefab
    // instance is authored inactive, which is the correct starting state already.

    private void OnDisable()
    {
        // Never leave the flag set because the object went away mid-transition.
        IsOpen = false;
    }

    /// <summary>Wire this to the "Game Options" button's OnClick.</summary>
    public void OpenMenu()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        selectedRow = 0;
        navCooldown = 0f;
        lastNav = Vector2.zero;

        if (returnPanel != null)
        {
            returnPanel.SetActive(false);
        }

        // Nothing on this panel is a Selectable, so the EventSystem must not keep a stale highlight
        // on the button behind us.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Ui.SetVisible(true);
        RefreshAllRows();
    }

    /// <summary>Back: return to Multiplayer Gamemodes Panel 2 with the chosen rules applied.</summary>
    public void CloseMenu()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        ApplyToGameManager();
        Ui.SetVisible(false);

        if (returnPanel != null)
        {
            returnPanel.SetActive(true);
        }

        GameObject selection = ResolveReturnSelection();
        if (selection == null)
        {
            return;
        }

        // Routed through Pause.SelectFirst like the rest of the menus: it waits a beat before
        // selecting, because returnPanel was re-activated on this same frame and a selection set
        // immediately can be dropped before it takes.
        Pause pause = ResolvePause();
        if (pause != null)
        {
            pause.StartCoroutine(pause.SelectFirst(selection));
        }
        else if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(selection);
        }
    }

    /// <summary>
    /// Unity can only navigate FROM a Selectable, so whatever is handed to the EventSystem here has
    /// to be one. Selecting a plain Image leaves the panel unnavigable AND gives
    /// Pause.TriggerSelectedButton nothing to press -- which is what happens if returnPanelSelection
    /// is wired to a border/backing object rather than the button itself.
    ///
    /// So: take a Selectable on the assigned object, else one around it, else the first usable one in
    /// the return panel.
    /// </summary>
    private GameObject ResolveReturnSelection()
    {
        if (returnPanelSelection != null)
        {
            Selectable direct = returnPanelSelection.GetComponent<Selectable>();
            if (IsUsable(direct))
            {
                return direct.gameObject;
            }

            Selectable inChildren = returnPanelSelection.GetComponentInChildren<Selectable>(true);
            if (IsUsable(inChildren))
            {
                return inChildren.gameObject;
            }

            Selectable inParent = returnPanelSelection.GetComponentInParent<Selectable>();
            if (IsUsable(inParent))
            {
                return inParent.gameObject;
            }
        }

        if (returnPanel != null)
        {
            Selectable[] candidates = returnPanel.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (IsUsable(candidates[i]))
                {
                    return candidates[i].gameObject;
                }
            }
        }

        return null;
    }

    private static bool IsUsable(Selectable selectable)
    {
        return selectable != null
            && selectable.interactable
            && selectable.gameObject.activeInHierarchy;
    }

    private static Pause ResolvePause()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.tempUI == null)
        {
            return null;
        }

        return manager.tempUI.GetComponent<Pause>();
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        Pause pause = ResolvePause();
        if (pause == null)
        {
            return;
        }

        if (pause.WasPausePlayerCancelPressedThisFrame() || pause.WasPausePlayerBackPressedThisFrame())
        {
            CloseMenu();
            return;
        }

        HandleNavigation(pause.GetPausePlayerNavigation());
    }

    /// <summary>
    /// Up/Down move the cursor, Left/Right change the selected row's value. GetPausePlayerNavigation
    /// returns HELD state rather than edges, so repeats are gated on navRepeatTime -- without that a
    /// single flick would run the value to its limit in a few frames.
    /// </summary>
    private void HandleNavigation(Vector2 nav)
    {
        bool released = Mathf.Approximately(nav.x, 0f) && Mathf.Approximately(nav.y, 0f);
        if (released)
        {
            navCooldown = 0f;
            lastNav = Vector2.zero;
            return;
        }

        // A fresh press in a new direction acts immediately; holding repeats on the cooldown.
        bool changedDirection = !Mathf.Approximately(nav.x, lastNav.x) || !Mathf.Approximately(nav.y, lastNav.y);
        if (!changedDirection)
        {
            navCooldown -= Time.unscaledDeltaTime;
            if (navCooldown > 0f)
            {
                return;
            }
        }

        lastNav = nav;
        navCooldown = navRepeatTime;

        // Vertical wins over horizontal so a diagonal can't move the cursor and edit at once.
        if (!Mathf.Approximately(nav.y, 0f))
        {
            MoveSelection(nav.y > 0f ? -1 : 1);
            return;
        }

        if (!Mathf.Approximately(nav.x, 0f))
        {
            AdjustSelectedRow(nav.x > 0f ? 1 : -1);
        }
    }

    private void MoveSelection(int delta)
    {
        selectedRow = Mathf.Clamp(selectedRow + delta, 0, RowCount - 1);
        RefreshAllRows();
    }

    private void AdjustSelectedRow(int delta)
    {
        switch ((Row)selectedRow)
        {
            case Row.MatchType:
                matchType = matchType == GameManager.WinCon.RAMRush
                    ? GameManager.WinCon.Elimination
                    : GameManager.WinCon.RAMRush;
                break;

            case Row.RamToWin:
                ramToWin = Step(ramToWin, delta * ramToWinStep, ramToWinMin, ramToWinMax);
                break;

            case Row.RamAddedPerRound:
                ramPerRound = Step(ramPerRound, delta * ramPerRoundStep, ramPerRoundMin, ramPerRoundMax);
                break;

            case Row.StartingLives:
                startingLives = Step(startingLives, delta, startingLivesMin, startingLivesMax);
                break;

            case Row.LivesAddedPerRound:
                livesPerRound = Step(livesPerRound, delta, livesPerRoundMin, livesPerRoundMax);
                break;
        }

        RefreshAllRows();
    }

    private static int Step(int value, int delta, int min, int max)
    {
        return Mathf.Clamp(value + delta, min, max);
    }

    private void RefreshAllRows()
    {
        bool elimination = matchType == GameManager.WinCon.Elimination;

        Ui.SetRowValue((int)Row.MatchType, elimination ? "Elimination" : "RAM Rush");
        Ui.SetRowValue((int)Row.RamToWin, ramToWin.ToString());
        Ui.SetRowValue((int)Row.RamAddedPerRound, ramPerRound.ToString());
        Ui.SetRowValue((int)Row.StartingLives, startingLives.ToString());
        Ui.SetRowValue((int)Row.LivesAddedPerRound, livesPerRound.ToString());

        for (int i = 0; i < RowCount; i++)
        {
            // The RAM rows mean nothing under Elimination and the lives rows mean nothing under RAM
            // Rush. Dimmed rather than hidden so the panel doesn't reflow as the type changes. The
            // cursor is still allowed to land on them; only the tint changes.
            bool applies = i == (int)Row.MatchType
                || (elimination
                    ? i == (int)Row.StartingLives || i == (int)Row.LivesAddedPerRound
                    : i == (int)Row.RamToWin || i == (int)Row.RamAddedPerRound);

            if (i == selectedRow)
            {
                Ui.SetRowHighlight(i, true, false);
            }
            else
            {
                Ui.SetRowAvailable(i, applies);
            }
        }
    }

    /// <summary>
    /// Pushes the chosen rules into the GameManager values the round logic reads.
    ///
    /// ONLINE IS DELIBERATELY LEFT ALONE. Every value here feeds ramNeededToWinRound / roundLives,
    /// which are part of SerializeSharedGameplayHashState -- so a peer that set them locally instead
    /// of receiving them from the host would diverge on frame one. That is exactly the bug winCon had.
    /// Until the rules are transmitted through the lobby metadata channel (the way GameModeKey
    /// already is) and applied in ApplyOnlineGameMode, an online match keeps the stock values.
    /// </summary>
    public void ApplyToGameManager()
    {
        // A match already in progress has its rules baked into the start token; changing them now
        // would put this peer on different numbers to everyone else.
        if (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive)
        {
            return;
        }

        // The match type is an OVERRIDE rather than a direct winCon write. SetGamemode recomputes
        // winCon from the gamemode every time a mode is picked (so Showdown always gets Elimination
        // and winCon can't leak between matches), which would clobber a direct write the moment the
        // player clicked Normal -- exactly why choosing Elimination here still started RAM Rush.
        GameManager.useCustomWinCon = true;
        GameManager.customWinCon = matchType;

        GameManager.baseRamNeeddedtowin = (ushort)ramToWin;
        GameManager.ramIncreasePerRound = (ushort)ramPerRound;
        GameManager.baseEliminationLives = (ushort)startingLives;
        GameManager.livesIncreasePerRound = (ushort)livesPerRound;

        // maxEliminationLives caps ComputeEliminationRoundLives at 3. Left below the chosen starting
        // count it would clamp the player's own setting away, so raise it to at least that. If you
        // want the cap itself to be tunable it wants its own row rather than this inference.
        GameManager.maxEliminationLives = (ushort)Mathf.Max(3, startingLives);

        PublishRulesIfPartyHost();
    }

    /// <summary>
    /// Publishes the rules to the party lobby so every peer runs the host's numbers. The host applies
    /// them locally above AND publishes here; guests never push their own -- SetPartyMatchRules is
    /// host-only, and the receiving side (ApplyOnlineMatchRules) overwrites whatever a guest had set
    /// locally. That one-way flow is what keeps winCon / ramNeededToWinRound / roundLives -- all
    /// hashed -- identical on every machine.
    /// </summary>
    private static void PublishRulesIfPartyHost()
    {
        SteamLobbyManager lobby = SteamLobbyManager.Instance;
        if (lobby == null || !lobby.IsPartyHost)
        {
            return;
        }

        lobby.SetPartyMatchRules(GameManager.EncodeMatchRules());
    }

    /// <summary>
    /// Restores the stock values. These are STATIC and survive scene loads, so anything that starts a
    /// match which should not use custom rules has to call this -- the same leak that let winCon
    /// carry Elimination out of the offline chooser into the next match.
    /// </summary>
    public static void ResetGameManagerRulesToDefaults()
    {
        // The values live on GameManager, so it owns the defaults -- ApplyOnlineMatchRules needs the
        // same reset when a host publishes no custom rules, and two copies would drift.
        GameManager.ResetMatchRulesToDefaults();
    }
}
