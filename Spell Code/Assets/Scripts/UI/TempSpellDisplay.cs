using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class TempSpellDisplay : MonoBehaviour
{
    public TempUIScript uiScript;
    public List<TextMeshProUGUI> spellSlots = new List<TextMeshProUGUI>();
    public bool invertAlign = false;
    private bool spellListUpdated = false;
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
    public float flashPulseDuration = 0.2f;
    public bool[] cooldownFlashAppeared;
    public bool[] cooldownFlashAnimationFinished;

    public void Start()
    {
        uiScript = FindParentByNameContains(gameObject.transform, "TempUI").GetComponent<TempUIScript>();
        cooldownFlashAppeared = new bool[cooldownFlashRect.Length];
        cooldownFlashAnimationFinished = new bool[cooldownFlashRect.Length];
    }

    public void Update()
    {
        if (GameManager.Instance.roundOver)
            UpdateRoundWinCounter();
    }

    public void UpdateRoundWinCounter()
    {
        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            for (int j = 0; j < GameManager.Instance.players[spellDisplayIndex].roundsWon; j++)
            {
                roundWinsIcons[j].color = new Color32(255, 255, 255, 255);
                roundWinsIcons[j].sprite = uiScript.roundWinIcon[1]; // ← assuming [1] = won
            }
        }
    }

    public void UpdateSpellDisplay(int playerIndex, bool showInputs = false)
    {   
        PlayerController player = GameManager.Instance.players[playerIndex];

        if(player.spellList.Count <= 0)
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
            
            GameObject parent = FindParentByNameContains(cooldownFills[i].transform, "CooldownBar");

            if (parent == null)
            {
                // cooldownBars[i].SetActive(true);
                continue;
            }
            
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
                {
                    spellSlots[i].text = PlayerController.ConvertCodeToString(playerSpells[i].spellInput);
                }
                else
                {
                    spellSlots[i].text = playerSpells[i].spellName;
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

    public IEnumerator CoolDownReadyPulse(int i)
    {
        while (cooldownFills[i].fillAmount >= 1f)
        {
            // Fade out
            float elapsed = 0f;
            Color c = cooldownFlashRect[i].GetComponent<Image>().color;

            while (elapsed < flashPulseDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0.5f, 0.1f, elapsed / flashPulseDuration);
                cooldownFlashRect[i].GetComponent<Image>().color = c;
                yield return null;
            }

            // Fade in
            elapsed = 0f;
            while (elapsed < flashPulseDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0.1f, 0.5f, elapsed / flashPulseDuration);
                cooldownFlashRect[i].GetComponent<Image>().color = c;
                yield return null;
            }
        }

        // Reset alpha when spell goes on cooldown again
        Color reset = cooldownFlashRect[i].GetComponent<Image>().color;
        reset.a = 1f;
        cooldownFlashRect[i].GetComponent<Image>().color = reset;
    }

    public void UpdateCooldownDisplay(int playerIndex)
    {
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
                    StartCoroutine(CoolDownReadyPulse(i));
                }
            }
        }
    }

    GameObject FindParentByNameContains(Transform childTransform, string nameToContain)
    {
        return childTransform.GetComponentsInParent<Transform>()
            .FirstOrDefault(t => t.name.Contains(nameToContain))?.gameObject;
    }

    public void OldUpdateSpellDisplay(int playerIndex)
    {
        var playerSpells = GameManager.Instance.players[playerIndex].spellList;
        for (int i = 0; i < spellSlots.Count; i++)
        {
            if (i < playerSpells.Count)
            {
                spellSlots[i].text = playerSpells[i].spellName + ":\n" + PlayerController.ConvertCodeToString(playerSpells[i].spellInput);
            }
            else
            {
                spellSlots[i].text = "";
            }
            spellSlots[i].alignment = invertAlign ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        }
    }

}
