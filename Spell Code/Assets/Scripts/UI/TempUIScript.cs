using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using DG.Tweening;
using System;

public class TempUIScript : MonoBehaviour, ISelectHandler
{
    public TextMeshProUGUI[] playerRamVals;
    public GameManager gameManager;
    public Image[] playerStoreBar;
    public Image[] playerBasicReplaceIcon;
    public Image[] followPlayerHpBar;
    public Image[] followPlayerDamageBar;
    public Image[] playerGoldBar;
    public RectTransform[] SpellInputBorder;
    public TextMeshPro[] SpellInputs;
    public GameObject[] onPlayerUI;
    public GameObject[] emptyQuadrants;

    public GameObject[] vibeCodeQuadrants;
    public Sprite[] spellOnCooldownIcon;
    public Sprite[] spellReadyIcon;
    public Sprite[] roundWinIcon;
    public Image[] flowStateVals;
    public Image[] flowStateDim;
    public TextMeshProUGUI[] stockStabilityVals;
    public Image[] stockStabilityIcons;
    public Image[] stockStabilityDim;
    public TextMeshProUGUI[] demonAuraVals;
    [NonSerialized] public string[] demonAuraGradeVals ={"D", "C", "B", "A", "S", "X"};
    public Image[] demonAuraIcons;
    public Image[] demonAuraDim;
    public TextMeshProUGUI[] repsVals;
    public Image[] repsIcons;
    public Image[] repsDim;
    public float flashAlpha = .5f;
    
    private Coroutine[] damageBarCoroutines = new Coroutine[4];
    private float[] damageBarDisplayFill = new float[4];

    // Track the player's hit counter the last time we fired a damage bar animation.
    // Fire the coroutine only when the counter increases. This avoids the online bug where
    // rollback resim re-set isHit -> UI restarted coroutine every Update -> animation never
    // played to completion. The counter is monotonic and deterministic across rollback so
    // lastSeen never falls behind after a resim.

    private uint[] lastSeenDamageBarHitCount = new uint[4];
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public GameObject MainMenuScreen;

    [Header("Party Kick Message")]
    [SerializeField] private GameObject kickedMessage;
    private bool showKickedMessageOnSoloLobbyLoad;
    private bool kickedMessageVisible;
    private bool suppressPauseUntilKickedMessageInputReleased;

    public bool IsKickedMessageBlockingPause =>
        kickedMessageVisible || suppressPauseUntilKickedMessageInputReleased;

    public GameObject textBoxUI;
    public Animator textBoxAnim;
    public GameObject[] announcer;

    public Transform[] ramIncreaseGlow;

    public bool transitionScreenDisplayed;
    public bool shopScreenDisplayed;

    public float textSpeed;
    private const float TransitionTextEraseDuration = 0.2f;
    private const float TransitionBannerExitDuration = 0.6f;
    private const float TransitionBannerExitPlaybackSpeed = 0.6f;
    private int i = 0;

    private int[] previousRamVals = new int[4];
    private int activeTransitionRequestId = 0;
    private Coroutine activeTypeCoroutine;
    private Coroutine activeReverseTypeCoroutine;

    public float baseScale = 0f;
    public float scalePerChar = 0.05f;
    public float maxScale = 2f;
 
    public GameObject gamemodesMenu;
    
    public GameObject _soloGamemodesMenuFirst;
    public GameObject soloGamemodesMenu;
    public bool soloGamemodesMenuOpened;

    [Header("Multiplayer Gamemodes Menu")] // Tutorial Prompt
    public GameObject _multiplayerGamemodesMenuFirst;
    public GameObject multiplayerGamemodesMenu;
    public bool multiplayerGamemodesMenuOpened;

    [Header("Multiplayer Gamemodes Chooser Menu")] // Tutorial Prompt
    public GameObject _multiplayerGamemodesChooserMenuFirst;
    public GameObject multiplayerGamemodesChooserMenu;
    public bool multiplayerGamemodesChooserMenuOpened;

    [Header("Tutorial Prompt Menu")] // Tutorial Prompt
    public GameObject _tutorialPromptMenuFirst;
    public GameObject tutorialPromptMenu;
    public RectTransform tutorialPromptImage;
    public RectTransform welcomeSign;
    public RectTransform[] tutorialPrompButtons;
    public RectTransform tutorialPromptSelector;
    public TextMeshProUGUI tutorialPromptButtonText;
    public TextMeshProUGUI tutorialPromptButtonText2;
    public bool tutorialPromptMenuOpened;

    [Header("Code Mode Options Menu")] // Code Mode Prompt
    public GameObject[] _codeModeMenuFirst;
    public GameObject[] codeModePromptMenu;
    public bool[] codeModePromptMenuOpened;

    [System.Serializable]
    public class PlayerCodeMode
    {
        public ButtonSelectHandler[] codeModes;
    }
 
    public PlayerCodeMode[] playerCodeMode = new PlayerCodeMode[4];

    public Pause pause;

    private int gamemodesMenuPlayerIndex = -1;
    private InputSystem_Actions input;

    public RectTransform highlightOverlay; // lives outside the Layout Group, e.g. sibling of the panel

    public ArenaNameDisplayHandler arenaNameDisplayHandler;

    [Header("Round End UI")] // Round End UI
    public GameObject roundEndUI;
    public RectTransform winnerPanel;

    public void OnSelect(BaseEventData eventData)
    {
        RectTransform myRect = (RectTransform)transform;
        highlightOverlay.position = myRect.position;
        highlightOverlay.sizeDelta = myRect.sizeDelta;
        highlightOverlay.SetAsLastSibling();
    }

    void Awake()
    {
        input = new InputSystem_Actions();
        ResolveKickedMessage();
        if (kickedMessage != null)
        {
            kickedMessage.SetActive(false);
        }
    }

    void Start()
    {
        followPlayerHpBar = new Image[4];
        followPlayerDamageBar = new Image[4];
        playerStoreBar = new Image[4];
        playerBasicReplaceIcon = new Image[4];
        SpellInputBorder = new RectTransform[4];
        SpellInputs = new TextMeshPro[4];
        onPlayerUI = new GameObject[4];
        damageBarDisplayFill = new float[] { 1f, 1f, 1f, 1f };
        gameManager = GameManager.Instance;

        previousRamVals = new int[4];
        for (int i = 0; i < gameManager.playerCount; i++)
            previousRamVals[i] = gameManager.players[i]?.roundRam ?? 0;

        InitFindingMatchText();
        InitJoiningMatchText();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        input.Enable();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        pause?.RestoreScopedUiInputDevices();
        StopDamageBarCoroutines();

        // Kill and hide both statuses. Resetting the transition flags lets Update recreate either
        // pulse if this UI is later re-enabled while its operation is still in progress.
        SetFindingMatchVisible(false);
        findingMatchShown = false;
        SetJoiningMatchVisible(false);
        joiningMatchShown = false;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        transitionScreenDisplayed = false;
        shopScreenDisplayed = false;

        if (showKickedMessageOnSoloLobbyLoad)
        {
            // Consume this on the very next scene arrival so an interrupted transition cannot make
            // an unrelated later SoloLobby visit display a stale kick notification.
            showKickedMessageOnSoloLobbyLoad = false;
            if (scene.name == "SoloLobby")
            {
                ShowKickedMessage();
            }
            else
            {
                Debug.LogWarning($"[TempUIScript] Kicked-message transition reached '{scene.name}' instead of SoloLobby.");
            }
        }
        else if (scene.name != "SoloLobby" && kickedMessageVisible)
        {
            HideKickedMessage();
        }

        if (scene.name != "MainMenu")
        {
            CloseAllCodeModePrompts();
        }

        if (scene.name == "Gameplay")
        {
            transitionScreenDisplayed = true;
            StartCoroutine(DisplayTransitionScreen(2.0f, "Round #" + (GameManager.Instance.CurrentTotalRoundsPlayed + 1) + "\nKill players to earn RAM!"));

            if(arenaNameDisplayHandler != null)
            {
                arenaNameDisplayHandler.WaitAndDisplay(2.0f, 2.5f);
            }
        }
        else if (scene.name == "Shop")
        {
            shopScreenDisplayed = true;
            StartCoroutine(DisplayTransitionScreen(2.0f, "Pick a new Spellcode"));
        }
    }

