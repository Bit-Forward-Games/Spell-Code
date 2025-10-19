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

public enum Brand
{
    None,
    VWave,
    RawrDX,
    Killeez,
    BigStox,
    SLUG,
    Halk
}

public enum ProcCondition
{
    OnSpellHit,
    OnBasicHit,
    OnDamaged,
    OnDodge,
    OnDodged,
    OnBlock,
    OnSpellCast,
    OnBasicCast,
    OnKill,
    OnDeath,
    OnLocationCheck
    
}

/// <summary>
/// [CreateAssetMenu(fileName = "New Spell", menuName = "Spells/Spell Data")]
/// </summary>
public abstract class SpellData : MonoBehaviour
{
    //[Header("Identification & Network")]
    public string spellName;
    public Brand[] brands;
    public ProcCondition[]procConditions;

    //[Header("Casting Requirements")]
    //public SpellDirection[] inputSequence;
    public int cooldown;
    public int cooldownCounter = 0;
    public uint spellInput = 0b_0000_0000_0000_0000_0000_0000_0000_0000;
    public bool activateFlag = false;
    public PlayerController owner;
    public SpellType spellType;

    //[Header("Prefab")]
    public GameObject[] projectilePrefabs;




    public abstract void SpellUpdate();

    //public abstract void CheckCondition();

    public abstract void CheckProcEffect();
}