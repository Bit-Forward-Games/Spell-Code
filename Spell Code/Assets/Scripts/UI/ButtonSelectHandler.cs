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
    
    // Triggers automatically when the Event System shifts focus to this button
    public void OnSelect(BaseEventData eventData)
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
         
        if (name.Split('_')[0] == "Options" || name.Contains("Slider"))
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

        if (name.Contains("Slider"))
        {
            Transform sliderChildTransform = transform.Find("SignSelecter");
            if (sliderChildTransform!= null) 
            {
                Debug.Log("Hello???");
                RectTransform signSelector = sliderChildTransform.gameObject.GetComponent<RectTransform>();
                signSelector.localScale = new Vector3(0f, signSelector.localScale.y, signSelector.localScale.z);
                signSelector
                    .DOScaleX(1f, 0.15f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }
    }

    // Triggers automatically when the button loses focus / gets unselected
    public void OnDeselect(BaseEventData eventData)
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

        if (name.Contains("Slider"))
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
    }

    void Start()
    {
        pause = Object.FindAnyObjectByType<Pause>();
    }

    float navCooldown;
    const float NAV_COOLDOWN_TIME = 0.2f;
    Vector2 lastNavValue = Vector2.zero;

    void Update()
    {
        if (EventSystem.current.currentSelectedGameObject == gameObject)
        {
            // 2. Check if the user is actively holding down the Submit key/button
            if (pause.WasPausePlayerSubmitPressedThisFrame()) 
            {
                Transform optionsChildTransform = transform.Find("SignText");
                if (optionsChildTransform != null) 
                {
                    TextMeshProUGUI optionsText = optionsChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                    optionsText.color = new Color(1f, 1f, 1f);
                }
            }

            if (name.Contains("Display Mode"))
            {
                pause.displayIndex = DisplayOptionsCycle(pause.displayModes, pause.displayIndex);
                pause.displayOptionString.text = pause.displayModes[pause.displayIndex];
            }
        }
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
}
