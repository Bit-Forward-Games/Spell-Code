using UnityEngine;
using System.Collections.Generic;

public enum SpellDirection 
{ 
    None, 
    Up, 
    Down, 
    Left, 
    Right 
}

public enum SpellType
{
    Passive,
    Active
}


[CreateAssetMenu(fileName = "New Spell", menuName = "Spells/Spell Data")]
public class SpellData : ScriptableObject
{
    [Header("Identification & Network")]
    public string spellName;
    public ushort spellId; 

    [Header("Casting Requirements")]
    public SpellDirection[] inputSequence;
    public float cooldown;

    [Header("Prefab")]
    public GameObject spellPrefab;
}