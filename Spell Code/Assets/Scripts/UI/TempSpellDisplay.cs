using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
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
    public List<ParticleSystem> spellReadyEffect = new List<ParticleSystem>();
    public List<GameObject> cooldownBars = new List<GameObject>();

    public void Start()
    {
        uiScript = FindParentByNameContains(gameObject.transform, "TempUI").GetComponent<TempUIScript>();
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
                if (playerSpells[i].spellName == "AsuranBlades")
                {
                    cooldownFills[i].color = new Color32(255, 62, 117, 255);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color32(255, 62, 117, 255));
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[0];
                    spellReadyIcons[i].sprite = uiScript.spellReadyIcon[0];
                }
                else if (playerSpells[i].spellName == "AmonSlash")
                {
                    cooldownFills[i].color = new Color32(255, 62, 117, 255);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color32(255, 62, 117, 255));
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[1];
                    spellReadyIcons[i].sprite = uiScript.spellReadyIcon[1];
                }
                else if (playerSpells[i].spellName == "GiftOfPromethius")
                {
                    cooldownFills[i].color = new Color32(255, 207, 0, 255);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color32(255, 207, 0, 255));
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[2];
                    spellReadyIcons[i].sprite = uiScript.spellReadyIcon[2];
                }
                else if (playerSpells[i].spellName == "MightOfZeus")
                {
                    cooldownFills[i].color = new Color32(255, 207, 0, 255);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color32(255, 207, 0, 255));
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[3];
                    spellReadyIcons[i].sprite = uiScript.spellReadyIcon[3];
                }
                else if (playerSpells[i].spellName == "ReloadShot")
                {
                    cooldownFills[i].color = new Color32(107, 255, 116, 255);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color32(107, 255, 116, 255));
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[4];
                    spellReadyIcons[i].sprite = uiScript.spellReadyIcon[4];
                }
                else if (playerSpells[i].spellName == "SkillshotSlash")
                {
                    cooldownFills[i].color = new Color32(107, 255, 116, 255);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color32(107, 255, 116, 255));
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[5];
                    spellReadyIcons[i].sprite = uiScript.spellReadyIcon[5];
                }
                
                if (showInputs)
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

    public void UpdateCooldownDisplay(int playerIndex)
    {
        var playerSpells = GameManager.Instance.players[playerIndex].spellList;


        for (int i = 0; i < spellSlots.Count; i++)
        {
            if (i < playerSpells.Count)
            {
                cooldownFills[i].fillAmount = (float)(playerSpells[i].cooldown - playerSpells[i].cooldownCounter) / (float)playerSpells[i].cooldown;

                cooldownFills[i].fillOrigin = invertAlign ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;

                if (cooldownFills[i].fillAmount < 1)
                {
                    spellReadyIcons[i].enabled = false;
                    spellReadyEffect[i].Stop();
                }
                else if (cooldownFills[i].fillAmount >= 1)
                {
                    spellReadyIcons[i].enabled = true;
                    spellReadyEffect[i].Play();
                }
            }
            else
            {
                cooldownFills[i].fillAmount = 0f;
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
