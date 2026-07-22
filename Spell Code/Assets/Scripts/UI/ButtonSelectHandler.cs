using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

public class ButtonSelectHandler : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    public Pause pause;

    private Transform arrowChildTransform;
    private Transform forwardArrowChildTransform;
    private TextMeshProUGUI arrowText;
    private TextMeshProUGUI forwardArrowText;

    public bool codeModeSelected;
    public int codeModeIndex;
    bool wasCodeModeMenuOpen;
    int lastCodeModeDirection;
    const int PUNK_CODE_MODE_INDEX = 1;
    
    // Triggers automatically when the Event System shifts focus to this button
    public void OnSelect(BaseEventData eventData)
    {
        if (name.Contains("_"))
        {
            if (name.Split('_')[0] == "Digital")
            {
                Transform childTransform = transform.Find("digitalText");
                if (childTransform != null) 
                {
                    TextMeshProUGUI digitalText = childTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    digitalText.font = pause.digitalBorderedFont;
                }
            }
            
            if (name.Split('_')[0] == "Options" || name.Contains("Slider") || name.Contains("Sign"))
            {
                Transform optionsChildTransform = transform.Find("SignText");
                if (optionsChildTransform != null && !pause.suppressingSelectionColor) 
                {
                    TextMeshProUGUI optionsText = optionsChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    optionsText.color = new Color(82f / 255f, 113f / 255f, 51f / 255f);
                }

                Transform blueOptionsChildTransform = transform.Find("Blue_SignText");
                if (blueOptionsChildTransform != null && !pause.suppressingSelectionColor) 
                {
                    TextMeshProUGUI optionsText = blueOptionsChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    optionsText.color = new Color(72f / 255f, 114f / 255f, 118f / 255f);
                }
            }
            
            if (name.Split('_')[1] == "Arrow")
            {
                arrowChildTransform = transform.Find("arrow");
                if (arrowChildTransform!= null) 
                {
                    arrowText = arrowChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    arrowText.text = "<<";
                }
                forwardArrowChildTransform = transform.Find("forwardArrow");
                if (forwardArrowChildTransform!= null) 
                {
                    forwardArrowText = forwardArrowChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    forwardArrowText.text = ">>";
                }
            }

            if (name.Contains("Slider") || name.Contains("Digital"))
            {
                Transform sliderChildTransform = transform.Find("SignSelecter");
                if (sliderChildTransform!= null) 
                {
                    RectTransform signSelector = sliderChildTransform.gameObject.GetComponent<RectTransform>();
                    signSelector.localScale = new Vector3(0f, signSelector.localScale.y, signSelector.localScale.z);
                    signSelector
                        .DOScaleX(1f, 0.15f)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(true);
                }
            }

            if (name.Contains("Pause"))
            {
                RectTransform pauseSelectorTransform = GetComponent<Button>().targetGraphic.gameObject.GetComponent<RectTransform>();
                pauseSelectorTransform.localScale = new Vector3(0f, pauseSelectorTransform.localScale.y, pauseSelectorTransform.localScale.z);
                pauseSelectorTransform.localEulerAngles = new Vector3(0, 0, 0);
                pauseSelectorTransform
                    .DOScaleX(1f, 0.15f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);

                pauseSelectorTransform.DORotate(new Vector3(0, 0, 11f), 0.5f).SetEase(Ease.OutQuad).SetUpdate(true);
                transform.parent.gameObject.GetComponent<Image>().enabled = false;
            }
        }
    }

    // Triggers automatically when the button loses focus / gets unselected
    public void OnDeselect(BaseEventData eventData)
    {
        if (name.Contains("_"))
        {
            Transform childTransform = transform.Find("digitalText");
            if (childTransform != null) 
            {
                TextMeshProUGUI digitalText = childTransform.gameObject.GetComponent<TextMeshProUGUI>();
                digitalText.font = pause.digitalNormalFont;
            }

            Transform optionsChildTransform = transform.Find("SignText");
            if (optionsChildTransform != null) 
            {
                TextMeshProUGUI optionsText = optionsChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                optionsText.color = new Color(255f, 255f, 255f);
            }

            Transform blueOptionsChildTransform = transform.Find("Blue_SignText");
            if (blueOptionsChildTransform != null && !pause.suppressingSelectionColor) 
            {
                TextMeshProUGUI optionsText = blueOptionsChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                optionsText.color = new Color(255f, 255f, 255f);
            }

            if (name.Split('_')[1] == "Arrow")
            {
                arrowChildTransform = transform.Find("arrow");
                if (arrowChildTransform!= null) 
                {
                    arrowText = arrowChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    arrowText.text = "";
                }
                forwardArrowChildTransform = transform.Find("forwardArrow");
                if (forwardArrowChildTransform!= null) 
                {
                    forwardArrowText = forwardArrowChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    forwardArrowText.text = "";
                }
            } 

            if (name.Contains("Slider") || name.Contains("Digital"))
            {
                Transform sliderChildTransform = transform.Find("SignSelecter");
                if (sliderChildTransform!= null) 
                {
                    RectTransform signSelector = sliderChildTransform.gameObject.GetComponent<RectTransform>();
                    signSelector.localScale = new Vector3(1f, signSelector.localScale.y, signSelector.localScale.z);
                    signSelector
                        .DOScaleX(0f, 0.15f)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(true);
                }
            }

            if (name.Contains("Pause"))
            {
                RectTransform pauseSelectorTransform = GetComponent<Button>().targetGraphic.gameObject.GetComponent<RectTransform>();
                pauseSelectorTransform
                    .DOScaleX(0f, 0.15f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);

                pauseSelectorTransform.DORotate(new Vector3(0, 0, 0f), 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
                transform.parent.gameObject.GetComponent<Image>().enabled = true;
            }

            if (name.Contains("Code Mode"))
            {
                Transform textImageTransform = transform.Find("Text Image");
                if (textImageTransform!= null) 
                {
                    textImageTransform.gameObject.SetActive(false);
                }
            } 
        }
    }

    void Start()
    {
        pause = Object.FindAnyObjectByType<Pause>();

        SelectCodeMode();
    }

    float navCooldown;
    const float NAV_COOLDOWN_TIME = 0.2f;
    Vector2 lastNavValue = Vector2.zero;

    void Update()
    {
        if (EventSystem.current.currentSelectedGameObject == gameObject)
        {
            // 2. Check if the user is actively holding down the Submit key/button
            if (name.Contains("Slider") || name.Split('_')[0] == "Digital")
            {
                Transform childTransform = transform.Find("SignText");
                if (childTransform != null)
                {
                    TextMeshProUGUI digitalOptionsText = childTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    digitalOptionsText.color = new Color(82f / 255f, 113f / 255f, 51f / 255f);
                }

                Transform blueOptionsChildTransform = transform.Find("Blue_SignText");
                if (blueOptionsChildTransform != null && !pause.suppressingSelectionColor) 
                {
                    TextMeshProUGUI optionsText = blueOptionsChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    optionsText.color = new Color(72f / 255f, 114f / 255f, 118f / 255f);
                }
            }

            if (pause.WasPausePlayerSubmitPressedThisFrame()) 
            {
                Transform optionsChildTransform = transform.Find("SignText");
                if (optionsChildTransform != null) 
                {
                    TextMeshProUGUI optionsText = optionsChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    optionsText.color = new Color(82f / 255f, 113f / 255f, 51f / 255f);
                }
            }

            if (name.Contains("Display Mode"))
            {
                int previousDisplayIndex = pause.displayIndex;
                pause.displayIndex = DisplayOptionsCycle(pause.displayModes, pause.displayIndex);
                pause.displayOptionString.text = pause.displayModes[pause.displayIndex];

                if (pause.displayIndex != previousDisplayIndex)
                {
                    pause.ApplySelectedDisplayMode();
                }
            }

            if (name.Contains("Resolution"))
            {
                int previousResolutionIndex = pause.resolutionIndex;
                pause.resolutionIndex = DisplayOptionsCycle(pause.resolutions, pause.resolutionIndex);
                pause.resolutionOptionString.text = pause.resolutions[pause.resolutionIndex];

                if (pause.resolutionIndex != previousResolutionIndex)
                {
                    pause.ApplySelectedResolution();
                }
            }
        }
//&& pause.WasPausePlayerSubmitPressedThisFrame()
        if (name.Contains("Code Mode"))
        {
            if (GameManager.Instance.players[codeModeIndex].input.ButtonStates[1] == ButtonState.Pressed)
            {
                PlayerController player = GameManager.Instance.players[codeModeIndex];
                player.vibeCoding = pause.uiScript.playerCodeMode[codeModeIndex]
                    .codeModes[PUNK_CODE_MODE_INDEX]
                    .codeModeSelected;
                SettingsManager.Instance?.SaveControlOptionsForPlayer(player);
                pause.uiScript.CloseCodeModeMenuPrompt(codeModeIndex);
            }

            if (pause.uiScript.codeModePromptMenuOpened[codeModeIndex])
            {
                int dir = GameManager.Instance.players[codeModeIndex].input.Direction;
                if (dir == 4 && lastCodeModeDirection != 4)
                {
                    pause.uiScript.playerCodeMode[codeModeIndex].codeModes[0].codeModeSelected = true;
                    pause.uiScript.playerCodeMode[codeModeIndex].codeModes[1].codeModeSelected = false;

                    pause.uiScript.playerCodeMode[codeModeIndex].codeModes[0].SelectCodeMode();
                    pause.uiScript.playerCodeMode[codeModeIndex].codeModes[1].SelectCodeMode();
                }
                else if (dir == 6 && lastCodeModeDirection != 6)
                {
                    pause.uiScript.playerCodeMode[codeModeIndex].codeModes[0].codeModeSelected = false;
                    pause.uiScript.playerCodeMode[codeModeIndex].codeModes[1].codeModeSelected = true;

                    pause.uiScript.playerCodeMode[codeModeIndex].codeModes[0].SelectCodeMode();
                    pause.uiScript.playerCodeMode[codeModeIndex].codeModes[1].SelectCodeMode();
                }
                lastCodeModeDirection = dir;
            }
        }

        bool isOpen = pause.uiScript.codeModePromptMenuOpened[codeModeIndex];
        if (isOpen && !wasCodeModeMenuOpen && name.Contains("Code Mode"))
        {
            SelectCodeMode();
        }
        wasCodeModeMenuOpen = isOpen;   
    }

    int DisplayOptionsCycle(List<string> optionsList, int currentIndex)
    {
        Vector2 nav = pause.GetPausePlayerNavigation();

        navCooldown -= Time.unscaledDeltaTime;
 
        // Only act on a fresh directional press OR if cooldown has expired while stick is held
        bool freshPress = (nav != Vector2.zero && lastNavValue == Vector2.zero);
        bool heldAndReady = (nav != Vector2.zero && navCooldown <= 0f);
 
        if (freshPress || heldAndReady)
        {
            navCooldown = NAV_COOLDOWN_TIME;
 
            if (nav.x < 0)
            {
                currentIndex = (currentIndex <= 0) ? optionsList.Count - 1 : currentIndex - 1;
            }
            else if (nav.x > 0)
            {
                currentIndex = (currentIndex >= optionsList.Count - 1) ? 0 : currentIndex + 1;
            }
        }

        if (nav == Vector2.zero) navCooldown = 0f;
 
        lastNavValue = nav;

        return currentIndex;
    }

    public void SelectCodeMode()
    {
        if (!name.Contains("Code Mode")) return;

        Image codeModeImage = GetComponent<Image>();
        Transform textImageTransform = transform.Find("Text Image");

        // Kill any tween still in flight on these targets so a stale OnComplete
        // can't deactivate us after a newer tween already changed our state.
        DOTween.Kill(codeModeImage);
        if (textImageTransform != null)
        {
            textImageTransform.GetComponent<RectTransform>().DOKill();
        }

        if (codeModeSelected)
        {
            gameObject.SetActive(true);
            codeModeImage.fillAmount = 0f;
            DOTween.To(() => (float)codeModeImage.fillAmount, x => codeModeImage.fillAmount = (float)x, 1f, 0.35f)
                .SetTarget(codeModeImage)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            if (textImageTransform != null)
            {
                textImageTransform.gameObject.SetActive(true);
                RectTransform rt = textImageTransform.GetComponent<RectTransform>();
                rt.localScale = new Vector3(0f, rt.localScale.y, rt.localScale.z);
                rt.DOScaleX(1f, 0.35f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
        }
        else
        {
            DOTween.To(() => (float)codeModeImage.fillAmount, x => codeModeImage.fillAmount = (float)x, 0f, 0.25f)
                .SetTarget(codeModeImage)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() => gameObject.SetActive(false));

            if (textImageTransform != null)
            {
                RectTransform rt = textImageTransform.GetComponent<RectTransform>();
                rt.DOScaleX(0f, 0.25f)
                    .SetEase(Ease.InQuad)
                    .SetUpdate(true)
                    .OnComplete(() => textImageTransform.gameObject.SetActive(false));
            }
        }
    }
}
