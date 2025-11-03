using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class TempSpellDisplay : MonoBehaviour
{

    public List<TextMeshProUGUI> spellSlots = new List<TextMeshProUGUI>();
    public bool invertAlign = false;
    public CodeList[] arrowLists;
    [SerializeField] private Sprite[] arrowsSprite = new Sprite[4];

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
        
        string codeStringWithSpaces;
        string codeString;

        for (int i = 0; i < spellSlots.Count; i++)
        {
            if (i < playerSpells.Count)
            {
                codeStringWithSpaces = PlayerController.ConvertCodeToString(playerSpells[i].spellInput);
                codeString = codeStringWithSpaces.Replace(" ", "");
                if (arrowLists != null && i < arrowLists.Length && arrowLists[i] != null && arrowLists[i].arrows != null)
                {
                    for (int j = 0; j < codeString.Length && j < arrowLists[i].arrows.Length; j++)
                    {
                        Color currentAlpha = arrowLists[i].arrows[j].color;

                        if (showInputs)
                        {
                            spellSlots[i].text = "";
                            currentAlpha.a = 255f;
                            arrowLists[i].arrows[j].color = currentAlpha;
                            if (codeString[j] == 'U')
                                arrowLists[i].arrows[j].sprite = arrowsSprite[0];
                            else if (codeString[j] == 'D')
                                arrowLists[i].arrows[j].sprite = arrowsSprite[1];
                            else if (codeString[j] == 'R')
                                arrowLists[i].arrows[j].sprite = arrowsSprite[2];
                            else if (codeString[j] == 'L')
                                arrowLists[i].arrows[j].sprite = arrowsSprite[3];
                        }
                        else 
                        {
                            currentAlpha.a = 0;
                            arrowLists[i].arrows[j].color = currentAlpha;
                            spellSlots[i].text = playerSpells[i].spellName;
                        }

                    }
                }

            }
            else
            {
                spellSlots[i].text = "Empty\n...";
            }
            spellSlots[i].alignment = invertAlign ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
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
                spellSlots[i].text = "Empty\n...";
            }
            spellSlots[i].alignment = invertAlign ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        }
    }

}