    public void SetSoloMenuActive(bool setOpen)
    {
        if (setOpen)
        {
            gamemodesMenuPlayerIndex = ResolveGamemodesMenuPlayerIndex();
            if (pause != null)
            {
                pause.ScopeUiInputToPlayerDevices(gamemodesMenuPlayerIndex);
            }

            gamemodesMenu.SetActive(true);
            soloGamemodesMenu.SetActive(true);
            soloGamemodesMenuOpened = true;
            EventSystem.current.SetSelectedGameObject(_soloGamemodesMenuFirst);
            Time.timeScale = 0f;
        }
        else
        {
            CloseGamemodeMenus();
        }
    }
    
    public void OpenCodeModeMenuPrompt(bool setOpen, int playerIndex)
    {
        OpenCodeModeMenuPrompt(setOpen, playerIndex, true);
    }

    private void OpenCodeModeMenuPrompt(bool setOpen, int playerIndex, bool interactive)
    {
        if (setOpen)
        {
            if (interactive)
            {
                gamemodesMenuPlayerIndex = playerIndex;
                if (pause != null)
                {
                    pause.ScopeUiInputToPlayerDevices(gamemodesMenuPlayerIndex);
                }
            }

            codeModePromptMenuOpened[playerIndex] = true;
            codeModePromptMenu[playerIndex].SetActive(true);
            CanvasGroup promptCanvasGroup = codeModePromptMenu[playerIndex].GetComponent<CanvasGroup>();
            if (promptCanvasGroup == null)
            {
                promptCanvasGroup = codeModePromptMenu[playerIndex].AddComponent<CanvasGroup>();
            }

            // Passive remote panels should look identical, but must not accept local pointer focus.
            promptCanvasGroup.interactable = true;
            promptCanvasGroup.blocksRaycasts = interactive;

            playerCodeMode[playerIndex].codeModes[0].ResetCodeModePromptPresentation();
            playerCodeMode[playerIndex].codeModes[1].ResetCodeModePromptPresentation();
            playerCodeMode[playerIndex].codeModes[0].codeModeSelected = true;
            playerCodeMode[playerIndex].codeModes[1].codeModeSelected = false;
            // A previously selected option deactivates its sibling after the close animation. On a
            // later lobby reset that inactive handler cannot run Update to restore the default, so
            // apply both visuals explicitly whenever this panel reopens.
            playerCodeMode[playerIndex].codeModes[0].SelectCodeMode();
            playerCodeMode[playerIndex].codeModes[1].SelectCodeMode();

            Sequence mySequence = DOTween.Sequence();
            Transform screenTransform = codeModePromptMenu[playerIndex].transform.Find("Code Mode Screen");
            RectTransform screenRect = screenTransform != null ? screenTransform.GetComponent<RectTransform>() : null;

            Transform borderTransform = codeModePromptMenu[playerIndex].transform.Find("Code Mode Menu Border");
            RectTransform borderRect = borderTransform != null ? borderTransform.GetComponent<RectTransform>() : null;

            Transform streaksTransform = codeModePromptMenu[playerIndex].transform.Find("Code Mode Screen Streaks");
            Image streaks = streaksTransform != null ? streaksTransform.GetComponent<Image>() : null;

            screenRect?.DOKill();
            streaks?.DOKill();

            if (streaks != null) 
            {
                streaks.fillAmount = 0f;
            }
            borderRect.localScale = new Vector3(0f, borderRect.localScale.y, borderRect.localScale.z);
            screenRect.localScale = new Vector3(0f, screenRect.localScale.y, screenRect.localScale.z);

            mySequence.Append(borderRect
            .DOScaleX(1f, 0.35f)
            .SetEase(Ease.OutQuad))
            .SetUpdate(true);

            mySequence.Join(screenRect
            .DOScaleX(1f, 0.35f)
            .SetEase(Ease.OutQuad))
            .SetUpdate(true);

            mySequence.AppendInterval(0.2f).SetUpdate(true);

            if (streaks != null) 
            {
                mySequence.Append(DOTween.To(() => (float)streaks.fillAmount, x => streaks.fillAmount = (float)x, 1f, 0.4f)
                .SetEase(Ease.OutQuad))
                .SetUpdate(true);
                if (interactive && pause != null)
                {
                    StartCoroutine(pause.SelectFirst(_codeModeMenuFirst[playerIndex]));
                }
            }
        }
        else
        {
            CloseCodeModeMenuPrompt(playerIndex);
        }
    }

    public void SetMultiplayerMenuActive(bool setOpen)
    {
        if (setOpen)
        {
            gamemodesMenuPlayerIndex = ResolveGamemodesMenuPlayerIndex();
            if (pause != null)
            {
                pause.ScopeUiInputToPlayerDevices(gamemodesMenuPlayerIndex);
            }

            gamemodesMenu.SetActive(true);
            multiplayerGamemodesMenu.SetActive(true);
            multiplayerGamemodesMenuOpened = true;
            EventSystem.current.SetSelectedGameObject(_multiplayerGamemodesMenuFirst);
            Time.timeScale = 0f;
        }
        else
        {
            CloseGamemodeMenus();
        }
    }

    public void SetMultiplayerGameModesMenuActive(bool setOpen)
    {
        if (setOpen)
        {
            gamemodesMenuPlayerIndex = ResolveGamemodesMenuPlayerIndex();
            if (pause != null)
            {
                pause.ScopeUiInputToPlayerDevices(gamemodesMenuPlayerIndex);
            }

            gamemodesMenu.SetActive(true);
            multiplayerGamemodesMenu.SetActive(false);
            multiplayerGamemodesMenuOpened = false;
            multiplayerGamemodesChooserMenu.SetActive(true);
            multiplayerGamemodesChooserMenuOpened = true;
            // EventSystem.current.SetSelectedGameObject(_multiplayerGamemodesChooserMenuFirst);
            StartCoroutine(pause.SelectFirst(_multiplayerGamemodesChooserMenuFirst));
            Time.timeScale = 0f;
        }
        else
        {
            CloseGamemodeMenus();
        }
    }

    // Closing either gamemode menu closes BOTH and clears BOTH flags. The two menus share the
    // gamemodesMenu container so they are never open together, and a close wired to the wrong
    // variant must not strand the other flag: the multiplayer menu's Local Play button called
    // SetSoloMenuActive(false), leaving multiplayerGamemodesMenuOpened true for the whole offline
    // match — PlayerController's pause gate checks that flag, so only P1 (whose pause press closes
    // the hidden menu via the gamemode-menu handler in Update) could ever pause.
    public void CloseGamemodeMenus()
    {
        soloGamemodesMenuOpened = false;
        multiplayerGamemodesMenuOpened = false;
        multiplayerGamemodesChooserMenuOpened = false;
        // codeModePromptMenuOpened[ResolveGamemodesMenuPlayerIndex()] = false;

        if (soloGamemodesMenu != null)
        {
            soloGamemodesMenu.SetActive(false);
        }

        if (multiplayerGamemodesMenu != null)
        {
            multiplayerGamemodesMenu.SetActive(false);
        }

        if (multiplayerGamemodesChooserMenu != null)
        {
            multiplayerGamemodesChooserMenu.SetActive(false);
        }

        if (gamemodesMenu != null)
        {
            gamemodesMenu.SetActive(false);
        }

        gamemodesMenuPlayerIndex = -1;
        pause?.RestoreScopedUiInputDevices();
        Time.timeScale = 1f;
    }

