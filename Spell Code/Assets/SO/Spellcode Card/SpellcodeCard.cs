using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "SpellcodeCard", menuName = "Scriptable Objects/SpellcodeCard")]
public class SpellcodeCard : ScriptableObject
{
    public string spellName;
    public Sprite spell;
}
