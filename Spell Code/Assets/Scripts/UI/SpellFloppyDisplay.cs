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
        new Vector2(-360, 250),
        new Vector2(360, 250), 
        new Vector2(-360, -250), 
        new Vector2(360, -250) };

    public GameObject canvasObject;

    [HideInInspector]
    public SpellData spellData;

    private Canvas displayCanvas;


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
        SetCanvasEnabled(true);
        if (SpellGifPlayer != null)
        {
            SpellGifPlayer.Reset();
        }
        SetDescriptionVisible(false, false);
    }

    public void StopFloppyDisplay()
    {
        Canvas canvas = GetDisplayCanvas();
        if (canvas == null || !canvas.enabled)
        {
            return;
        }

        KillDisplayTweens();
        canvas.enabled = false;
    }

    public bool IsDisplayCanvasEnabled()
    {
        Canvas canvas = GetDisplayCanvas();
        return canvas != null && canvas.enabled;
    }

    public void FloppyDisplayUpdate()
    {
        KillDisplayTweens();

        if (showDesc)
        {
            if (SpellGifGO != null)
            {
                Tween tween = SpellGifGO.transform.DOScale(gifScaleDesc, .25f).SetLink(SpellGifGO);
                tween.OnComplete(() =>
                {
                    if (spellDesc != null)
                    {
                        spellDesc.DOColor(Color.black, .25f).SetLink(spellDesc.gameObject);
                    }
                });
            }
        }
        else
        {
            if (spellDesc != null)
            {
                Tween tween = spellDesc.DOColor(Color.clear, .25f).SetLink(spellDesc.gameObject);
                tween.OnComplete(() =>
                {
                    if (SpellGifGO != null)
                    {
                        SpellGifGO.transform.DOScale(gifScaleNoDesc, .25f).SetLink(SpellGifGO);
                    }
                });
            }
        }
    }

    public void SetDescriptionVisible(bool visible, bool animate)
    {
        showDesc = visible;

        if (animate)
        {
            FloppyDisplayUpdate();
            return;
        }

        KillDisplayTweens();

        if (SpellGifGO != null)
        {
            SpellGifGO.transform.localScale = new Vector3(showDesc ? gifScaleDesc : gifScaleNoDesc, showDesc ? gifScaleDesc : gifScaleNoDesc, 1);
        }

        if (spellDesc != null)
        {
            spellDesc.color = showDesc ? Color.black : Color.clear;
        }
    }

    public void SetFloppyDisplayPosition(int index)
    {
        Background.rectTransform.anchoredPosition = displayLocations[index];
    }

    private void OnDisable()
    {
        KillDisplayTweens();
    }

    private void OnDestroy()
    {
        KillDisplayTweens();
    }

    private void SetCanvasEnabled(bool enabled)
    {
        Canvas canvas = GetDisplayCanvas();
        if (canvas != null)
        {
            canvas.enabled = enabled;
        }
    }

    private Canvas GetDisplayCanvas()
    {
        if (displayCanvas == null && canvasObject != null)
        {
            displayCanvas = canvasObject.GetComponent<Canvas>();
        }

        return displayCanvas;
    }

    private void KillDisplayTweens()
    {
        if (SpellGifGO != null)
        {
            SpellGifGO.transform.DOKill();
        }

        if (spellDesc != null)
        {
            spellDesc.DOKill();
        }
    }
}
