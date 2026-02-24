using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;
using System.Linq; // (no longer needed, but leaving it won't break anything)

public class TempSpellDisplay : MonoBehaviour
{
    public TempUIScript uiScript;
    public List<TextMeshProUGUI> spellSlots = new List<TextMeshProUGUI>();
    public bool invertAlign = false;
    private bool spellListUpdated = false;
    private bool roundWinCounterUpdated = false;

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
    public float flashPulseDuration = 0.2f;
    public bool[] cooldownFlashAppeared;
    public bool[] cooldownFlashAnimationFinished;

    // ---- FIXES: coroutine guards + caching ----
    private Coroutine[] readyPulseRoutines;
    private Coroutine[] flashAppearRoutines;
    private Image[] cooldownFlashImages;
    private GameObject[] cooldownBarParents; // cached parent "CooldownBar" for each fill

    public void Start()
    {
        GameObject tempUI = FindParentByNameContains_NoAlloc(gameObject.transform, "TempUI");
        if (tempUI != null)
            uiScript = tempUI.GetComponent<TempUIScript>();

        cooldownFlashAppeared = new bool[cooldownFlashRect.Length];
        cooldownFlashAnimationFinished = new bool[cooldownFlashRect.Length];

        // coroutine handles sized to slot count
        readyPulseRoutines = new Coroutine[spellSlots.Count];
        flashAppearRoutines = new Coroutine[spellSlots.Count];

        // cache images on flash rects
        cooldownFlashImages = new Image[cooldownFlashRect.Length];
        for (int i = 0; i < cooldownFlashRect.Length; i++)
        {
            cooldownFlashImages[i] = cooldownFlashRect[i] != null ? cooldownFlashRect[i].GetComponent<Image>() : null;
        }

        // cache cooldown bar parents (avoids GetComponentsInParent + LINQ allocations)
        cooldownBarParents = new GameObject[cooldownFills.Count];
        for (int i = 0; i < cooldownFills.Count; i++)
        {
            cooldownBarParents[i] = cooldownFills[i] != null
                ? FindParentByNameContains_NoAlloc(cooldownFills[i].transform, "CooldownBar")
                : null;
        }
    }

    public void Update()
    {
        if (GameManager.Instance.roundOver && !roundWinCounterUpdated)
        {
            UpdateRoundWinCounter();
            roundWinCounterUpdated = true;
        }
        else if (!GameManager.Instance.roundOver)
        {
            roundWinCounterUpdated = false;
        }
    }

    public void UpdateRoundWinCounter()
    {
        if (uiScript == null || uiScript.roundWinIcon == null || uiScript.roundWinIcon.Length < 2)
            return;

        var player = GameManager.Instance.players[spellDisplayIndex];
        if (player == null)
            return;

        for (int j = 0; j < player.roundsWon && j < roundWinsIcons.Count; j++)
        {
            roundWinsIcons[j].color = new Color32(255, 255, 255, 255);
            roundWinsIcons[j].sprite = uiScript.roundWinIcon[1];
        }
    }

