using UnityEngine;

public class TutorialGlyph : MonoBehaviour
{

    public Animator dpadAnimator;
    public Animator buttonsAnimator;
    public TextSetter[] textSetters;
    public int phase;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dpadAnimator.SetInteger("phase",phase);
        buttonsAnimator.SetInteger("phase",phase);
        textSetters = gameObject.GetComponentsInChildren<TextSetter>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateAllGlyphs()
    {
        if (textSetters == null || textSetters.Length == 0)
        {
            textSetters = gameObject.GetComponentsInChildren<TextSetter>();
        }

        foreach(TextSetter ts in textSetters)
        {
            if (ts != null)
            {
                ts.UpdateGlyph();
            }
        }
    }
}
