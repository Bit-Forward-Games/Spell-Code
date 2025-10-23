using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class TempSpellDisplay : MonoBehaviour
{

    public List<TextMeshProUGUI> spellSlots = new List<TextMeshProUGUI>();
    public bool invertAlign = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateSpellDisplay(int playerIndex)
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
