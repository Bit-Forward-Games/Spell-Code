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
    private bool roundWinCounterUpdated = false;

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
        UpdateRoundWinCounter();
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
            UpdateRoundWinCounter();
            roundWinCounterUpdated = true;
        }
        else if (!GameManager.Instance.roundOver)
        {
            roundWinCounterUpdated = false;
        }

        // PINGA PONGA instead of coroutine for flash alpha pulse 
        float t = Mathf.PingPong(Time.time * flashPulseSpeed, 1f);
        uiScript.flashAlpha = Mathf.Lerp(flashAlphaMax, flashAlphaMin, t);

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == "End")
        {
            UpdateRoundWinCounter();
        }

        /*
        if (!isPulsing)
            StartCoroutine(CoolDownReadyPulse());
        */
    }

    public void UpdateRoundWinCounter()
    {
        if (uiScript == null || uiScript.roundWinIcon == null || uiScript.roundWinIcon.Length < 2)
        {
            return;
        }

        var player = GameManager.Instance.players[spellDisplayIndex];
        if (player == null)
        {
            return;
        }

        for (int j = 0; j < roundWinsIcons.Count; j++)
        {
            roundWinsIcons[j].color = new Color32(255, 255, 255, 60);
            roundWinsIcons[j].sprite = uiScript.roundWinIcon[0];
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == "End")
        {
            for (int j = 0; j < player.roundsWon && j < roundWinsIcons.Count; j++)
            {
                roundWinsIcons[j].color = new Color32(255, 255, 255, 255);
                roundWinsIcons[j].sprite = uiScript.roundWinIcon[1];
            }
            return; // Leave all icons in the reset state on the End screen
        }

        // Fill in won rounds
        for (int j = 0; j < player.roundsWon && j < roundWinsIcons.Count; j++)
        {
            roundWinsIcons[j].color = new Color32(255, 255, 255, 255);
            roundWinsIcons[j].sprite = uiScript.roundWinIcon[1];
        }

    }

    public void UpdateSpellDisplay(int playerIndex, bool showInputs = false)
    {
        if (IsRollbackFrame) return;

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

            //if (parent == null)
            //{
            //    // Deprecated - was doing GetComponentsInParent + LINQ every call.
            //    // parent = FindParentByNameContains(cooldownFills[i].transform, "CooldownBar");
            //    continue;
            //}

            if (i < playerSpells.Count)
            {
                var main = spellReadyEffect[i].main;
                parent.gameObject.SetActive(true);

                //handle cooldown fill color and particle effect color based on spell brand
                switch (playerSpells[i].brands[0])
                {
                    case Brand.VWave:
                        cooldownFills[i].color = new Color32(107, 255, 116, 255);
                        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(107, 255, 116, 255));
                        if (i < uiScript.flowStateVals.Length && uiScript.flowStateVals[i] != null)
                            uiScript.flowStateVals[i].enabled = true;
                        break;
                    case Brand.BigStox:
                        cooldownFills[i].color = new Color32(67, 122, 252, 255);
                        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(67, 122, 252, 255));
                        if (i < uiScript.stockStabilityVals.Length && uiScript.stockStabilityVals[i] != null)
                            uiScript.stockStabilityVals[i].enabled = true;
                        if (i < uiScript.stockStabilityIcons.Length && uiScript.stockStabilityIcons[i] != null)
                            uiScript.stockStabilityIcons[i].enabled = true;
                        break;
                    case Brand.DemonX:
                        cooldownFills[i].color = new Color32(255, 62, 117, 255);
                        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(255, 62, 117, 255));
                        if (i < uiScript.demonAuraVals.Length && uiScript.demonAuraVals[i] != null)
                            uiScript.demonAuraVals[i].enabled = true;
                        break;
                    case Brand.Killeez:
                        cooldownFills[i].color = new Color32(255, 207, 0, 255);
                        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(255, 207, 0, 255));
                        if (i < uiScript.repsVals.Length && uiScript.repsVals[i] != null)
                            uiScript.repsVals[i].enabled = true;
                        if (i < uiScript.repsIcons.Length && uiScript.repsIcons[i] != null)
                            uiScript.repsIcons[i].enabled = true;
                        break;
                }

                spellRechargingIcons[i].sprite = playerSpells[i].notReadyIcon;
                spellReadyIcons[i].sprite = playerSpells[i].readyIcon;

                if (playerSpells[i].spellType == SpellType.Active)
                {
                    spellSlots[i].text = PlayerController.ConvertCodeToString(playerSpells[i].spellInput);
                    spellSlots[i].fontSize =14;
                }
                else
                {
                    spellSlots[i].text = playerSpells[i].spellName;
                    spellSlots[i].fontSize = 8;
                }
            }
            else
            {
                parent.gameObject.SetActive(false);
                spellSlots[i].text = "";
            }

            spellSlots[i].alignment = invertAlign ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
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