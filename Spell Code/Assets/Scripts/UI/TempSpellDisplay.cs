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
    //public CodeList[] arrowLists;
    //[SerializeField] private Sprite[] arrowsSprite = new Sprite[4];
    public List<Image> cooldownFills = new List<Image>();
    public List<Image> spellRechargingIcons = new List<Image>();
    public List<GameObject> cooldownBars = new List<GameObject>();

    public void Start()
    {
        uiScript = FindParentByNameContains(gameObject.transform, "TempUI").GetComponent<TempUIScript>();
    }

    public void UpdateSpellDisplay(int playerIndex, bool showInputs = false)
    {   
        PlayerController player = GameManager.Instance.players[playerIndex];

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
                parent.gameObject.SetActive(true);
                if (playerSpells[i].spellName == "AsuranBlades")
                {
                    cooldownFills[i].color = new Color32(255, 62, 117, 255);
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[0];
                }
                else if (playerSpells[i].spellName == "AmonSlash")
                {
                    cooldownFills[i].color = new Color32(255, 62, 117, 255);
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[1];
                }
                else if (playerSpells[i].spellName == "GiftOfPrometheus")
                {
                    cooldownFills[i].color = new Color32(255, 207, 0, 255);
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[2];
                }
                else if (playerSpells[i].spellName == "MightOfZeus")
                {
                    cooldownFills[i].color = new Color32(255, 207, 0, 255);
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[3];
                }
                else if (playerSpells[i].spellName == "ReloadShot")
                {
                    cooldownFills[i].color = new Color32(107, 255, 116, 255);
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[4];
                }
                else if (playerSpells[i].spellName == "SkillshotSlash")
                {
                    cooldownFills[i].color = new Color32(107, 255, 116, 255);
                    spellRechargingIcons[i].sprite = uiScript.spellOnCooldownIcon[5];
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
