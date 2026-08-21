using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using DG.Tweening;

public class SpellFloppyDisplay_Character : MonoBehaviour
{
    [Header("Character")]
    public TextMeshProUGUI charName;
    public TextMeshProUGUI charExe;

    [Header("Spell_1")]
    public TextMeshProUGUI spellDesc_1;
    public TextMeshProUGUI spellName_1;
    //public TextMeshProUGUI spellCooldown_1;
    //public TextMeshProUGUI spellInput_1;

    [Header("Spell_2")]
    public TextMeshProUGUI spellDesc_2;
    public TextMeshProUGUI spellName_2;
    //public TextMeshProUGUI spellCooldown_2;
    //public TextMeshProUGUI spellInput_2;

    [Header("Spell_3")]
    public TextMeshProUGUI spellDesc_3;
    public TextMeshProUGUI spellName_3;
    //public TextMeshProUGUI spellCooldown_3;
    //public TextMeshProUGUI spellInput_3;

    [Header("Spell_4")]
    public TextMeshProUGUI spellDesc_4;
    public TextMeshProUGUI spellName_4;
    //public TextMeshProUGUI spellCooldown_4;
    //public TextMeshProUGUI spellInput_4;

    [Header("Spell_5")]
    public TextMeshProUGUI spellDesc_5;
    public TextMeshProUGUI spellName_5;
    //public TextMeshProUGUI spellCooldown_5;
    //public TextMeshProUGUI spellInput_5;

    [Header("Spell_6")]
    public TextMeshProUGUI spellDesc_6;
    public TextMeshProUGUI spellName_6;
    //public TextMeshProUGUI spellCooldown_6;
    //public TextMeshProUGUI spellInput_6;

    [Header("Other")]
    public Sprite[] backgroundImageReference = new Sprite[5];
    public Image spellIcon;
    public Image Background;
    public Image selectFill;
    //public SpellVideoPlayer spellVideoPlayer;
    //public GameObject spellVideoObject;

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

    [SerializeField]
    private TextMeshProUGUI[] spellNames = new TextMeshProUGUI[6];

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    public void SetSpellFloppyDisplay(string[] spells)
    {
        spellData = SpellDictionary.Instance.spellDict[spells[1]];

        charName.text = spells[0];
        charExe.text = spells[0] + ".exe";
        spellIcon.sprite = spellData.readyIcon;

        spellNames[0] = spellName_1;
        spellNames[1] = spellName_2;
        spellNames[2] = spellName_3;
        spellNames[3] = spellName_4;
        spellNames[4] = spellName_5;
        spellNames[5] = spellName_6;

        for (int i = 1; i < spells.Length; i++)
        {
            spellNames[i - 1].text = spells[i];
        }

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
            case Brand.DarkWeb:
                Background.sprite = backgroundImageReference[4];
                break;
        }
    }

    public void StartFloppyDisplay()
    {
        SetCanvasEnabled(true);
        //if (spellVideoPlayer != null)
        //{
        //    spellVideoPlayer.PlayPrepared();
        //}
        //SetDescriptionVisible(false, false);
    }

    public void StopFloppyDisplay()
    {
        Canvas canvas = GetDisplayCanvas();
        if (canvas == null || !canvas.enabled)
        {
            return;
        }

        //KillDisplayTweens();
        //spellVideoPlayer?.Hide();
        canvas.enabled = false;
    }

    public bool IsDisplayCanvasEnabled()
    {
        Canvas canvas = GetDisplayCanvas();
        return canvas != null && canvas.enabled;
    }

    //public void FloppyDisplayUpdate()
    //{
    //    KillDisplayTweens();

    //    if (showDesc)
    //    {
    //        if (spellVideoObject != null)
    //        {
    //            Tween tween = spellVideoObject.transform.DOScale(videoScaleDesc, .25f).SetLink(spellVideoObject);
    //            tween.OnComplete(() =>
    //            {
    //                if (spellDesc != null)
    //                {
    //                    spellDesc.DOColor(spellData.brands[0] == Brand.DarkWeb ? GameManager.colors["white"] : Color.black, .25f).SetLink(spellDesc.gameObject);
    //                }
    //            });
    //        }
    //    }
    //    else
    //    {
    //        if (spellDesc != null)
    //        {
    //            Tween tween = spellDesc.DOColor(Color.clear, .25f).SetLink(spellDesc.gameObject);
    //            tween.OnComplete(() =>
    //            {
    //                if (spellVideoObject != null)
    //                {
    //                    spellVideoObject.transform.DOScale(videoScaleNoDesc, .25f).SetLink(spellVideoObject);
    //                }
    //            });
    //        }
    //    }
    //}

    //public void SetDescriptionVisible(bool visible, bool animate)
    //{
    //    showDesc = visible;

    //    if (animate)
    //    {
    //        FloppyDisplayUpdate();
    //        return;
    //    }

    //    KillDisplayTweens();

    //    if (spellVideoObject != null)
    //    {
    //        spellVideoObject.transform.localScale = new Vector3(
    //            showDesc ? videoScaleDesc : videoScaleNoDesc,
    //            showDesc ? videoScaleDesc : videoScaleNoDesc,
    //            1);
    //    }

    //    if (spellDesc != null)
    //    {
    //        spellDesc.color = showDesc ? Color.black : Color.clear;
    //    }
    //}

    public void SetFloppyDisplayPosition(int index)
    {
        Background.rectTransform.anchoredPosition = displayLocations[index];
    }

    //private void OnDisable()
    //{
    //    KillDisplayTweens();
    //}

    //private void OnDestroy()
    //{
    //    KillDisplayTweens();
    //}

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

    //private void KillDisplayTweens()
    //{
    //    if (spellVideoObject != null)
    //    {
    //        spellVideoObject.transform.DOKill();
    //    }

    //    if (spellDesc != null)
    //    {
    //        spellDesc.DOKill();
    //    }
    //}

}
