using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SpellFloppyDisplay : MonoBehaviour
{

    public TextMeshProUGUI spellDesc;
    public TextMeshProUGUI spellName;
    public TextMeshProUGUI spellCooldown;
    public TextMeshProUGUI spellInput;
    public Image spellIcon;
    public Image Background;
    [HideInInspector]
    public SpellData spellData;


    public void SetSpellFloppyDisplay()
    {
        spellData = gameObject.GetComponentInParent<SpellData>();
        spellName.text = spellData.name;
        spellDesc.text = spellData.description;
        spellCooldown.text = $"Cooldown: {spellData.cooldown} seconds";
        spellInput.text = $"Input: {PlayerController.ConvertCodeToString(spellData.spellInput)}";
        spellIcon.sprite = spellData.readyIcon;
    }
}
