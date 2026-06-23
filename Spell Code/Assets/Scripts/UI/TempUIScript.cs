using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using DG.Tweening;

public class TempUIScript : MonoBehaviour, ISelectHandler
{
    public TextMeshProUGUI[] playerRamVals;
    public GameManager gameManager;
    public Image[] playerStoreBar;
    public Image[] followPlayerHpBar;
    public Image[] followPlayerDamageBar;
    public Image[] playerGoldBar;
    public RectTransform[] SpellInputBorder;
    public TextMeshPro[] SpellInputs;
    public GameObject[] onPlayerUI;
    public GameObject[] emptyQuadrants;
    public Sprite[] spellOnCooldownIcon;
    public Sprite[] spellReadyIcon;
    public Sprite[] roundWinIcon;
    public Image[] flowStateVals;
    public Image[] flowStateDim;
    public TextMeshProUGUI[] stockStabilityVals;
    public Image[] stockStabilityIcons;
    public Image[] stockStabilityDim;
    public Image[] demonAuraVals;
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

    public GameObject textBoxUI;
    public Animator textBoxAnim;
    public GameObject[] announcer;

    public Transform[] ramIncreaseGlow;

    public bool transitionScreenDisplayed;
    public bool shopScreenDisplayed;

    public float textSpeed;
    private int i = 0;

    private int[] previousRamVals = new int[4];
    private int activeTransitionRequestId = 0;
    private Coroutine activeTypeCoroutine;
    private Coroutine activeReverseTypeCoroutine;

    public float baseScale = 0f;
    public float scalePerChar = 0.05f;
    public float maxScale = 2f;

    public GameObject _soloGamemodesMenuFirst;
    public GameObject soloGamemodesMenu;
    public bool soloGamemodesMenuOpened;

    public GameObject _tutorialPromptMenuFirst;
    public GameObject tutorialPromptMenu;
    public RectTransform tutorialPromptImage;
    public RectTransform welcomeSign;
    public RectTransform[] tutorialPrompButtons;
    public RectTransform tutorialPromptSelector;
    public TextMeshProUGUI tutorialPromptButtonText;
    public TextMeshProUGUI tutorialPromptButtonText2;
    public bool tutorialPromptMenuOpened;

    public Pause pause;

    private InputSystem_Actions input;

