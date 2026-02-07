using UnityEngine;
using System.Collections.Generic;
using BestoNet.Types;
using System.IO;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using System;

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
    DemonX,
    Killeez,
    BigStox,
    SLUG,
    Halk
}

public enum ProcCondition
{
    Null,
    OnHit,
    OnHitBasic,
    OnHitSpell,
    ActiveOnHit,
    OnHurt,
    OnHurtBasic,
    OnHurtSpell,
    OnSlide,
    OnDodge,
    OnDodged,
    OnBlock,
    OnCast,
    OnCastBasic,
    OnCastSpell,
    ActiveOnCast,
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
    [NonSerialized]
    public string spellName;
    [NonSerialized]
    public Brand[] brands;
    [NonSerialized]
    public ProcCondition[] procConditions;

    [NonSerialized]
    public string description;

    //[Header("Casting Requirements")]
    //public SpellDirection[] inputSequence;
    [NonSerialized]
    public int cooldown;
    [NonSerialized]
    public int cooldownCounter = 0;
    [NonSerialized]
    public uint spellInput = 0b_0000_0000_0000_0000_0000_0000_0000_0000;
    [NonSerialized]
    public bool activateFlag = false;
    [NonSerialized]
    public PlayerController owner;
    [NonSerialized]
    public SpellType spellType;
    [NonSerialized]
    public int spawnOffsetX = 10;
    [NonSerialized]
    public int spawnOffsetY = 15;

    //[Header("Prefab")]
    public GameObject[] projectilePrefabs;

    //this array holds the actual instances of the projectiles spawned from the prefabs during runtime
    [HideInInspector]
    public List<GameObject> projectileInstances;

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
        if (projectileInstances.Count < 1) return;
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
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }
            cooldownCounter = cooldown;
        }
    }

    public virtual void LoadSpell()
    {

    }

    /// <summary>
    /// This function checks if the conditions for setting the given spell's activateFlag is met after its procCondition is triggered
    /// e.g. if the spell's procCondition is OnHit and requires the player to have a certain number of some resource to activate,
    /// this function would check for that.
    /// </summary>
    public abstract void CheckCondition(PlayerController defender = null, ProcCondition targetProcCon = ProcCondition.Null);

    // Serialization Methods

    /// <summary>
    /// Saves the current dynamic state of the spell.
    /// Base implementation saves cooldownCounter and activateFlag.
    /// Override in derived classes to save additional state.
    /// </summary>
    public virtual void Serialize(BinaryWriter bw)
    {
        bw.Write(cooldownCounter);
        bw.Write(activateFlag);
        // Derived classes should call base.Serialize(bw) then write their own state.
    }

    /// <summary>
    /// Loads the dynamic state of the spell.
    /// Base implementation loads cooldownCounter and activateFlag.
    /// Override in derived classes to load additional state.
    /// </summary>
    public virtual void Deserialize(BinaryReader br)
    {
        cooldownCounter = br.ReadInt32();
        activateFlag = br.ReadBoolean();
        // Derived classes should call base.Deserialize(br) then read their own state.
    }
}