using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class TutorialGlyphAnimationEvents : MonoBehaviour
{
    private TMP_Text animatedText;
    private TutorialGlyph tutorialGlyph;

    private void Awake()
    {
        ResolveReferences();
    }

    public void SetTextAndUpdateAllGlyphs(string newText)
    {
        ResolveReferences();

        animatedText.text = newText;
        tutorialGlyph.UpdateAllGlyphs();
    }

    private void ResolveReferences()
    {
        if (animatedText == null)
        {
            animatedText = GetComponent<TMP_Text>();
        }

        if (tutorialGlyph == null)
        {
            tutorialGlyph = GetComponentInParent<TutorialGlyph>();
        }
    }
}
