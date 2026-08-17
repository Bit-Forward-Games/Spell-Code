using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
// using System.Linq; // Deprecated path used LINQ
public class TempSpellDisplay : MonoBehaviour
{
    public TempUIScript uiScript;
    public List<TextMeshProUGUI> spellSlots = new List<TextMeshProUGUI>();
    public bool invertAlign = false;
    //private bool spellListUpdated = false;
    public bool roundWinCounterUpdated = false;
    // roundsWon last painted onto the icons. -1 forces a draw on the first pass.
    private int lastDrawnRoundWins = -1;

    // Pulsing alpha (PingPong)
    [Header("Flash Alpha Pulse (PingPong)")]
    [SerializeField] private float flashAlphaMin = 0.1f;
    [SerializeField] private float flashAlphaMax = 0.5f;
    [SerializeField] private float flashPulseSpeed = 2.5f; // higher = faster pulse

    //public CodeList[] arrowLists;
    //[SerializeField] private Sprite[] arrowsSprite = new Sprite[4];
    public List<Image> cooldownFills = new List<Image>();
    public List<Image> spellRechargingIcons = new List<Image>();
    public List<Image> spellReadyIcons = new List<Image>();
    public List<Image> roundWinsIcons = new List<Image>();
    public TextMeshProUGUI roundWinTextImage;
    public List<ParticleSystem> spellReadyEffect = new List<ParticleSystem>();
    public List<GameObject> cooldownBars = new List<GameObject>();
    public int spellDisplayIndex;

    // Cooldown bar flash
    public RectTransform[] cooldownFlashRect;
    public Vector2 startSize = new Vector2(120, 30);
    public Vector2 minSize = new Vector2(101, 26);
    public float duration = 2f;
    public float flashPulseDuration = 0.2f; // Deprecated (coroutine-based pulse)
    public bool[] cooldownFlashAppeared;
    public bool[] cooldownFlashAnimationFinished;

    // Cached references 
    private GameObject[] cooldownBarParents; // cached things so we dont have to do GetComponentInParent + LINQ every update

    private bool IsRollbackFrame => GameManager.Instance != null
                              && GameManager.Instance.isOnlineMatchActive
                              && RollbackManager.Instance != null
                              && RollbackManager.Instance.isRollbackFrame;

    public void Start()
    {
        //its better to just assign this in inspector bcs find functions are doody other than find with tag
        //GameObject tempUI = FindParentByNameContains(gameObject.transform, "TempUI");
        //if (tempUI != null)
        //    uiScript = tempUI.GetComponent<TempUIScript>();

        cooldownFlashAppeared = new bool[cooldownFlashRect.Length];
        cooldownFlashAnimationFinished = new bool[cooldownFlashRect.Length];

        roundWinCounterUpdated = false; 

        // Cache the parent gameobjects once 
        CacheCooldownParents();
        UpdateRoundWinCounter(roundWinsIcons, uiScript.roundWinIcon, spellDisplayIndex);
    }

    private void CacheCooldownParents()
    {
        // schizo check: use the smaller of the two lists so we don't index out of range.
        int n = Mathf.Min(cooldownFills.Count, cooldownBars.Count);
        cooldownBarParents = new GameObject[n];

        for (int i = 0; i < n; i++)
        {
            cooldownBarParents[i] = cooldownBars[i];
        }
    }

