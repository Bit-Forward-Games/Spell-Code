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

    public int controlOptionDescriptionIndex;

    public void ResetCodeModePromptPresentation()
    {
        wasCodeModeMenuOpen = false;
        lastCodeModeDirection = 5;
    }
    
    /// <summary>
    /// Writes the controls panel's description label, or does nothing if it is not in the scene.
    /// GameObject.Find only sees ACTIVE objects, so the lookup returns null whenever the label (or
    /// any ancestor) is disabled -- including mid-teardown. Dereferencing it there throws out of a
    /// selection handler, and an exception on that path is what strands the screen cover and
    /// black-screens a transition (same failure mode as the old TextSetter.SetText crash). Fail
    /// quietly instead.
    /// </summary>
    public static void SetControlOptionDescription(string description)
    {
        GameObject describeObject = GameObject.Find("Control Option Description Text");
        TextMeshProUGUI describeText = describeObject != null
            ? describeObject.GetComponent<TextMeshProUGUI>()
            : null;

        if (describeText != null)
        {
            describeText.text = description;
        }
    }

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
            
            if (name.Split('_')[0] == "Options" || name.Contains("Slider") || name.Contains("Sign") || name.Contains("Rebind"))
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

            if (name.Contains("Slider") || name.Contains("Digital") || name.Contains("Rebind") || name.Split('_')[0] == "Options")
            {
                Transform sliderChildTransform = transform.Find("SignSelector");
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

            if (name.Contains("Lobby"))
            {
                Transform lobbyChildTransform = transform.Find("LobbySelector");
                if (lobbyChildTransform != null)
                {
                    Image lobbySelector = lobbyChildTransform.gameObject.GetComponent<Image>();
                    lobbyChildTransform.gameObject.SetActive(true);
                    lobbySelector.fillAmount = 0f;
                    DOTween.To(() => (float)lobbySelector.fillAmount, x => lobbySelector.fillAmount = (float)x, 1f, 0.35f)
                        .SetTarget(lobbySelector)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(true);
                }
            }
            
            if (name.Contains("FindMatch"))
            {
                Image findMatchSelector = GetComponent<Image>();
                findMatchSelector.gameObject.SetActive(true);
                findMatchSelector.fillAmount = 0f;
                DOTween.To(() => (float)findMatchSelector.fillAmount, x => findMatchSelector.fillAmount = (float)x, 1f, 0.5f)
                    .SetTarget(findMatchSelector)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }

            if (name.Contains("Describe"))
            {
                string description;
                switch (controlOptionDescriptionIndex)
                {
                    case 0:
                        description = "Tap the up direction to jump";
                        break;
                    case 1:
                        description = "Disable the abillity to slide using down and jump. Used to help consistently dropping through platforms";
                        break;
                    case 2:
                        description = "Tap the code button again instead of releasing the button to execute the stored code";
                        break;
                    case 3:
                        description = "Left and right button inputs are based on the player's facing direction ( e.g. right is forward )";
                        break;
                    default:
                        description = "";
                        break;
                }

                SetControlOptionDescription(description);
            }
        }
    }

    // Triggers automatically when the button loses focus / gets unselected
    public void OnDeselect(BaseEventData eventData)
    {
        if (name.Contains("_"))
        {
            // The buttons that have no description of their own (key rebinds, Back, Reset) never
            // run the OnSelect block above, so the panel would keep displaying whichever option was
            // highlighted last. Blank it on the way out instead: the EventSystem always deselects
            // the old object before selecting the new one, so a described option still overwrites
            // this with its own text, and everything else correctly leaves the label empty.
            if (name.Contains("Describe"))
            {
                SetControlOptionDescription("");
            }

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

            if (name.Contains("Slider") || name.Contains("Digital") || name.Contains("Rebind") || name.Split('_')[0] == "Options")
            {
                Transform sliderChildTransform = transform.Find("SignSelector");
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

            if (name.Contains("Lobby"))
            {
                Transform lobbyChildTransform = transform.Find("LobbySelector");
                if (lobbyChildTransform != null)
                {
                    Image lobbySelector = lobbyChildTransform.gameObject.GetComponent<Image>();
                    lobbyChildTransform.gameObject.SetActive(false);
                    lobbySelector.fillAmount = 0f;
                }
            }

            if (name.Contains("FindMatch"))
            {
                Image findMatchSelector = GetComponent<Image>();
                findMatchSelector.fillAmount = 0f;
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
            if (name.Contains("Slider") || name.Split('_')[0] == "Digital" || name.Contains("Rebind"))
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
        if (name.Contains("Code Mode"))
        {
            // Runs every render frame, including mid-teardown, so resolve the player defensively.
            GameManager manager = GameManager.Instance;
            PlayerController codeModePlayer =
                manager != null && manager.players != null
                && codeModeIndex >= 0 && codeModeIndex < manager.players.Length
                    ? manager.players[codeModeIndex]
                    : null;
            bool onlineMatch = manager != null && manager.isOnlineMatchActive;

            // ONLINE: the SIM owns the decision. PlayerUpdate clears choosingCodeMode off the
            // NETWORKED jump edge, so every machine agrees on the release frame; the UI only watches
            // that flag. Reading the local InputAction here instead is what let one press close every
            // player's prompt at once, and what desynced the lobby.
            bool confirmed = onlineMatch
                ? (codeModePlayer != null && !codeModePlayer.choosingCodeMode)
                : (wasCodeModeMenuOpen && pause.WasPlayerSubmitPressedThisFrame(codeModeIndex));

            if (codeModePlayer != null && pause.uiScript.codeModePromptMenuOpened[codeModeIndex] && confirmed)
            {
                bool ownsPrompt = !onlineMatch || codeModeIndex == manager.localPlayerIndex;
                if (ownsPrompt)
                {
                    PlayerController player = codeModePlayer;
                    bool punkSelected = pause.uiScript.playerCodeMode[codeModeIndex]
                        .codeModes[PUNK_CODE_MODE_INDEX]
                        .codeModeSelected;

                    // Punk mode drives both options; synthesizer mode leaves both off.
                    player.vibeCoding = punkSelected;
                    player.relativeInputs = punkSelected;

                    // Save AFTER the fields are set: the no-value overload snapshots player.*, and
                    // the online input packing re-reads these saved options every frame.
                    SettingsManager.Instance?.SaveControlOptionsForPlayer(player);
                }

                // Remote panels only mirror networked state. Their actual control options arrive
                // through ApplyOnlineControlOptionsFromInput, so this close must remain cosmetic.
                pause.uiScript.CloseCodeModeMenuPrompt(codeModeIndex);
            }

            // Navigation reads the sim input snapshot, which PlayerUpdate refreshes BEFORE the
            // choosingCodeMode freeze returns. All peers intentionally mirror the owner's networked
            // left/right highlight; only the owning peer may commit or save the option above.
            if (codeModePlayer != null && pause.uiScript.codeModePromptMenuOpened[codeModeIndex])
            {
                int dir = codeModePlayer.input.Direction;
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
        GameObject descriptionText = transform.Find("Code Mode Description").gameObject;

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

            //display the code mode description for the currently selected code mode
            if(descriptionText != null)
            {
                descriptionText.SetActive(true);
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

            //stop displaying the code mode description
            if (descriptionText != null)
            {
                descriptionText.SetActive(false);
            }
        }
    }
}
