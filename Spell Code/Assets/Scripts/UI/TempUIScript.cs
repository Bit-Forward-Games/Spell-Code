using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class TempUIScript : MonoBehaviour
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
            previousRamVals[i] = gameManager.players[i]?.totalRam ?? 0;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
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
    }

    public void UpdateUIBarVals()
    {
        Scene currentScene = SceneManager.GetActiveScene();

        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            onPlayerUI[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "On-Player UI").gameObject;
            // if (currentScene.name == "MainMenu" || currentScene.name == "Shop")
            // {
            //     onPlayerUI[i].SetActive(false);
            // }
            // else
            //     onPlayerUI[i].SetActive(true);

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

            int _ramIncrease = GameManager.Instance.players[i].totalRam;

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

            if (GameManager.Instance.players[i].isHit)
            {
                if (damageBarCoroutines[i] != null) StopCoroutine(damageBarCoroutines[i]);
                damageBarCoroutines[i] = StartCoroutine(DamageBar(i));
            }

            float fillAmountVal = GameManager.Instance.players[i].charData != null? ((float)GameManager.Instance.players[i].currentPlayerHealth / GameManager.Instance.players[i].charData.playerHealth) : 0;
            float fillGoldAmountVal = GameManager.Instance.players[i].charData != null? ((float)GameManager.Instance.players[i].roundRam / GameManager.Instance.ramNeededToWinRound) : 0;
            followPlayerHpBar[i].fillAmount = fillAmountVal;
            playerRamVals[i].text = (GameManager.Instance.ramNeededToWinRound - GameManager.Instance.players[i].roundRam < 100)?"MATCH POINT!":$"{GameManager.Instance.players[i].roundRam}";
            playerGoldBar[i].fillAmount = (GameManager.Instance.ramNeededToWinRound - GameManager.Instance.players[i].roundRam < 100)?1:fillGoldAmountVal;

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
            flowStateVals[i].fillAmount = (float)GameManager.Instance.players[i].flowState / PlayerController.maxFlowState;

            // stockStabilityVals[i].enabled = true;
            // stockStabilityIcons[i].enabled = true;
            stockStabilityVals[i].text = GameManager.Instance.players[i].stockStability.ToString() + "%";

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
        // Transform childTransform = GameManager.Instance.players[playerIndex].transform.Find("Damage Bar");
        followPlayerDamageBar[playerIndex] = FindChildContainingName(GameManager.Instance.players[playerIndex].gameObject, "Damage Bar").GetComponent<Image>();
        PlayerController player = GameManager.Instance.players[playerIndex];

        player.isHit = false;
        
        float previousHealthAmount = damageBarDisplayFill[playerIndex];
        
        float newHealthAmount = (float)player.currentPlayerHealth / player.charData.playerHealth;
        
        followPlayerDamageBar[playerIndex].fillAmount = previousHealthAmount;

        yield return new WaitForSeconds(1f);

        float elapsedTime = 0f;
        float animationDuration = 1f;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            followPlayerDamageBar[playerIndex].fillAmount = Mathf.Lerp(previousHealthAmount, newHealthAmount, t);
            yield return null;
        }

        followPlayerDamageBar[playerIndex].fillAmount = newHealthAmount;
        damageBarDisplayFill[playerIndex] = newHealthAmount;
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
            activeTypeCoroutine = StartCoroutine(TypeLine(screenText, text, false));
        }
        
        yield return new WaitForSeconds(transitionTime);

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
            activeReverseTypeCoroutine = StartCoroutine(TypeLine(screenText, text, true));
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

    IEnumerator TypeLine(TextMeshProUGUI screenText, string text, bool reverse)
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