    public void Update()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.roundOver && !roundWinCounterUpdated)
        {
            uiScript.transitionScreenDisplayed = false;
            UpdateRoundWinCounter(roundWinsIcons, uiScript.roundWinIcon, spellDisplayIndex);
            roundWinCounterUpdated = true;
        }
        else if (!GameManager.Instance.roundOver)
        {
            roundWinCounterUpdated = false;
        }

        // Outside the End screen the counter is only redrawn on the roundOver edge, and Start() runs
        // once for a UI that survives on the persistent TempUI -- so a rematch kept the previous
        // match's icons on screen until the first round finished. Redraw whenever this player's
        // total changes, which catches the reset back to 0 as well as a win being scored. Skipped
        // during rollback resim so a momentarily rolled-back total cannot flicker the icons; the
        // next confirmed frame still sees the difference and draws it.
        if (!IsRollbackFrame)
        {
            PlayerController[] roster = GameManager.Instance.players;
            if (roster != null && spellDisplayIndex >= 0 && spellDisplayIndex < roster.Length)
            {
                PlayerController displayPlayer = roster[spellDisplayIndex];
                int wins = displayPlayer != null ? displayPlayer.roundsWon : 0;
                if (wins != lastDrawnRoundWins)
                {
                    UpdateRoundWinCounter(roundWinsIcons, uiScript.roundWinIcon, spellDisplayIndex);
                }
            }
        }

        // PINGA PONGA instead of coroutine for flash alpha pulse 
        float t = Mathf.PingPong(Time.time * flashPulseSpeed, 1f);
        uiScript.flashAlpha = Mathf.Lerp(flashAlphaMax, flashAlphaMin, t);

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == "End")
        {
            UpdateRoundWinCounter(roundWinsIcons, uiScript.roundWinIcon, spellDisplayIndex);
        }

        /*
        if (!isPulsing)
            StartCoroutine(CoolDownReadyPulse());
        */
    }

    public void UpdateRoundWinCounter(List<Image> roundWinsIcons, Sprite[] roundWinIcon, int spellDisplayIndex)
    {
        if (uiScript == null || roundWinTextImage == null)
        {
            return;
        }
 
        var player = GameManager.Instance.players[spellDisplayIndex];
        if (player == null)
        {
            return;
        }

        // Only record once a draw is actually going ahead, so an early return above cannot mark a
        // total as drawn that never reached the pips. Without this the change detector in Update
        // never advances past -1, so it matches every frame and rebuilds the sprite string
        // continuously for every player.
        lastDrawnRoundWins = player.roundsWon;

        // Builds the 3-pip round win indicator out of WinCounterTextImage sprites:
        // the first wonCount pips are the "won" icon, the rest stay "not won".
        void BuildWinCounterText(int wonCount)
        {
            roundWinTextImage.text = "";
            for (int j = 0; j < 3; j++)
            {
                string iconName = j < wonCount ? "RoundWonIcon" : "RoundNotWonIcon";
                roundWinTextImage.text += "<sprite=\"WinCounterTextImage\" name=\"" + iconName + "\">";
            }
        }
 
        // Reset to the "not won" state first
        BuildWinCounterText(0);
 
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == "End")
        {
            BuildWinCounterText(player.roundsWon);
            return; // Leave all icons in the reset state on the End screen
        }
 
        // Fill in won rounds
        BuildWinCounterText(player.roundsWon);
    }

    public void UpdateSpellDisplay(int playerIndex)
    {
        if (IsRollbackFrame) return;
        // bool hasDemonX = false;
        // bool hasBigStox = false;
        // bool hasKilleez = false;
        // bool hasVWave = false;

        PlayerController player = GameManager.Instance.players[playerIndex];

        if (player.spellList.Count <= 0)
        {
            for (int i = 0; i < cooldownBars.Count; i++)
            {
                cooldownBars[i].SetActive(false);
                spellSlots[i].text = "";
            }
            return;
        }

        for (int i = 0; i < player.spellList.Count; i++)
        {
            cooldownBars[i].SetActive(true);
        }

        var playerSpells = GameManager.Instance.players[playerIndex].spellList;

        for (int i = 0; i < spellSlots.Count; i++)
        {
            // Fix #1: avoid hierarchy search + LINQ allocs every update; use cached refs instead.
            GameObject parent = (cooldownBarParents != null && i < cooldownBarParents.Length) ? cooldownBarParents[i] : null;


            if (i < playerSpells.Count)
            {
                var spellReadyParticles = spellReadyEffect[i].main;
                parent.gameObject.SetActive(true);

                //handle cooldown fill color and particle effect color based on spell brand
                switch (playerSpells[i].brands[0])
                {
                    case Brand.VWave:
                        cooldownFills[i].color = GameManager.colors["green"];
                        spellReadyParticles.startColor = new ParticleSystem.MinMaxGradient(GameManager.colors["green"]);
                        break;
                    case Brand.BigStox:
                        cooldownFills[i].color = GameManager.colors["blue"];
                        spellReadyParticles.startColor = new ParticleSystem.MinMaxGradient(GameManager.colors["blue"]);
                        break;
                    case Brand.DemonX:
                        cooldownFills[i].color = GameManager.colors["red"];
                        spellReadyParticles.startColor = new ParticleSystem.MinMaxGradient(GameManager.colors["red"]);
                        break;
                    case Brand.Killeez:
                        cooldownFills[i].color = GameManager.colors["yellow"];
                        spellReadyParticles.startColor = new ParticleSystem.MinMaxGradient(GameManager.colors["yellow"]);
                        break;
                    case Brand.DarkWeb:
                        cooldownFills[i].color = GameManager.colors["white"];
                        spellReadyParticles.startColor = new ParticleSystem.MinMaxGradient(GameManager.colors["evil color"]);
                        break;
                }

                spellRechargingIcons[i].sprite = playerSpells[i].notReadyIcon;
                spellReadyIcons[i].sprite = playerSpells[i].readyIcon;

                if (playerSpells[i].spellType == SpellType.Active)
                {
                    if (player.vibeCoding)
                    {
                        uint codeToMatch;
                        switch (i)
                        {
                            case 0://up
                                codeToMatch = 0b_0000_0000_0000_0000_0000_0011_0000_0001;
                                break;
                            case 1://right
                                codeToMatch = 0b_0000_0000_0000_0000_0000_0001_0000_0001;
                                break;
                            case 2://down
                                codeToMatch = 0b_0000_0000_0000_0000_0000_0000_0000_0001;
                                break;
                            case 3://left
                                codeToMatch = 0b_0000_0000_0000_0000_0000_0010_0000_0001;
                                break;
                            default:
                                codeToMatch = 0;
                                break;
                        }
                        spellSlots[i].text = PlayerController.ConvertCodeToString(codeToMatch, 
                        null, 
                        player.relativeInputs ? 
                        player.facingRight : 
                        true);
                        //spellSlots[i].fontSize =14;
                    }
                    else
                    {
                        spellSlots[i].text = PlayerController.ConvertCodeToString(playerSpells[i].spellInput, 
                        null, 
                        player.relativeInputs ? 
                        player.facingRight : 
                        true);
                        //spellSlots[i].fontSize =14;
                    }
                    
                }
                else
                {
                    spellSlots[i].text = playerSpells[i].spellName;
                    //spellSlots[i].fontSize = 7;
                }
            }
            else
            {
                parent.gameObject.SetActive(false);
                spellSlots[i].text = "";
            }

            spellSlots[i].alignment = invertAlign ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        }

        //Resource Icons
        // PlayerController targetPlayer =GameManager.Instance.players[playerIndex];

      
        // uiScript.demonAuraVals[playerIndex].enabled = targetPlayer.demonAura != 0 || hasDemonX;
        // uiScript.demonAuraIcons[playerIndex].enabled = targetPlayer.demonAura != 0 || hasDemonX;
        
        // uiScript.stockStabilityVals[playerIndex].enabled = targetPlayer.stockStabilityModified != 0 || hasBigStox;
        // uiScript.stockStabilityIcons[playerIndex].enabled = targetPlayer.stockStabilityModified != 0 || hasBigStox;
    
        // uiScript.repsVals[playerIndex].enabled = targetPlayer.reps != 0 || hasKilleez;
        // uiScript.repsIcons[playerIndex].enabled = targetPlayer.reps != 0 || hasKilleez;
    
        // uiScript.flowStateVals[playerIndex].enabled = targetPlayer.flowState != 0 || hasVWave;
        
        
    }

    /// <summary>
    /// Blanks this quadrant's spell display when its player disconnects mid-match:
    /// hides the chosen-spell slots, cooldown bars, ready/recharge icons and round-win pips.
    /// </summary>
    public void ClearForDisconnect()
    {
        for (int i = 0; i < spellSlots.Count; i++)
        {
            if (spellSlots[i] != null) spellSlots[i].text = "";
        }
        for (int i = 0; i < cooldownBars.Count; i++)
        {
            if (cooldownBars[i] != null) cooldownBars[i].SetActive(false);
        }
        for (int i = 0; i < cooldownFills.Count; i++)
        {
            if (cooldownFills[i] != null) cooldownFills[i].fillAmount = 0f;
        }
        for (int i = 0; i < spellReadyIcons.Count; i++)
        {
            if (spellReadyIcons[i] != null) spellReadyIcons[i].enabled = false;
        }
        for (int i = 0; i < spellRechargingIcons.Count; i++)
        {
            if (spellRechargingIcons[i] != null) spellRechargingIcons[i].enabled = false;
        }
        for (int i = 0; i < spellReadyEffect.Count; i++)
        {
            if (spellReadyEffect[i] != null) spellReadyEffect[i].Stop();
        }
        for (int i = 0; i < roundWinsIcons.Count; i++)
        {
            if (roundWinsIcons[i] != null) roundWinsIcons[i].enabled = false;
        }
    }

    public IEnumerator CoolDownFlashAppear(int i)
    {
        float elapsed = 0f;
        cooldownFlashRect[i].gameObject.SetActive(true);
        cooldownFlashRect[i].sizeDelta = startSize;

        if (!cooldownFlashAnimationFinished[i])
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                cooldownFlashRect[i].sizeDelta = Vector2.Lerp(startSize, minSize, t);
                yield return null;
            }
        }

        cooldownFlashRect[i].sizeDelta = minSize;
        cooldownFlashAppeared[i] = false;
        cooldownFlashAnimationFinished[i] = true;
    }

    public void UpdateCooldownDisplay(int playerIndex)
    {
        if (IsRollbackFrame) return;

        var playerSpells = GameManager.Instance.players[playerIndex].spellList;

        for (int i = 0; i < spellSlots.Count; i++)
        {
            if (i >= playerSpells.Count)
            {
                cooldownFills[i].fillAmount = 0f;
                continue;
            }

            Color tempColor = cooldownFills[i].color;
            tempColor.a = cooldownFills[i].fillAmount >= 1f ? 1.0f : 0.2f;
            cooldownFills[i].color = tempColor;

            cooldownFills[i].fillAmount = (float)(playerSpells[i].cooldown - playerSpells[i].cooldownCounter) / (float)playerSpells[i].cooldown;
            cooldownFills[i].fillOrigin = invertAlign ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;

            if (cooldownFills[i].fillAmount < 1)
            {
                spellReadyIcons[i].enabled = false;
                spellReadyEffect[i].Stop();
                cooldownFlashRect[i].gameObject.SetActive(false);
                cooldownFlashAnimationFinished[i] = false;
            }
            else if (cooldownFills[i].fillAmount >= 1)
            {
                spellReadyIcons[i].enabled = true;
                spellReadyEffect[i].Play();

                if (!cooldownFlashAppeared[i])
                {
                    cooldownFlashAppeared[i] = true;
                    StartCoroutine(CoolDownFlashAppear(i));
                }
                if (cooldownFlashAnimationFinished[i])
                {
                    Color c = cooldownFlashRect[i].GetComponent<Image>().color;
                    c.a = uiScript.flashAlpha;
                    cooldownFlashRect[i].GetComponent<Image>().color = c;
                }
            }
        }
    }

    // Old: alloc-heavy hierarchy search + LINQ (kept commented for reference)
    //GameObject FindParentByNameContains(Transform childTransform, string nameToContain)
    //{
    //    return childTransform.GetComponentsInParent<Transform>()
    //        .FirstOrDefault(t => t.name.Contains(nameToContain))?.gameObject;
    //}

    //public void OldUpdateSpellDisplay(int playerIndex)
    //{
    //    var playerSpells = GameManager.Instance.players[playerIndex].spellList;
    //    for (int i = 0; i < spellSlots.Count; i++)
    //    {
    //        if (i < playerSpells.Count)
    //        {
    //            spellSlots[i].text = playerSpells[i].spellName + ":\n" + PlayerController.ConvertCodeToString(playerSpells[i].spellInput);
    //        }
    //        else
    //        {
    //            spellSlots[i].text = "";
    //        }
    //        spellSlots[i].alignment = invertAlign ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
    //    }
    //}
}