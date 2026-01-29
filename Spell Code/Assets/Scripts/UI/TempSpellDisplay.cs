using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;

public class TempSpellDisplay : MonoBehaviour
{

    public List<TextMeshProUGUI> spellSlots = new List<TextMeshProUGUI>();
    public bool invertAlign = false;
    //public CodeList[] arrowLists;
    //[SerializeField] private Sprite[] arrowsSprite = new Sprite[4];
    public List<Image> cooldownFills = new List<Image>();

    public void Start()
    {
    }

    public void UpdateSpellDisplay(int playerIndex, bool showInputs = false)
    {   
        var playerSpells = GameManager.Instance.players[playerIndex].spellList;
        

        for (int i = 0; i < spellSlots.Count; i++)
        {
            GameObject parent = FindParentByNameContains(cooldownFills[i].transform, "CooldownBar");

            if (parent == null)
            {
                Debug.LogWarning($"CooldownBar parent not found for cooldownFills[{i}] ({cooldownFills[i].name})");
                continue; // Skip this iteration
            }
            
            if (i < playerSpells.Count)
            {
                parent.gameObject.SetActive(true);
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