    public void UpdateSpellDisplay(int playerIndex, bool showInputs = false)
    {
        PlayerController player = GameManager.Instance.players[playerIndex];

        if (player.spellList.Count <= 0)
        {
            for (int i = 0; i < cooldownBars.Count; i++)
            {
                cooldownBars[i].SetActive(false);
                if (i < spellSlots.Count) spellSlots[i].text = "";
            }
            return;
        }

        for (int i = 0; i < player.spellList.Count && i < cooldownBars.Count; i++)
            cooldownBars[i].SetActive(true);

        var playerSpells = player.spellList;

        for (int i = 0; i < spellSlots.Count; i++)
        {
            GameObject parent = (cooldownBarParents != null && i < cooldownBarParents.Length) ? cooldownBarParents[i] : null;

            if (parent == null)
            {
                // if cache missed (e.g., dynamic UI), try once:
                if (i < cooldownFills.Count && cooldownFills[i] != null)
                {
                    parent = FindParentByNameContains_NoAlloc(cooldownFills[i].transform, "CooldownBar");
                    if (cooldownBarParents != null && i < cooldownBarParents.Length) cooldownBarParents[i] = parent;
                }

                if (parent == null)
                    continue;
            }

            if (i < playerSpells.Count)
            {
                var main = spellReadyEffect[i].main;
                parent.gameObject.SetActive(true);

                // handle cooldown fill color and particle effect color based on spell brand
                switch (playerSpells[i].brands[0])
                {
                    case Brand.VWave:
                        cooldownFills[i].color = new Color32(107, 255, 116, 255);
                        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(107, 255, 116, 255));
                        break;
                    case Brand.BigStox:
                        cooldownFills[i].color = new Color32(67, 122, 252, 255);
                        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(67, 122, 252, 255));
                        break;
                    case Brand.Killeez:
                        cooldownFills[i].color = new Color32(255, 207, 0, 255);
                        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(255, 207, 0, 255));
                        break;
                    case Brand.DemonX:
                        cooldownFills[i].color = new Color32(255, 62, 117, 255);
                        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(255, 62, 117, 255));
                        break;
                }

                spellRechargingIcons[i].sprite = playerSpells[i].notReadyIcon;
                spellReadyIcons[i].sprite = playerSpells[i].readyIcon;

                if (playerSpells[i].spellType == SpellType.Active)
                    spellSlots[i].text = PlayerController.ConvertCodeToString(playerSpells[i].spellInput);
                else
                    spellSlots[i].text = playerSpells[i].spellName;
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
        cooldownFlashAppeared[i] = false;                 // allow re-appear next time we enter ready state
        cooldownFlashAnimationFinished[i] = true;

        // clear coroutine handle
        if (flashAppearRoutines != null && i < flashAppearRoutines.Length)
            flashAppearRoutines[i] = null;
    }

    public IEnumerator CoolDownReadyPulse(int i)
    {
        // cache reference
        Image img = (cooldownFlashImages != null && i < cooldownFlashImages.Length) ? cooldownFlashImages[i] : null;
        if (img == null)
        {
            // fallback if not cached
            img = cooldownFlashRect[i].GetComponent<Image>();
            if (cooldownFlashImages != null && i < cooldownFlashImages.Length) cooldownFlashImages[i] = img;
        }

        while (cooldownFills[i].fillAmount >= 1f)
        {
            // Fade out
            float elapsed = 0f;
            Color c = img.color;

            while (elapsed < flashPulseDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0.5f, 0.1f, elapsed / flashPulseDuration);
                img.color = c;
                yield return null;
            }

            // Fade in
            elapsed = 0f;
            while (elapsed < flashPulseDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0.1f, 0.5f, elapsed / flashPulseDuration);
                img.color = c;
                yield return null;
            }
        }

        // Reset alpha when spell goes on cooldown again
        Color reset = img.color;
        reset.a = 1f;
        img.color = reset;

        // clear coroutine handle
        if (readyPulseRoutines != null && i < readyPulseRoutines.Length)
            readyPulseRoutines[i] = null;
    }

    public void UpdateCooldownDisplay(int playerIndex)
    {
        var playerSpells = GameManager.Instance.players[playerIndex].spellList;

        for (int i = 0; i < spellSlots.Count; i++)
        {
            if (i >= playerSpells.Count)
            {
                if (i < cooldownFills.Count) cooldownFills[i].fillAmount = 0f;

                // stop pulses/flash if any
                StopSlotCoroutines(i);
                continue;
            }

            // get fill amount first (so alpha logic uses updated state)
            float fill = (float)(playerSpells[i].cooldown - playerSpells[i].cooldownCounter) / (float)playerSpells[i].cooldown;
            cooldownFills[i].fillAmount = fill;
            cooldownFills[i].fillOrigin = invertAlign ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;

            // get alpha based on fill
            Color tempColor = cooldownFills[i].color;
            tempColor.a = fill >= 1f ? 1.0f : 0.2f;
            cooldownFills[i].color = tempColor;

            if (fill < 1f)
            {
                spellReadyIcons[i].enabled = false;
                if (spellReadyEffect[i].isPlaying) spellReadyEffect[i].Stop();

                cooldownFlashRect[i].gameObject.SetActive(false);
                cooldownFlashAnimationFinished[i] = false;
                cooldownFlashAppeared[i] = false;

                // stop pulse coroutines
                StopSlotCoroutines(i);
            }
            else // ready
            {
                spellReadyIcons[i].enabled = true;
                if (!spellReadyEffect[i].isPlaying) spellReadyEffect[i].Play();

                // Appear animation (start once)
                if (!cooldownFlashAppeared[i])
                {
                    cooldownFlashAppeared[i] = true;
                    if (flashAppearRoutines != null && i < flashAppearRoutines.Length && flashAppearRoutines[i] == null)
                        flashAppearRoutines[i] = StartCoroutine(CoolDownFlashAppear(i));
                }

                // Ready pulse (start once)
                if (cooldownFlashAnimationFinished[i])
                {
                    if (readyPulseRoutines != null && i < readyPulseRoutines.Length && readyPulseRoutines[i] == null)
                        readyPulseRoutines[i] = StartCoroutine(CoolDownReadyPulse(i));
                }
            }
        }
    }

    private void StopSlotCoroutines(int i)
    {
        if (readyPulseRoutines != null && i < readyPulseRoutines.Length && readyPulseRoutines[i] != null)
        {
            StopCoroutine(readyPulseRoutines[i]);
            readyPulseRoutines[i] = null;
        }

        if (flashAppearRoutines != null && i < flashAppearRoutines.Length && flashAppearRoutines[i] != null)
        {
            StopCoroutine(flashAppearRoutines[i]);
            flashAppearRoutines[i] = null;
        }
    }

    private void OnDisable()
    {
        // prevent coroutines from lingering if object is disabled/destroyed
        if (readyPulseRoutines != null)
        {
            for (int i = 0; i < readyPulseRoutines.Length; i++)
                if (readyPulseRoutines[i] != null) StopCoroutine(readyPulseRoutines[i]);
        }

        if (flashAppearRoutines != null)
        {
            for (int i = 0; i < flashAppearRoutines.Length; i++)
                if (flashAppearRoutines[i] != null) StopCoroutine(flashAppearRoutines[i]);
        }
    }

    
    GameObject FindParentByNameContains_NoAlloc(Transform childTransform, string nameToContain)
    {
        Transform t = childTransform;
        while (t != null)
        {
            if (t.name.Contains(nameToContain))
                return t.gameObject;

            t = t.parent;
        }
        return null;
    }

    //GameObject FindParentByNameContains(Transform childTransform, string nameToContain)
    //{
    //    return FindParentByNameContains_NoAlloc(childTransform, nameToContain);
    //}

    //public void OldUpdateSpellDisplay(int playerIndex)
    //{
    //    var playerSpells = GameManager.Instance.players[playerIndex].spellList;
    //    for (int i = 0; i < spellSlots.Count; i++)
    //    {
    //        if (i < playerSpells.Count)
    //            spellSlots[i].text = playerSpells[i].spellName + ":\n" + PlayerController.ConvertCodeToString(playerSpells[i].spellInput);
    //        else
    //            spellSlots[i].text = "";

    //        spellSlots[i].alignment = invertAlign ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
    //    }
    //}
}