    private void RefreshOnlineCodeModePrompts(bool onlineEntryPending)
    {
        GameManager manager = GameManager.Instance;
        if (manager == null
            || !manager.isOnlineMatchActive
            || manager.players == null
            || codeModePromptMenuOpened == null
            || codeModePromptMenu == null
            || playerCodeMode == null)
        {
            return;
        }

        bool inMainMenu = SceneManager.GetActiveScene().name == "MainMenu";
        int localIndex = manager.localPlayerIndex;
        int promptCount = Math.Min(
            manager.players.Length,
            Math.Min(codeModePromptMenuOpened.Length, Math.Min(codeModePromptMenu.Length, playerCodeMode.Length)));

        for (int playerIndex = 0; playerIndex < promptCount; playerIndex++)
        {
            PlayerController player = manager.players[playerIndex];
            // choosingCodeMode is already true through the opponent wait (SpawnPlayer sets it and
            // the sim that would clear it isn't running yet), so the pending gate is what keeps the
            // prompts off screen until the match is actually live.
            bool shouldShow = inMainMenu
                && !onlineEntryPending
                && player != null
                && manager.IsPlayerSlotConnected(playerIndex)
                && player.choosingCodeMode;

            if (shouldShow && !codeModePromptMenuOpened[playerIndex])
            {
                // Every peer mirrors the same visual state, but only this machine's player is
                // allowed to own EventSystem focus or scope the local UI input devices.
                OpenCodeModeMenuPrompt(true, playerIndex, playerIndex == localIndex);
            }
            else if (!shouldShow && codeModePromptMenuOpened[playerIndex])
            {
                bool waitingForLocalCommit = playerIndex == localIndex
                    && inMainMenu
                    && !onlineEntryPending
                    && player != null
                    && manager.IsPlayerSlotConnected(playerIndex);
                if (!waitingForLocalCommit)
                {
                    // Remote prompts and terminal local cases (disconnect/scene exit) can close
                    // immediately. A normal local confirmation remains open for its handler to
                    // commit the selected option before closing.
                    CloseCodeModeMenuPrompt(playerIndex);
                }
            }
        }
    }

    public void CloseCodeModeMenuPrompt(int playerIndex)
    {
        bool ownsLocalUiControl = gamemodesMenuPlayerIndex == playerIndex;
        codeModePromptMenuOpened[playerIndex] = false;
        if (codeModePromptMenu != null)
        {
            codeModePromptMenu[playerIndex].SetActive(false);
        }

        // Closing a remote visual must not clear focus/input ownership from a local prompt that is
        // still open. Only the prompt that acquired those shared UI resources may release them.
        if (ownsLocalUiControl)
        {
            gamemodesMenuPlayerIndex = -1;
            pause?.RestoreScopedUiInputDevices();
            Time.timeScale = 1f;
        }
    }

    public void CloseAllCodeModePrompts()
    {
        if (codeModePromptMenuOpened == null || codeModePromptMenu == null)
        {
            return;
        }

        int promptCount = Math.Min(codeModePromptMenuOpened.Length, codeModePromptMenu.Length);
        for (int playerIndex = 0; playerIndex < promptCount; playerIndex++)
        {
            if (codeModePromptMenuOpened[playerIndex])
            {
                CloseCodeModeMenuPrompt(playerIndex);
            }
        }
    }

    // Drives the two edges of the online-entry window. Entering it tears down whatever lobby
    // presentation the offline MainMenu already put up (a host can be mid-banner with their
    // code-mode prompt open when a friend accepts the invite); leaving it re-arms the banner so it
    // plays on the frame the match goes live, for the host too -- no scene load happens on their
    // side, so OnSceneLoaded would never clear transitionScreenDisplayed for them.
    private void RefreshOnlineEntryPresentation(bool onlineEntryPending)
    {
        if (onlineEntryPending == onlineEntryPendingLastFrame)
        {
            return;
        }

        onlineEntryPendingLastFrame = onlineEntryPending;

        if (onlineEntryPending)
        {
            CancelTransitionScreen();
            CloseAllCodeModePrompts();
        }
        else
        {
            transitionScreenDisplayed = false;
        }
    }

    // Hard-stops an in-flight announcer banner and leaves it re-armed. Unlike the coroutine's own
    // exit path this is instant: the point is to clear the screen for the "JOINING/STARTING
    // MATCH..." label, not to play a graceful outro.
    private void CancelTransitionScreen()
    {
        // Bumping the id makes any live DisplayTransitionScreen bail at its next checkpoint instead
        // of waking up later and re-showing the box we just hid.
        activeTransitionRequestId++;
        StopTransitionTextCoroutines();

        if (announcer != null)
        {
            foreach (var item in announcer)
            {
                if (item == null)
                {
                    continue;
                }

                item.transform.DOKill();
                item.transform.localScale = Vector3.zero;
            }
        }

        if (textBoxAnim != null)
        {
            textBoxAnim.speed = 1f;
            textBoxAnim.SetInteger("Reverse", 0);
        }

        if (textBoxUI != null)
        {
            textBoxUI.SetActive(false);
        }

        transitionScreenDisplayed = false;
    }

    // Update is called once per frame
    void Update()
    {
        UpdateKickedMessage();

        // One read per frame: every piece of lobby presentation below keys off the same window, so
        // the label, the banner and the prompts can never disagree about whether the match is live.
        bool onlineEntryPending = GameManager.Instance != null && GameManager.Instance.IsOnlineEntryPending;
        RefreshOnlineEntryPresentation(onlineEntryPending);

        UpdateUIBarVals();
        RefreshFindingMatchText();
        RefreshJoiningMatchText(onlineEntryPending);
        RefreshOnlineCodeModePrompts(onlineEntryPending);

        Scene currentScene = SceneManager.GetActiveScene();

        // Suppressed for the whole online handshake, so the banner plays once the match is actually
        // live rather than the instant the joining player's object exists.
        if (currentScene.name == "MainMenu" && GameManager.Instance.players[0] != null && !transitionScreenDisplayed && !onlineEntryPending)
        {
            transitionScreenDisplayed = true;
            StartCoroutine(DisplayTransitionScreen(3.5f, "Pick your first Spellcode"));
        }

        // pause was null-checked inside the branch but dereferenced in the condition; a null pause
        // with the prompt open threw here every frame.
        if (tutorialPromptMenuOpened && pause != null && !pause.paused)
        {
            if (pause.WasPausePlayerSubmitPressedThisFrame())
            {
                pause.TriggerSelectedButton();
            }
        }

        // || codeModePromptMenuOpened[ResolveGamemodesMenuPlayerIndex()]
        if ((soloGamemodesMenuOpened || multiplayerGamemodesMenuOpened || multiplayerGamemodesChooserMenuOpened) && !pause.paused)
        {
            if (gamemodesMenuPlayerIndex < 0)
            {
                gamemodesMenuPlayerIndex = ResolveGamemodesMenuPlayerIndex();
            }

            pause?.ScopeUiInputToPlayerDevices(gamemodesMenuPlayerIndex);

            // An online sub-panel (Online Modes / Friends Lobby / Matchmaking) sits on top of this
            // menu and owns confirm/back while it is up. Running both handlers would fire the
            // focused button TWICE in one frame, and a single Back would collapse the whole door
            // menu instead of stepping back one level. The scoping above still has to run every
            // frame -- Pause re-evaluates device scoping from these flags continuously, so the
            // flags must stay set or UI input falls back to the character.
            if (OnlineMenuPanel.OpenPanelCount > 0)
            {
                return;
            }

            if (pause != null && pause.WasPausePlayerSubmitPressedThisFrame())
            {
                pause.TriggerSelectedButton();
            }

            if (pause != null
                && (pause.WasPausePlayerCancelPressedThisFrame()
                    || pause.WasPausePlayerBackPressedThisFrame()))
            {
                if (soloGamemodesMenuOpened)
                {
                    SetSoloMenuActive(false);
                }
                else if (multiplayerGamemodesChooserMenuOpened)
                {
                    SetMultiplayerGameModesMenuActive(false);
                }
                else
                {
                    SetMultiplayerMenuActive(false);
                }

            Time.timeScale = 1f;
            EventSystem.current.SetSelectedGameObject(null);
        }

    }

        // if (Input.GetKeyDown(KeyCode.Space))
        // {
        //     tutorialPromptMenu.SetActive(true);
        //     Time.timeScale = 0f;
        //     tutorialPromptMenuOpened = true;
        //     StartCoroutine(pause.SelectFirst(_tutorialPromptMenuFirst));
        //     TutorialPromptAnimation(0f, new Vector2 (-212f, 62f), new Vector2 (916f, 344f), new Vector2(1432f, 408f));
        // }
        //if (SteamManager.DebugToolsEnabled && Input.GetKeyDown(KeyCode.Space) && !soloGamemodesMenuOpened && !multiplayerGamemodesMenuOpened && !pause.paused && !gameManager.MainMenuScreen.activeSelf)
        //{
        //    OpenCodeModeMenuPrompt(true);
        //}
    }

