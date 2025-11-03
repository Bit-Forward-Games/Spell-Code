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
    OnHit,
    OnHurt,
    OnDodge,
    OnDodged,
    OnBlock,
    OnCast,
    OnKill,
    OnDeath,
    OnUpdate
    
}

/// <summary>
/// [CreateAssetMenu(fileName = "New Spell", menuName = "Spells/Spell Data")]
/// </summary>
public abstract class SpellData : MonoBehaviour
{
    
    //[Header("Identification & Network")]
    [HideInInspector]
    public string spellName;
    [HideInInspector]
    public Brand[] brands;
    [HideInInspector]
    public ProcCondition[]procConditions;

    public string description;

    //[Header("Casting Requirements")]
    //public SpellDirection[] inputSequence;
    //[HideInInspector]
    public int cooldown;
    [HideInInspector]
    public int cooldownCounter = 0;
    [HideInInspector]
    public uint spellInput = 0b_0000_0000_0000_0000_0000_0000_0000_0000;
    [HideInInspector]
    public bool activateFlag = false;
    [HideInInspector]
    public PlayerController owner;
    [HideInInspector]
    public SpellType spellType;
    [HideInInspector]
    public int spawnOffsetX = 10;
    [HideInInspector]
    public int spawnOffsetY = 15;

    //[Header("Prefab")]
    public GameObject[] projectilePrefabs;

    public Sprite shopSprite;


    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }
    /// <summary>
    /// tis function is called every logic frame to update the spell's internal state
    /// </summary>
    public virtual void SpellUpdate()
    {
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }
        if (activateFlag)
        {

            // Reset the activate flag
            activateFlag = false;

            
            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 0)
            {
                ProjectileManager.Instance.SpawnProjectile(spellName, owner, owner.facingRight, new Vector2(spawnOffsetX, spawnOffsetY));
            }
            cooldownCounter = cooldown;
        }
    }

    public virtual void LoadSpell()
    {

    }

    /// <summary>
    /// this function is called when a SPECIFIC spell need its own unique proc behavior on hit
    /// e.g. an active spell that does something special when you hit it, but wouldn't apply to all spell's on hit
    /// </summary>
    public virtual void ActiveOnHitProc( PlayerController defender)
    {

    }


    /// <summary>
    /// This function checks if the conditions for setting the given spell's activateFlag is met after its procCondition is triggered
    /// e.g. if the spell's procCondition is OnHit and requires the player to have a certain number of some resource to activate,
    /// this function would check for that.
    /// </summary>
    public abstract void CheckCondition();
}