using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonSelectHandler : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    public Pause pause;

    private Transform arrowChildTransform;
    private TextMeshProUGUI arrowText;
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
         
        if (name.Split('_')[0] == "Options")
        {
            Transform optionsChildTransform = transform.Find("Text");
            if (optionsChildTransform!= null) 
            {
                TextMeshProUGUI optionsText = optionsChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
                optionsText.color = new Color(82f / 255f, 113f / 255f, 51f / 255f);
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

        Transform optionsChildTransform = transform.Find("Text");
        if (optionsChildTransform != null) 
        {
            TextMeshProUGUI optionsText = optionsChildTransform.gameObject.GetComponent<TextMeshProUGUI>();
            optionsText.color = new Color(255f, 255f, 255f);
        }

        arrowText.text = "";
    }

    void Start()
    {
        pause = Object.FindAnyObjectByType<Pause>();
    }
}