    public void QueueKickedMessageForSoloLobby()
    {
        showKickedMessageOnSoloLobbyLoad = true;
        HideKickedMessage();
    }

    private void ResolveKickedMessage()
    {
        if (kickedMessage != null)
        {
            return;
        }

        Transform messageTransform = transform.Find("Canvas/Kicked Message");
        if (messageTransform == null)
        {
            Transform[] descendants = GetComponentsInChildren<Transform>(true);
            messageTransform = descendants.FirstOrDefault(
                descendant => descendant != null && descendant.name == "Kicked Message");
        }

        kickedMessage = messageTransform != null ? messageTransform.gameObject : null;
    }

    private void ShowKickedMessage()
    {
        ResolveKickedMessage();
        if (kickedMessage == null)
        {
            Debug.LogError("[TempUIScript] Cannot show the kick notification because 'Kicked Message' was not found under TempUI.");
            suppressPauseUntilKickedMessageInputReleased = false;
            return;
        }

        kickedMessage.SetActive(true);
        kickedMessageVisible = true;
        suppressPauseUntilKickedMessageInputReleased = true;
    }

    private void HideKickedMessage()
    {
        if (kickedMessage != null)
        {
            kickedMessage.SetActive(false);
        }

        kickedMessageVisible = false;
        suppressPauseUntilKickedMessageInputReleased = false;
    }

    private void UpdateKickedMessage()
    {
        bool dismissInputHeld = input != null && input.UI.Cancel.IsPressed();

        if (!kickedMessageVisible)
        {
            if (suppressPauseUntilKickedMessageInputReleased && !dismissInputHeld)
            {
                suppressPauseUntilKickedMessageInputReleased = false;
            }
            return;
        }

        // UI/Cancel is the same physical pair shown by the prefab's [START] glyph: keyboard Escape
        // and every gamepad's Start button. It stays enabled even for a playerless invite client.
        if (input == null || !input.UI.Cancel.WasPressedThisFrame())
        {
            return;
        }

        if (kickedMessage != null)
        {
            kickedMessage.SetActive(false);
        }

        kickedMessageVisible = false;

        // Keep pause blocked until this press is physically released. Otherwise the same Escape/
        // Start edge can dismiss this overlay and open Pause through PlayerController.FixedUpdate.
        suppressPauseUntilKickedMessageInputReleased = true;
    }

    public void OpenTutorialPromptMenu()
    {
        tutorialPromptMenu.SetActive(true);
        Time.timeScale = 0f;
        tutorialPromptMenuOpened = true;
        pause.ScopeUiInputToPlayerDevices(ResolveGamemodesMenuPlayerIndex());
        StartCoroutine(pause.SelectFirst(_tutorialPromptMenuFirst));
        TutorialPromptAnimation(0f, new Vector2(-212f, 62f), new Vector2(916f, 344f), new Vector2(1432f, 408f));
    }

    /// <summary>
    /// LEGACY entry point from the pre-rework online option. It used to call
    /// OpenInviteOverlayOrHost, which creates a lobby WITHOUT a lobbyMode -- that lobby auto-starts
    /// the instant a second member joins, so a stale button still wired to this would silently
    /// bypass the VS Friends party lobby and drag everyone into a match the host never started.
    ///
    /// It now routes to the same party flow as VS Friends, so pressing any leftover copy of that
    /// button does the right thing instead of the dangerous thing. Prefer wiring buttons to
    /// OnlinePlayMenu.ChooseVsFriends; this exists only so old wiring cannot cause harm.
    /// </summary>
    public void InvitePlayer()
    {
        if (SteamManager.DebugToolsEnabled)
        {
            Debug.LogWarning("[TempUIScript] InvitePlayer() is the legacy online entry point -- redirecting to the VS Friends party lobby. Re-wire this button to OnlinePlayMenu.ChooseVsFriends.");
        }

        CloseGamemodesMenuForOnlineEntry();

        SteamLobbyManager lobbyManager = SteamLobbyManager.Instance;
        if (lobbyManager == null)
        {
            Debug.LogError("[TempUIScript] Online option selected, but SteamLobbyManager was not found.");
            return;
        }

        if (!lobbyManager.HostPartyLobby())
        {
            Debug.LogWarning("[TempUIScript] Online party lobby request could not be started.");
        }
    }

    // Quick Match: size selector + handlers
    // Assign matchSizeText to the "2/3/4" label between your arrow buttons in the Inspector, and set
    // its starting text to "2" (the default). Wire the buttons' OnClick to the methods below.
    public TextMeshProUGUI matchSizeText;

    public TextMeshProUGUI findingMatchText;
    public TextMeshProUGUI joiningMatchText;

    // Looping pulse while waiting for a match. The label itself appears/disappears instantly;
    // only its alpha breathes between 1 and findingMatchPulseMinAlpha. Seconds per half-cycle.
    public float findingMatchPulseDuration = 0.6f;
    [Range(0f, 1f)] public float findingMatchPulseMinAlpha = 0.3f;

    // Last size written into findingMatchText, so the label string is only rebuilt when it changes
    // (RefreshFindingMatchText runs every frame). -1 == "not currently searching".
    private int lastFindingMatchSize = -1;

    // findingMatchShown tracks the INTENDED visibility so we only act on a transition -- Update runs
    // every frame and would otherwise restart the pulse tween continuously.
    private bool findingMatchShown;
    private CanvasGroup findingMatchGroup;
    private Tween findingMatchPulseTween;
    private bool joiningMatchShown;
    private CanvasGroup joiningMatchGroup;
    private Tween joiningMatchPulseTween;
    // Latched wording for the current online-entry window; null while no entry is in flight.
    private string joiningMatchStatusText;
    private const string JoiningMatchStatusText = "JOINING MATCH...";
    private const string StartingMatchStatusText = "STARTING MATCH...";

