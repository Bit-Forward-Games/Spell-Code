using UnityEngine;
using System.Collections.Generic;

/*public enum SpellDirection 
{ 
    None, 
    Up, 
    Down, 
    Left, 
    Right 
}*/

public enum SpellType
{
    Passive,
    Active
}


/// <summary>
/// [CreateAssetMenu(fileName = "New Spell", menuName = "Spells/Spell Data")]
/// </summary>
public abstract class SpellData : MonoBehaviour
{
    //[Header("Identification & Network")]
    public string spellName;
    //public ushort spellId;

    //[Header("Casting Requirements")]
    //public SpellDirection[] inputSequence;
    public float cooldown;
    public uint spellInput = 0b_0000_0000_0000_0000_0000_0000_0000_0000;
    public bool activateFlag = false;
    public PlayerController owner;
    public SpellType spellType;

    //[Header("Prefab")]
    public GameObject[] projectilePrefabs;


    public virtual void LoadSpell()
    {
        // Load or initialize spell-specific data here
    }

    public abstract void SpellUpdate();
}