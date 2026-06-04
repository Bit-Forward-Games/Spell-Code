using UnityEngine;

public class TutorialGlyph : MonoBehaviour
{

    public Animator dpadAnimator;
    public Animator buttonsAnimator;
    public int phase;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dpadAnimator.SetInteger("phase",phase);
        buttonsAnimator.SetInteger("phase",phase);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