    // Previous frame's GameManager.IsOnlineEntryPending, so the presentation only reacts to the two
    // edges of that window rather than re-running every frame.
    private bool onlineEntryPendingLastFrame;

    private int matchmakingSize = MinMatchSize;
    private const int MinMatchSize = 2;
    private const int MaxMatchSize = 4;

    // Right arrow button OnClick. (Clamps at 4; change Min/Max to wrap if you'd rather it cycle.)
    public void IncreaseMatchSize()
    {
        matchmakingSize = Mathf.Min(MaxMatchSize, matchmakingSize + 1);
        RefreshMatchSizeText();
    }

    // Left arrow button OnClick.
    public void DecreaseMatchSize()
    {
        matchmakingSize = Mathf.Max(MinMatchSize, matchmakingSize - 1);
        RefreshMatchSizeText();
    }

    public void RefreshMatchSizeText()
    {
        if (matchSizeText != null)
        {
            matchSizeText.text = matchmakingSize.ToString();
        }
    }

    // "Find Match" button OnClick. Starts Quick Match for the currently selected size.
    public void FindMatch()
    {
        CloseGamemodesMenuForOnlineEntry();

        SteamLobbyManager lobbyManager = SteamLobbyManager.Instance;
        if (lobbyManager == null)
        {
            Debug.LogError("[TempUIScript] Find Match selected, but SteamLobbyManager was not found.");
            return;
        }

        lobbyManager.FindMatch(matchmakingSize);
    }

    // "Cancel" button OnClick (optional, shown while searching).
    public void CancelMatchmaking()
    {
        SteamLobbyManager.Instance?.CancelMatchmaking();
    }

    // Shows/hides the "FINDING MATCH..." label from the lobby manager's search state. Called every
    // frame from Update rather than toggled by the buttons, so it stays correct across the deferred
    // MainMenu transition and can't get stuck on if the search ends some way other than Cancel.
    private void RefreshFindingMatchText()
    {
        if (findingMatchText == null)
        {
            return;
        }

        SteamLobbyManager lobbyManager = SteamLobbyManager.Instance;
        bool searching = lobbyManager != null && lobbyManager.IsSearchingForMatch;

        if (searching)
        {
            // Read the size from the lobby manager, NOT from matchmakingSize, that's an instance field
            // and the deferred MainMenu transition can rebuild this UI, resetting it to the 2-player
            // default. Rebuild the string only when the size changes; this runs every frame.
            // SearchingMatchSize is the primary size and doubles as the change detector; the label
            // itself uses the full accepted set, which reads "2 OR 4" when the player picked both.
            int size = lobbyManager.SearchingMatchSize;
            if (size != lastFindingMatchSize)
            {
                lastFindingMatchSize = size;
                findingMatchText.text = $"FINDING {lobbyManager.SearchingMatchSizesLabel}-PLAYER MATCH...";
            }
        }
        else
        {
            lastFindingMatchSize = -1;
        }

        // Only act on a CHANGE, otherwise the pulse tween would be recreated every frame.
        if (searching == findingMatchShown)
        {
            return;
        }

        findingMatchShown = searching;
        SetFindingMatchVisible(searching);
    }

    // Shows/hides the label instantly, and runs a looping alpha pulse while it's waiting. Pure
    // presentation -- DOTween is real-time and NOT deterministic, so it must never drive sim state
    // (see the floppy-spawn desync); a UI alpha tween is safe.
    private void SetFindingMatchVisible(bool visible)
    {
        SetPulsingStatusVisible(
            findingMatchText,
            ref findingMatchGroup,
            ref findingMatchPulseTween,
            visible);
    }

    // Shows the shared online-entry label for the match-start handshake. A VS Friends lobby still
    // counts as an online entry while it gathers players (that broader window suppresses gameplay
    // prompts), but it is not joining/starting a match until the host presses Start Match.
    private void RefreshJoiningMatchText(bool onlineEntryPending)
    {
        if (joiningMatchText == null)
        {
            return;
        }

        SteamLobbyManager lobbyManager = SteamLobbyManager.Instance;
        bool starting = lobbyManager != null && lobbyManager.IsStartingMatch;
        bool joining = lobbyManager != null && lobbyManager.IsJoiningMatch;
        bool gatheringParty =
            lobbyManager != null
            && lobbyManager.IsPartyEntryPending
            && !lobbyManager.IsPartyMatchStartRequested
            && (!lobbyManager.IsInPartyLobby || lobbyManager.IsPartyLobbyWaitingForHostStart);
        bool showMatchStatus = onlineEntryPending && !gatheringParty;

        if (showMatchStatus)
        {
            // Latch the wording: the role flags go quiet for the last stretch of the window, and the
            // host's label must not flip to "JOINING MATCH..." on the way out.
            if (starting)
            {
                joiningMatchStatusText = StartingMatchStatusText;
            }
            else if (joining || string.IsNullOrEmpty(joiningMatchStatusText))
            {
                joiningMatchStatusText = JoiningMatchStatusText;
            }

            if (joiningMatchText.text != joiningMatchStatusText)
            {
                joiningMatchText.text = joiningMatchStatusText;
            }
        }
        else
        {
            joiningMatchStatusText = null;
        }

        if (showMatchStatus == joiningMatchShown)
        {
            return;
        }

        joiningMatchShown = showMatchStatus;
        SetJoiningMatchVisible(showMatchStatus);
    }

    private void SetJoiningMatchVisible(bool visible)
    {
        SetPulsingStatusVisible(
            joiningMatchText,
            ref joiningMatchGroup,
            ref joiningMatchPulseTween,
            visible);
    }

