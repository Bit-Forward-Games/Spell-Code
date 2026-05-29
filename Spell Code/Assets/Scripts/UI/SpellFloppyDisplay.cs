using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using GifImporter;
using DG.Tweening;

public class SpellFloppyDisplay : MonoBehaviour
{

    public TextMeshProUGUI spellDesc;
    public TextMeshProUGUI spellName;
    public TextMeshProUGUI spellExe;
    public TextMeshProUGUI spellCooldown;
    public TextMeshProUGUI spellInput;

    public Sprite[] backgroundImageReference = new Sprite[4];
    public Image spellIcon;
    public Image Background;
    public Image selectFill;
    public GifPlayer SpellGifPlayer;
    public GameObject SpellGifGO;


//fields for animated interactivity
    public bool showDesc = false;
    public const float gifScaleNoDesc = 2.75f;
    public const float gifScaleDesc = 2f;



    [NonSerialized]
    public Vector2[] displayLocations = new Vector2[4] { 
        new Vector2(-400, 260),
        new Vector2(400, 260), 
        new Vector2(-400, -260), 
        new Vector2(400, -260) };

    public GameObject canvasObject;

    [HideInInspector]
    public SpellData spellData;


    public void SetSpellFloppyDisplay( string spellString)
    {
        spellData = SpellDictionary.Instance.spellDict[spellString];
        spellName.text = spellData.spellName;
        spellExe.text = spellData.spellName.Replace(" ", "_") + ".exe";
        spellDesc.text = spellData.description;
        int displayCooldown = (int)(spellData.cooldown > 59 ? spellData.cooldown / 60f : 0f);
        spellCooldown.text = $"Cooldown: {displayCooldown}s";
        spellInput.text = spellData.spellType == SpellType.Active? $"Input: {PlayerController.ConvertCodeToString(spellData.spellInput)}": "Passive";
        spellIcon.sprite = spellData.readyIcon;
        SpellGifPlayer.Gif = spellData.SpellGIF;;
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

    public void StartFloppyDisplay()
    {
        canvasObject.GetComponent<Canvas>().enabled = true;
        SpellGifPlayer.Reset();
        showDesc = false;
        SpellGifGO.transform.localScale = new Vector3(gifScaleNoDesc, gifScaleNoDesc, 1);
        spellDesc.color = Color.clear;
    }

    public void FloppyDisplayUpdate()
    {
        if (showDesc)
        {
            if (SpellGifGO != null)
            {
                Tween tween = SpellGifGO.transform.DOScale(gifScaleDesc, .25f);
                tween.OnComplete(() =>
                {
                    spellDesc.DOColor(Color.black, .25f);
                });
            }
        }
        else
        {
            if (SpellGifGO != null)
            {
                Tween tween = spellDesc.DOColor(Color.clear, .25f);
                tween.OnComplete(() =>
                {
                    SpellGifGO.transform.DOScale(gifScaleNoDesc, .25f);
                });
            }
        }
    }

    public void SetFloppyDisplayPosition(int index)
    {
        Background.rectTransform.anchoredPosition = displayLocations[index];
    }
}
