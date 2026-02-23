using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class SpellFloppyDisplay : MonoBehaviour
{

    public TextMeshProUGUI spellDesc;
    public TextMeshProUGUI spellName;
    public TextMeshProUGUI spellCooldown;
    public TextMeshProUGUI spellInput;

    public Sprite[] backgroundImageReference = new Sprite[4];
    public Image spellIcon;
    public Image Background;
    public Image selectFill;
    [NonSerialized]
    public Vector2[] displayLocations = new Vector2[4] { 
        new Vector2(-480, 260),
        new Vector2(480, 260), 
        new Vector2(-480, -260), 
        new Vector2(480, -260) };

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
        switch (spellData.brands[0])
        {
            case Brand.Killeez:
                Background.sprite = backgroundImageReference[0];
                break;
            case Brand.VWave:
                Background.sprite = backgroundImageReference[1];
                break;
            case Brand.DemonX:
                Background.sprite = backgroundImageReference[2];
                break;
            case Brand.BigStox:
                Background.sprite = backgroundImageReference[3];
                break;

        }
    }

    public void SetFloppyDisplayPosition(int index)
    {
        Background.rectTransform.anchoredPosition = displayLocations[index];
    }
}
