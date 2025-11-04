using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class TempSpellDisplay : MonoBehaviour
{

    public List<TextMeshProUGUI> spellSlots = new List<TextMeshProUGUI>();
    public bool invertAlign = false;
    //public CodeList[] arrowLists;
    //[SerializeField] private Sprite[] arrowsSprite = new Sprite[4];
    public List<Image> cooldownFills = new List<Image>();

    public void Start()
    {
        // arrowsSprite[0] = Resources.Load<Sprite>("Arrows/Up_Arrow");
        // arrowsSprite[1] = Resources.Load<Sprite>("Arrows/Down_Arrow");
        // arrowsSprite[2] = Resources.Load<Sprite>("Arrows/Right_Arrow");
        // arrowsSprite[3] = Resources.Load<Sprite>("Arrows/Left_Arrow");
        // arrowsSprite[3] = Resources.Load<Sprite>("Sprites/Test sprites/UI Elements/Player Panel/Arrows/Left_Arrow");

    }

    public void UpdateSpellDisplay(int playerIndex, bool showInputs = false)
    {   
        var playerSpells = GameManager.Instance.players[playerIndex].spellList;
        

        for (int i = 0; i < spellSlots.Count; i++)
        {
            if (i < playerSpells.Count)
            {
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
                spellSlots[i].text = "Empty";
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
                spellSlots[i].text = "Empty";
            }
            spellSlots[i].alignment = invertAlign ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        }
    }

}