    // Both matchmaking status labels use the same unscaled alpha pulse.
    private void SetPulsingStatusVisible(
        TextMeshProUGUI statusText,
        ref CanvasGroup cachedGroup,
        ref Tween pulseTween,
        bool visible)
    {
        if (statusText == null)
        {
            pulseTween?.Kill();
            pulseTween = null;
            cachedGroup = null;
            return;
        }

        CanvasGroup group = GetStatusTextGroup(statusText, ref cachedGroup);
        if (group == null)
        {
            return;
        }

        // Kill first: a live yoyo tween would keep writing alpha over whatever we set below.
        pulseTween?.Kill();
        pulseTween = null;

        if (!visible)
        {
            group.alpha = 1f; // leave it clean for the next search
            statusText.gameObject.SetActive(false);
            return;
        }

        group.alpha = 1f;
        statusText.gameObject.SetActive(true);

        // Full -> dim -> full, forever, until the search ends.
        // DOTween.To rather than CanvasGroup.DOFade so this doesn't depend on DOTween's UI module
        // being generated. SetUpdate(true) = unscaled: the gamemodes menu sets Time.timeScale = 0,
        // which would otherwise freeze the pulse. SetLink kills the tween if the label is destroyed
        // (ExecuteOrder66 tears down tempUI mid-search).
        pulseTween = DOTween
            .To(() => group.alpha, a => group.alpha = a, findingMatchPulseMinAlpha, findingMatchPulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .SetLink(statusText.gameObject);
    }

    private CanvasGroup GetStatusTextGroup(TextMeshProUGUI statusText, ref CanvasGroup cachedGroup)
    {
        if (cachedGroup == null && statusText != null)
        {
            cachedGroup = statusText.GetComponent<CanvasGroup>();
            if (cachedGroup == null)
            {
                cachedGroup = statusText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        return cachedGroup;
    }

    // Force the label to a known hidden state on load, so a label left enabled in the Inspector (or a
    // rebuilt tempUI) doesn't start visible before the first search.
    private void InitFindingMatchText()
    {
        if (findingMatchText == null)
        {
            return;
        }

        findingMatchShown = false;
        lastFindingMatchSize = -1;

        findingMatchPulseTween?.Kill();
        findingMatchPulseTween = null;

        CanvasGroup group = GetStatusTextGroup(findingMatchText, ref findingMatchGroup);
        if (group != null)
        {
            group.alpha = 1f;
        }

        findingMatchText.gameObject.SetActive(false);
    }

    // Keep a newly assigned label hidden until a join or hosted-start handshake is in flight.
    private void InitJoiningMatchText()
    {
        if (joiningMatchText == null)
        {
            return;
        }

        joiningMatchShown = false;
        joiningMatchStatusText = null;

        joiningMatchPulseTween?.Kill();
        joiningMatchPulseTween = null;

        CanvasGroup group = GetStatusTextGroup(joiningMatchText, ref joiningMatchGroup);
        if (group != null)
        {
            group.alpha = 1f;
        }

        joiningMatchText.gameObject.SetActive(false);
    }

    public void CloseGamemodesMenuForOnlineEntry()
    {
        // A normal pause is handled later by GameManager.StartOnlineMatch. Do not force timeScale
        // back to 1 while that pause UI is still open; this cleanup is only for the mode selectors.
        if (!soloGamemodesMenuOpened && !multiplayerGamemodesMenuOpened && !multiplayerGamemodesChooserMenuOpened)
        {
            return;
        }

        if (pause != null)
        {
            pause.SaveSettings();
        }

        // Clear BOTH gamemode menus. The online invite is reachable from the multiplayer menu
        // (solo lobby door 2); leaving multiplayerGamemodesMenuOpened set would make a later
        // online-match Resume() run BackToMultiplayerSelector and freeze the sim (timeScale 0).
        CloseGamemodeMenus();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public int ResolveGamemodesMenuPlayerIndex()
    {
        GameManager manager = gameManager != null ? gameManager : GameManager.Instance;
        if (manager == null || manager.players == null)
        {
            return 0;
        }

        if (manager.isOnlineMatchActive
            && manager.localPlayerIndex >= 0
            && manager.localPlayerIndex < manager.players.Length
            && manager.players[manager.localPlayerIndex] != null
            && manager.players[manager.localPlayerIndex].isConnected)
        {
            return manager.localPlayerIndex;
        }

        for (int i = 0; i < manager.playerCount && i < manager.players.Length; i++)
        {
            PlayerController player = manager.players[i];
            if (player != null && player.isConnected)
            {
                return i;
            }
        }

        return 0;
    }

    public void UpdateUIBarVals()
    {
        Scene currentScene = SceneManager.GetActiveScene();

        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            PlayerController quadrantPlayer = GameManager.Instance.players[i];

            // A player who disconnected mid-match is eliminated: clear their quadrant so a
            // stale health bar and chosen-spell display don't linger. Mirror the empty-slot look.
            if (quadrantPlayer != null && !quadrantPlayer.isConnected)
            {
                GameObject onPlayerUiGO = FindChildContainingName(quadrantPlayer.gameObject, "On-Player UI");
                if (onPlayerUiGO != null) onPlayerUiGO.SetActive(false);

                if (emptyQuadrants != null && i < emptyQuadrants.Length && emptyQuadrants[i] != null)
                    emptyQuadrants[i].SetActive(true);

                if (GameManager.Instance.spellDisplays != null && i < GameManager.Instance.spellDisplays.Length
                    && GameManager.Instance.spellDisplays[i] != null)
                    GameManager.Instance.spellDisplays[i].ClearForDisconnect();

                // Clear the rest of this quadrant's live readouts so nothing stale lingers.
                if (i < playerRamVals.Length && playerRamVals[i] != null) playerRamVals[i].text = "";
                if (i < playerGoldBar.Length && playerGoldBar[i] != null) playerGoldBar[i].fillAmount = 0f;
                if (i < playerStoreBar.Length && playerStoreBar[i] != null) playerStoreBar[i].fillAmount = 0f;
                if (i < playerBasicReplaceIcon.Length && playerBasicReplaceIcon[i] != null) playerBasicReplaceIcon[i].enabled = false;
                if (i < flowStateVals.Length && flowStateVals[i] != null) flowStateVals[i].enabled = false;
                if (i < flowStateDim.Length && flowStateDim[i] != null) flowStateDim[i].enabled = false;
                if (i < stockStabilityVals.Length && stockStabilityVals[i] != null) stockStabilityVals[i].enabled = false;
                if (i < stockStabilityIcons.Length && stockStabilityIcons[i] != null) stockStabilityIcons[i].enabled = false;
                if (i < stockStabilityDim.Length && stockStabilityDim[i] != null) stockStabilityDim[i].enabled = false;
                if (i < demonAuraVals.Length && demonAuraVals[i] != null) demonAuraVals[i].enabled = false;
                if (i < demonAuraIcons.Length && demonAuraIcons[i] != null) demonAuraIcons[i].enabled = false;
                if (i < demonAuraDim.Length && demonAuraDim[i] != null) demonAuraDim[i].enabled = false;
                if (i < repsVals.Length && repsVals[i] != null) repsVals[i].enabled = false;
                if (i < repsIcons.Length && repsIcons[i] != null) repsIcons[i].enabled = false;
                if (i < repsDim.Length && repsDim[i] != null) repsDim[i].enabled = false;
                continue;
            }

            onPlayerUI[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "On-Player UI").gameObject;

            followPlayerHpBar[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "Health Bar").GetComponent<Image>();
            playerStoreBar[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "Store Bar").GetComponent<Image>();
            playerBasicReplaceIcon[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "Basic Attack Replacement Icon").GetComponent<Image>();
            SpellInputBorder[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "Spell Input Border").GetComponent<RectTransform>();
            SpellInputs[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "Spell_Inputs").GetComponent<TextMeshPro>();

            int charCount = SpellInputs[i].text.Length;
            float targetScale = Mathf.Clamp(baseScale + (charCount * scalePerChar), baseScale, maxScale);

            // Smoothly lerp toward target scale
            Vector3 currentScale = SpellInputBorder[i].localScale;
            float smoothedScale = Mathf.Lerp(currentScale.x, targetScale, Time.deltaTime * 10f);
            SpellInputBorder[i].localScale = new Vector3(smoothedScale, 0.025f, 1f);

            int _ramIncrease = GameManager.Instance.players[i].roundRam;

            // Initialize tracking for newly joined players
            if (previousRamVals[i] == 0 && _ramIncrease != 0)
                previousRamVals[i] = _ramIncrease;

            if (_ramIncrease != previousRamVals[i])
            {
                Image glowImage = ramIncreaseGlow[i].GetComponent<Image>();
                Sequence fadeSequence = DOTween.Sequence();
                fadeSequence.Append(glowImage.DOFade(1f, 0.2f));
                fadeSequence.Append(glowImage.DOFade(0f, 0.3f));
                previousRamVals[i] = _ramIncrease;
            }

            // Fire the damage bar coroutine only on the rising edge of damageBarHitCount.
            // The previous design watched player.isHit, but in online play rollback resim
            // would re-run HitboxManager which re-set isHit -> UI restarted the coroutine
            // every Update -> WaitForSeconds never elapsed -> bar never animated. The
            // counter is monotonic across rollback (deterministic) so lastSeen never falls
            // behind after a resim, and the coroutine fires exactly once per actual hit.
            uint currentHitCount = GameManager.Instance.players[i].damageBarHitCount;
            if (currentHitCount != lastSeenDamageBarHitCount[i])
            {
                lastSeenDamageBarHitCount[i] = currentHitCount;
                if (damageBarCoroutines[i] != null) StopCoroutine(damageBarCoroutines[i]);
                damageBarCoroutines[i] = StartCoroutine(DamageBar(i));
            }

            float fillAmountVal = GameManager.Instance.players[i].charData != null? ((float)GameManager.Instance.players[i].currentPlayerHealth / GameManager.Instance.players[i].charData.playerHealth) : 0;
            float fillGoldAmountVal = GameManager.Instance.players[i].charData != null? ((float)GameManager.Instance.players[i].roundRam / GameManager.Instance.ramNeededToWinRound) : 0;
            followPlayerHpBar[i].fillAmount = fillAmountVal;
            playerRamVals[i].text = /*(GameManager.Instance.ramNeededToWinRound - GameManager.Instance.players[i].roundRam < PlayerController.baseRamKillBonus)?"MATCH POINT!":*/$"{GameManager.Instance.players[i].roundRam}";
            playerGoldBar[i].fillAmount = (GameManager.Instance.ramNeededToWinRound - GameManager.Instance.players[i].roundRam < PlayerController.baseRamKillBonus)?1:fillGoldAmountVal;

            emptyQuadrants[i].SetActive(false);

            flowStateVals[i].enabled = false;
            stockStabilityVals[i].enabled = false;
            stockStabilityIcons[i].enabled = false;
            demonAuraVals[i].enabled = false;
            demonAuraIcons[i].enabled = false;
            repsVals[i].enabled = false;
            repsIcons[i].enabled = false;

            flowStateDim[i].enabled = false;
            stockStabilityDim[i].enabled = false;
            demonAuraDim[i].enabled = false;
            repsDim[i].enabled = false;

            if (vibeCodeQuadrants != null && i < vibeCodeQuadrants.Length && vibeCodeQuadrants[i] != null)
                    vibeCodeQuadrants[i].SetActive(GameManager.Instance.players[i].vibeCoding);
            if (quadrantPlayer.flowState !=0)
            {
                flowStateVals[i].enabled = true;
                flowStateDim[i].enabled = true;
            }
            if (quadrantPlayer.stockStabilityModified != 0)
            {
                stockStabilityVals[i].enabled = true;
                stockStabilityIcons[i].enabled = true;
                stockStabilityDim[i].enabled = true;
            }
            if (quadrantPlayer.demonAura != 0)
            {
                demonAuraVals[i].enabled = true;
                demonAuraIcons[i].enabled = true;
                demonAuraDim[i].enabled = true;
            }
            if (quadrantPlayer.reps != 0)
            {
                repsVals[i].enabled = true;
                repsIcons[i].enabled = true;
                repsDim[i].enabled = true;
            }

            foreach (SpellData spell in GameManager.Instance.players[i].spellList)
            {
                if (spell.brands.Contains(Brand.VWave))
                {
                    flowStateVals[i].enabled = true;
                    flowStateDim[i].enabled = true;
                }
                if (spell.brands.Contains(Brand.BigStox))
                {
                    stockStabilityVals[i].enabled = true;
                    stockStabilityIcons[i].enabled = true;
                    stockStabilityDim[i].enabled = true;
                }
                if (spell.brands.Contains(Brand.DemonX))
                {
                    demonAuraVals[i].enabled = true;
                    demonAuraIcons[i].enabled = true;
                    demonAuraDim[i].enabled = true;
                }
                if (spell.brands.Contains(Brand.Killeez))
                {
                    repsVals[i].enabled = true;
                    repsIcons[i].enabled = true;
                    repsDim[i].enabled = true;
                }

            }

            // flowStateVals[i].enabled = true;
            flowStateVals[i].fillAmount = (float)GameManager.Instance.players[i].flowState / FlowState.maxFlowState;

            // stockStabilityVals[i].enabled = true;
            // stockStabilityIcons[i].enabled = true;
            stockStabilityVals[i].text = GameManager.Instance.players[i].stockStabilityModified.ToString() + "%";

            // demonAuraVals[i].enabled = true;
            demonAuraIcons[i].fillAmount = (float)GameManager.Instance.players[i].demonAuraLifeSpanTimer / DemonAura.DemonAuraResetTime;
            demonAuraVals[i].text = demonAuraGradeVals[Mathf.CeilToInt(GameManager.Instance.players[i].demonAura/20)];

            // repsVals[i].enabled = true;
            // repsIcons[i].enabled = true;
            repsVals[i].text = GameManager.Instance.players[i].reps.ToString();

            if (repsVals[i].text == "0")
            {
                repsVals[i].enabled = false;
                repsIcons[i].enabled = false;
            }
            else if (repsVals[i].text != "0")
            {
                repsVals[i].enabled = true;
                repsIcons[i].enabled = true;
            }

            //Spell Store Bar
            float storeFillAmount = (float)GameManager.Instance.players[i].storedCodeDuration / 240;//TODO: change 240 to use the scale the bar length based on spell length
            playerStoreBar[i].fillAmount = storeFillAmount;
            

            //Basic attack replacement Icon logic
            if(GameManager.Instance.players[i].basicSpawnOverride != "")
            {
                playerBasicReplaceIcon[i].enabled = true;
                playerBasicReplaceIcon[i].sprite = SpellDictionary.Instance.spellDict[GameManager.Instance.players[i].basicSpawnOverride].readyIcon;
            }
            else
            {
                playerBasicReplaceIcon[i].enabled = false;
            }
        }
    }

    public IEnumerator DamageBar(int playerIndex)
    {
        if (GameManager.Instance == null
            || playerIndex < 0
            || playerIndex >= GameManager.Instance.players.Length
            || GameManager.Instance.players[playerIndex] == null)
        {
            yield break;
        }

        PlayerController player = GameManager.Instance.players[playerIndex];
        if (player.charData == null)
        {
            yield break;
        }

        GameObject damageBarObject = FindChildContainingName(player.gameObject, "Damage Bar");
        Image damageBar = damageBarObject != null ? damageBarObject.GetComponent<Image>() : null;
        if (damageBar == null)
        {
            yield break;
        }

        followPlayerDamageBar[playerIndex] = damageBar;

        // Note: previously we did `player.isHit = false` here to "consume" the trigger flag,
        // but that was UI code writing to a field that's part of the deterministic sim's
        // state hash. The damageBarHitCount counter pattern replaces that flag-clear with a
        // UI-side lastSeen tracker, so the sim's isHit is left untouched by UI.

        float previousHealthAmount = damageBarDisplayFill[playerIndex];
        
        float newHealthAmount = (float)player.currentPlayerHealth / player.charData.playerHealth;
        
        damageBar.fillAmount = previousHealthAmount;

        yield return new WaitForSeconds(1f);

        float elapsedTime = 0f;
        float animationDuration = 1f;

        while (elapsedTime < animationDuration)
        {
            if (damageBar == null)
            {
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            damageBar.fillAmount = Mathf.Lerp(previousHealthAmount, newHealthAmount, t);
            yield return null;
        }

        if (damageBar == null)
        {
            yield break;
        }

        damageBar.fillAmount = newHealthAmount;
        damageBarDisplayFill[playerIndex] = newHealthAmount;
    }

    private void StopDamageBarCoroutines()
    {
        if (damageBarCoroutines == null)
        {
            return;
        }

        for (int index = 0; index < damageBarCoroutines.Length; index++)
        {
            if (damageBarCoroutines[index] != null)
            {
                StopCoroutine(damageBarCoroutines[index]);
                damageBarCoroutines[index] = null;
            }
        }
    }

    public IEnumerator DisplayTransitionScreen(float transitionTime, string text)
    {
        int requestId = ++activeTransitionRequestId;

        StopTransitionTextCoroutines();
        textBoxUI.SetActive(true);
        textBoxAnim.speed = 1f;
        textBoxAnim.SetInteger("Reverse", 0);
        textBoxAnim.Rebind();
        textBoxAnim.Update(0f);
        textBoxAnim.Play("Anim_TextBox", 0, 0f);
        textBoxAnim.Play("Anim_TextBoxShadow", 1, 0f);

        foreach (var item in announcer)
        {
            item.transform.DOKill();
            item.transform.localScale = Vector3.zero;
        }

        foreach (var item in announcer)
        {
            item.transform.DOScale(new Vector2(0.17f, 0.33575f), 1f).SetEase(Ease.OutBounce);
        }

        Transform childTransform = textBoxUI.transform.Find("Text");
        TextMeshProUGUI screenText = null;

        if (childTransform != null)
            screenText = childTransform.GetComponent<TextMeshProUGUI>();

        if (screenText != null)
        {
            screenText.text = "";
            activeTypeCoroutine = StartCoroutine(TypeLine(screenText, text, false, textSpeed));
        }
        
        yield return new WaitForSeconds(transitionTime);

        // The scene (and this HUD) can be torn down during the wait -- the online round-end message
        // is shown long enough to persist until the next scene loads -- so bail before touching
        // now-destroyed UI. This coroutine is started from GameManager, so it survives the HUD's
        // destruction and would otherwise wake on a dead instance and throw.
        if (textBoxUI == null || textBoxAnim == null)
            yield break;

        if (requestId != activeTransitionRequestId)
            yield break;

        StopTransitionTextCoroutines();

        textBoxAnim.speed = TransitionBannerExitPlaybackSpeed;
        textBoxAnim.SetInteger("Reverse", 1);

        foreach (var item in announcer)
        {
            item.transform.DOKill();
            item.transform.DOScale(0f, 1f).SetEase(Ease.InOutQuint);
        }

        if (screenText != null)
        {
            screenText.text = text;
            float reverseTextSpeed = screenText.text.Length > 0
                ? TransitionTextEraseDuration / screenText.text.Length
                : TransitionTextEraseDuration;
            activeReverseTypeCoroutine = StartCoroutine(TypeLine(screenText, text, true, reverseTextSpeed));
            yield return activeReverseTypeCoroutine;
            activeReverseTypeCoroutine = null;

            yield return new WaitForSeconds(TransitionBannerExitDuration - TransitionTextEraseDuration);
        }
        else
        {
            yield return new WaitForSeconds(TransitionBannerExitDuration);
        }

        if (requestId != activeTransitionRequestId)
            yield break;

        textBoxAnim.speed = 1f;
        textBoxAnim.SetInteger("Reverse", 0);
        textBoxUI.SetActive(false);
    }

    public void TutorialPromptAnimation(float tutorialPromptMenuYPos, Vector2 welcomeSignPos, Vector2 buttonScale, Vector2 tutorialSelectorPos)
    {
        Sequence mySequence = DOTween.Sequence();

        tutorialPromptImage.DOAnchorPos(new Vector2(tutorialPromptImage.anchoredPosition.x, tutorialPromptMenuYPos), 0.5f).SetEase(Ease.OutQuad).SetUpdate(true);
        welcomeSign.DOAnchorPos(new Vector2(welcomeSignPos.x, welcomeSignPos.y), 0.5f).SetEase(Ease.OutQuad).SetUpdate(true);
        if (tutorialPromptMenuOpened)
        {
            DOTween.To(() => tutorialPromptButtonText.text, 
                    x => tutorialPromptButtonText.text = x, 
                    "I'm good!", 1f)
                .SetEase(Ease.Linear).SetUpdate(true);

            DOTween.To(() => tutorialPromptButtonText2.text, 
                    x => tutorialPromptButtonText2.text = x, 
                    "Show Me!", 1f)
                .SetEase(Ease.Linear).SetUpdate(true);
        }


        for (int i = 0; i < 2; i++)
        {
            mySequence.AppendInterval(0.1f).SetUpdate(true);
            // mySequence.Append(tutorialPrompButtons[i].DOSizeDelta(new Vector2(buttonScale.x, buttonScale.y), 0.35f).SetEase(Ease.OutQuad).SetUpdate(true));
            tutorialPrompButtons[i].DOSizeDelta(new Vector2(buttonScale.x, buttonScale.y), 0.35f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
        mySequence.AppendInterval(0.1f).SetUpdate(true);
        // mySequence.Append(tutorialPromptSelector.DOSizeDelta(new Vector2(tutorialSelectorPos.x, tutorialSelectorPos.y), 0.35f).SetEase(Ease.OutQuad).SetUpdate(true));
        tutorialPromptSelector.DOSizeDelta(new Vector2(tutorialSelectorPos.x, tutorialSelectorPos.y), 0.35f).SetEase(Ease.OutQuad).SetUpdate(true);

        if (!tutorialPromptMenuOpened) 
        {
            DOTween.To(() => tutorialPromptButtonText.text, 
                    x => tutorialPromptButtonText.text = x, 
                    "", 0.01f)
                .SetEase(Ease.Linear).SetUpdate(true);

            DOTween.To(() => tutorialPromptButtonText2.text, 
                    x => tutorialPromptButtonText2.text = x, 
                    "qqq", 0.01f)
                .SetEase(Ease.Linear).SetUpdate(true);
            mySequence.AppendCallback(() => tutorialPromptMenu.SetActive(false));
        }
    }

    public void RemoveButtonText()
    {
        StartCoroutine(TypeLine(tutorialPromptButtonText, "", true, 0.03f));
        StartCoroutine(TypeLine(tutorialPromptButtonText2, "", true, 0.03f));
    }

    public void ExitTutorialPromptAnimation()
    {
        // Mirror of OpenTutorialPromptMenu, which sets the flag/timeScale/input scope before
        // animating. Both prompt buttons ("I'm good!" and "Show Me!") route here via UnityEvents on pfb_GameManager
        // Flag must be cleared BEFORE the call so the animation takes its close branch.
        tutorialPromptMenuOpened = false;
        Time.timeScale = 1f;
        pause?.RestoreScopedUiInputDevices();
        TutorialPromptAnimation(-1000f, new Vector2(-1820f, -480f), new Vector2(0f, 0f), new Vector2(0f, 0f));
    }

    IEnumerator TypeLine(TextMeshProUGUI screenText, string text, bool reverse, float textSpeed)
    {
        if (!reverse)
        {
            foreach (char c in text.ToCharArray())
            {
                screenText.text += c;
                yield return new WaitForSeconds(textSpeed);
            }
        }
        else
        {
            while (screenText.text.Length > 0)
            {
                screenText.text = screenText.text.Substring(0, screenText.text.Length - 1);
                yield return new WaitForSeconds(textSpeed);
            }
        }
    }

    private void StopTransitionTextCoroutines()
    {
        if (activeTypeCoroutine != null)
        {
            StopCoroutine(activeTypeCoroutine);
            activeTypeCoroutine = null;
        }

        if (activeReverseTypeCoroutine != null)
        {
            StopCoroutine(activeReverseTypeCoroutine);
            activeReverseTypeCoroutine = null;
        }
    }

    GameObject FindChildContainingName(GameObject parent, string namePart)
    {
        // Get all child transforms (including grandchildren, etc.)
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);

        // Iterate through the children to find one whose name contains the specified part
        foreach (Transform childTransform in children)
        {
            // Exclude the parent itself from the search
            if (childTransform.gameObject == parent)
            {
                continue;
            }

            if (childTransform.name.Contains(namePart))
            {
                return childTransform.gameObject;
            }
        }
        return null; // No child found
    }
}
