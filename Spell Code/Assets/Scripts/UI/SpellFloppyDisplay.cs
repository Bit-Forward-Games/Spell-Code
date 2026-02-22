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
    public Image selectFill;

    public GameObject canvasObject;

    [HideInInspector]
    public SpellData spellData;


    public void SetSpellFloppyDisplay( string spellString)
    {
        spellData = SpellDictionary.Instance.spellDict[spellString];
        spellName.text = spellData.spellName;
        spellDesc.text = spellData.description;
        spellCooldown.text = $"Cooldown: {spellData.cooldown/60f}s";
        spellInput.text = $"Input: {PlayerController.ConvertCodeToString(spellData.spellInput)}";
        spellIcon.sprite = spellData.readyIcon;
    }
}