    public RectTransform highlightOverlay; // lives outside the Layout Group, e.g. sibling of the panel

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
    }

    void Start()
    {
        followPlayerHpBar = new Image[4];
        followPlayerDamageBar = new Image[4];
        playerStoreBar = new Image[4];
        SpellInputBorder = new RectTransform[4];
        SpellInputs = new TextMeshPro[4];
        onPlayerUI = new GameObject[4];
        damageBarDisplayFill = new float[] { 1f, 1f, 1f, 1f };
        gameManager = GameManager.Instance;

        previousRamVals = new int[4];
        for (int i = 0; i < gameManager.playerCount; i++)
            previousRamVals[i] = gameManager.players[i]?.roundRam ?? 0;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        input.Enable();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        input.Disable();
        StopDamageBarCoroutines();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        transitionScreenDisplayed = false;
        shopScreenDisplayed = false;

        if (scene.name == "Gameplay")
        {
            transitionScreenDisplayed = true;
            StartCoroutine(DisplayTransitionScreen(2.0f, "FIGHT!!!"));
        }
        else if (scene.name == "Shop")
        {
            shopScreenDisplayed = true;
            StartCoroutine(DisplayTransitionScreen(3.5f, "Equip new spells before entering the next round"));
        }
    }

    public void SetSoloMenuActive(bool setOpen)
    {
        if (setOpen)
        {
            soloGamemodesMenu.SetActive(true);
            soloGamemodesMenuOpened = true;
            EventSystem.current.SetSelectedGameObject(_soloGamemodesMenuFirst);
            Time.timeScale = 0f;
        }
        else
        {
            soloGamemodesMenuOpened = false;
            soloGamemodesMenu.SetActive(false);
            // pause._pauseMenuFirst.Select();
            Time.timeScale = 1f;
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        UpdateUIBarVals();

        Scene currentScene = SceneManager.GetActiveScene();

        if (currentScene.name == "MainMenu" && GameManager.Instance.players[0] != null && !transitionScreenDisplayed)
        {
            transitionScreenDisplayed = true;
            StartCoroutine(DisplayTransitionScreen(3.5f, "Pick your starter spell before beginning the match"));
        }

        if (soloGamemodesMenuOpened && input.UI.Back.WasPressedThisFrame() && !pause.paused)
        {
            SetSoloMenuActive(false);
            Time.timeScale = 1f;
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            tutorialPromptMenu.SetActive(true);
            Time.timeScale = 0f;
            tutorialPromptMenuOpened = true;
            StartCoroutine(pause.SelectFirst(_tutorialPromptMenuFirst));
            TutorialPromptAnimation(0f, new Vector2 (-212f, 62f), new Vector2 (916f, 344f), new Vector2(1432f, 408f));
        }
    }

    public void InvitePlayer()
    {
        CloseGamemodesMenuForOnlineInvite();

        SteamLobbyManager lobbyManager = SteamLobbyManager.Instance;
        if (lobbyManager == null)
        {
            Debug.LogError("[TempUIScript] Online option selected, but SteamLobbyManager was not found.");
            return;
        }

        if (!lobbyManager.OpenInviteOverlayOrHost())
        {
            Debug.LogWarning("[TempUIScript] Online invite request could not be started.");
        }
    }

    private void CloseGamemodesMenuForOnlineInvite()
    {
        if (pause != null)
        {
            pause.SaveSettings();
        }

        soloGamemodesMenuOpened = false;

        if (soloGamemodesMenu != null)
        {
            soloGamemodesMenu.SetActive(false);
        }

        Time.timeScale = 1f;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
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
                if (i < flowStateVals.Length && flowStateVals[i] != null) flowStateVals[i].enabled = false;
                if (i < flowStateDim.Length && flowStateDim[i] != null) flowStateDim[i].enabled = false;
                if (i < stockStabilityVals.Length && stockStabilityVals[i] != null) stockStabilityVals[i].enabled = false;
                if (i < stockStabilityIcons.Length && stockStabilityIcons[i] != null) stockStabilityIcons[i].enabled = false;
                if (i < stockStabilityDim.Length && stockStabilityDim[i] != null) stockStabilityDim[i].enabled = false;
                if (i < demonAuraVals.Length && demonAuraVals[i] != null) demonAuraVals[i].enabled = false;
                if (i < demonAuraDim.Length && demonAuraDim[i] != null) demonAuraDim[i].enabled = false;
                if (i < repsVals.Length && repsVals[i] != null) repsVals[i].enabled = false;
                if (i < repsIcons.Length && repsIcons[i] != null) repsIcons[i].enabled = false;
                if (i < repsDim.Length && repsDim[i] != null) repsDim[i].enabled = false;
                continue;
            }

            onPlayerUI[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "On-Player UI").gameObject;

            followPlayerHpBar[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "Health Bar").GetComponent<Image>();
            playerStoreBar[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "Store Bar").GetComponent<Image>();
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
            repsVals[i].enabled = false;
            repsIcons[i].enabled = false;

            flowStateDim[i].enabled = false;
            stockStabilityDim[i].enabled = false;
            demonAuraDim[i].enabled = false;
            repsDim[i].enabled = false;

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
            demonAuraVals[i].fillAmount = (float)GameManager.Instance.players[i].demonAura / PlayerController.maxDemonAura;

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

        textBoxAnim.SetInteger("Reverse", 1);

        foreach (var item in announcer)
        {
            item.transform.DOKill();
            item.transform.DOScale(0f, 1f).SetEase(Ease.InOutQuint);
        }

        if (screenText != null)
        {
            screenText.text = text;
            activeReverseTypeCoroutine = StartCoroutine(TypeLine(screenText, text, true, textSpeed));
            yield return activeReverseTypeCoroutine;
            activeReverseTypeCoroutine = null;
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        if (requestId != activeTransitionRequestId)
            yield break;

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
            mySequence.Append(tutorialPrompButtons[i].DOSizeDelta(new Vector2(buttonScale.x, buttonScale.y), 0.35f).SetEase(Ease.OutQuad).SetUpdate(true));
        }
        mySequence.AppendInterval(0.1f).SetUpdate(true);
        mySequence.Append(tutorialPromptSelector.DOSizeDelta(new Vector2(tutorialSelectorPos.x, tutorialSelectorPos.y), 0.35f).SetEase(Ease.OutQuad).SetUpdate(true));

        if (!tutorialPromptMenuOpened) mySequence.AppendCallback(() => tutorialPromptMenu.SetActive(false));
    }

    public void RemoveButtonText()
    {
        StartCoroutine(TypeLine(tutorialPromptButtonText, "", true, 0.03f));
        StartCoroutine(TypeLine(tutorialPromptButtonText2, "", true, 0.03f));
    }

    public void ExitTutorialPromptAnimation()
    {
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
