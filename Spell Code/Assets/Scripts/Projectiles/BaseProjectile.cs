using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using System;

public abstract class BaseProjectile : MonoBehaviour
{
    [NonSerialized]  public string projName;
    [NonSerialized]  public HitboxGroup[] projectileHitboxes;
    public Sprite[] sprites;
    [NonSerialized]  public byte activeHitboxGroupIndex = 0;
    public Fixed hSpeed;
    public Fixed vSpeed;
    public FixedVec2 position;
    [NonSerialized]
    public bool facingRight;
    public int logicFrame;
    public ushort animationFrame; //which frame of animation the projectile is on
    [NonSerialized] public ushort lifeSpan = 0; //in logic frames, when lifeSpan == 0 ignore it
    [NonSerialized] public PlayerController owner;
    [NonSerialized] public SpellData ownerSpell;
    [NonSerialized] public bool[] playerIgnoreArr = new bool[4] { false, false, false, false }; //which players this projectile should ignore collisions with 

    //Multihit projectile fields
    [NonSerialized] public ushort[] multiHitPlayerIgnoreCounterArr = new ushort[]{ 0, 0, 0, 0 };
    [NonSerialized] public byte[] multiHitCount = new byte[]{ 0, 0, 0, 0 };
    [NonSerialized] public byte maxMultiHitCount = 0;
    [NonSerialized] public byte multiHitCooldown = 0;

    //anim frames
    [NonSerialized] public AnimFrames animFrames;
    [NonSerialized]  public bool deleteOnHit = false;
    [NonSerialized] public bool ignoreBrand = false;
    // [NonSerialized] public bool ignoreEffectDamage = false;
    [NonSerialized] public bool meleeProjectile = false;
    [NonSerialized] public Action onHitAction = null;

    [NonSerialized] public FrameData frameData = null;//NOTE: IF FRAMEDATA IS NOT NULL HITBOX[0] MUST ALWAYS BE A NULL HITBOX

    // Temporary storage for deserialized IDs before references are resolved
    private int _tempOwnerIndex = -1;
    private int _tempOwnerSpellIndex = -1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        InitializeDefaults();
        DontDestroyOnLoad(this.gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public virtual void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        //this.owner = owner;
        this.facingRight = facingRight;
        position = owner.position + (new FixedVec2(spawnOffset.X * Fixed.FromInt((facingRight ? 1 : -1)), spawnOffset.Y));
        activeHitboxGroupIndex = 0;
        logicFrame = 0;
        Array.Fill(multiHitCount, maxMultiHitCount);

        //if nameOverride is empty,...
        if (nameOverride == "")
        {
            //play the defualt spawn sfx for this spellcode based on the projectile name
            SFX_Manager.Instance.PlaySpellcodeSound(projName, 1.0f, 1.0f);
        }
        //else nameOverrie has a value,...
        else
        {
            //play the spawn sfx for this spellcode based on nameOverride
            SFX_Manager.Instance.PlaySpellcodeSound(nameOverride, 1.0f, 1.0f);
        }
    }

    public virtual void ResetValues()
    {
        activeHitboxGroupIndex = 0;
        logicFrame = 0;
        animationFrame = 0;
        hSpeed = Fixed.FromInt(0); 
        vSpeed = Fixed.FromInt(0);
        position = FixedVec2.Zero;
        playerIgnoreArr = new bool[4] { false, false, false, false };
        multiHitPlayerIgnoreCounterArr = new ushort[]{ 0, 0, 0, 0 };
        Array.Fill(multiHitCount, maxMultiHitCount);
        facingRight = true;
        _tempOwnerIndex = -1;
        _tempOwnerSpellIndex = -1;
    }
    public virtual void LoadProjectile()
    {
        InitializeDefaults();
        foreach (HitboxGroup hitboxGroup in projectileHitboxes)
        {
            foreach (List<HitboxData> hitboxList in new List<List<HitboxData>> { hitboxGroup.hitbox1, hitboxGroup.hitbox2, hitboxGroup.hitbox3, hitboxGroup.hitbox4 })
            {
                foreach (HitboxData hitbox in hitboxList)
                {
                    hitbox.parentProjectile = this;
                }
            }
        }
        activeHitboxGroupIndex = 0;
        logicFrame = 0;
    }

    protected virtual void InitializeDefaults() { }

    public void EnsureDefaults()
    {
        if (string.IsNullOrEmpty(projName))
        {
            InitializeDefaults();
        }
    }

    public virtual void ProjectileUpdate()
    {
        ProjectileUpdate(null);   
    }
    public virtual void ProjectileUpdate(Action onHitAction)
    {
        
        logicFrame++;

        if (meleeProjectile)
        {
            Fixed xOffset = Fixed.FromInt(ownerSpell.spawnOffsetX);
            Fixed yOffset = Fixed.FromInt(ownerSpell.spawnOffsetY);
            Fixed direction = Fixed.FromInt(owner.facingRight ? 1 : -1);
            Fixed newX = owner.position.X + (xOffset * direction);
            Fixed newY = owner.position.Y + yOffset;

            position = new FixedVec2(newX, newY);
        }
        else
        {
            position += new FixedVec2(hSpeed, vSpeed);
        }

        // Check lifespan
        if( lifeSpan != 0)  //if lifeSpan == 0, then use the anim frames instead of lifespan to delete the projectile
        {
            if (logicFrame >= lifeSpan)
            {
                ProjectileManager.Instance.DeleteProjectile(this);
                return;
            }
        }
        else if (logicFrame >= animFrames.frameLengths.Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
        
        if(frameData != null)
        {
            bool hitboxSet = false;
            for(int i = 0; i < frameData.startFrames.Count; i++)
            {
                if(logicFrame >= frameData.startFrames[i] && logicFrame <= frameData.endFrames[i])
                {
                    activeHitboxGroupIndex = (byte)(i+1);
                    hitboxSet = true;
                }
            }
            if(!hitboxSet)activeHitboxGroupIndex = 0;
        }


        //this is what happens when this projectile hits something
        if(playerIgnoreArr.Any(ignore => ignore))
        {
            //first, if we've added an onhit action in an override for a projectile, do it
            if(onHitAction != null) onHitAction();

            //then check if the projectile is a multihit
            if(maxMultiHitCount != 0 && multiHitCount != new byte[]{0,0,0,0})
            {
                //loop through all the players
                for(int i = 0; i < multiHitPlayerIgnoreCounterArr.Length; i++)
                {
                    if (playerIgnoreArr[i])
                    {
                        //if a given player was hit and has had long enough since last hit, consume a hit, restart the cooldown per that player, and set that player back to false
                        if( multiHitPlayerIgnoreCounterArr[i] >= multiHitCooldown && multiHitCount[i] > 0)
                        {
                            multiHitCount[i] --;
                            
                            playerIgnoreArr[i] = false;
                            multiHitPlayerIgnoreCounterArr[i] = 0;
                        }   
                        else
                        {
                            multiHitPlayerIgnoreCounterArr[i]++;
                        }
                    }
                    
                }   
                
                
             
            }
            //if the projectile either isnt a multihit or has no more hits left, check if it should be deleted on hit
            else if (deleteOnHit)
            {
                ProjectileManager.Instance.DeleteProjectile(this);
                return;
            }
            
        }

        


        // Update animation frame
        animationFrame = GetCurrentFrameIndex(animFrames.frameLengths, animFrames.loopAnim);

    }

    public ushort GetCurrentFrameIndex(List<int> frameLengths, bool loopAnim)
    {
        int accumulatedLength = 0;
        int totalAnimationLength = frameLengths.Sum();
        if (totalAnimationLength <= 0) return 0;
        int animFrame = loopAnim ? (logicFrame % totalAnimationLength) : Math.Clamp(logicFrame, 0, totalAnimationLength - 1);

        for (ushort i = 0; i < frameLengths.Count; i++)
        {
            accumulatedLength += frameLengths[i];
            if (animFrame < accumulatedLength)
            {
                return i; // Return correct frame index
            }
        }
        return 0; // Default to first frame (shouldn't happen)
    }

    // Serialization Methods

    public virtual void Serialize(BinaryWriter bw)
    {
        // Fixed-point values: Write the internal raw integer
        bw.Write(position.X.RawValue);
        bw.Write(position.Y.RawValue);
        bw.Write(hSpeed.RawValue);
        bw.Write(vSpeed.RawValue);

        // Other state
        bw.Write(facingRight);
        bw.Write(logicFrame);
        bw.Write(animationFrame); // Save animation frame directly
        bw.Write(activeHitboxGroupIndex);
        bw.Write(lifeSpan); // Save lifespan in case it changes dynamically? (If static, no need)
        bw.Write(deleteOnHit);
        bw.Write(multiHitCount);
        bw.Write(ignoreBrand);

        // Player Ignore Array
        for (int i = 0; i < 4; i++)
        {
            bw.Write(playerIgnoreArr[i]);
            bw.Write(multiHitPlayerIgnoreCounterArr[i]);
        }

        // References as IDs
        // Owner Player Index
        int ownerIndex = -1;
        if (owner != null)
        {
            // Find the index of the owner in the GameManager's player array
            ownerIndex = System.Array.IndexOf(GameManager.Instance.players, owner);
        }
        bw.Write(ownerIndex); // Write -1 if no owner

        // Owner Spell Index (in the owner's spell list)
        int spellIndex = -1;
        if (owner != null && ownerSpell != null)
        {
            spellIndex = owner.spellList.IndexOf(ownerSpell);
        }
        bw.Write(spellIndex); // Write -1 if no owner spell or owner
    }

    public virtual void Deserialize(BinaryReader br)
    {
        // Fixed-point values: Read raw integer and reconstruct
        position = new FixedVec2(new Fixed(br.ReadInt32()), new Fixed(br.ReadInt32())); // Assuming Fixed32 uses int internally
        hSpeed = new Fixed(br.ReadInt32());
        vSpeed = new Fixed(br.ReadInt32());

        // Other state
        facingRight = br.ReadBoolean();
        logicFrame = br.ReadInt32();
        animationFrame = br.ReadUInt16(); // Read animation frame
        activeHitboxGroupIndex = br.ReadByte();
        lifeSpan = br.ReadUInt16(); // Read lifespan
        deleteOnHit = br.ReadBoolean();
        multiHitCount = br.ReadByte();
        ignoreBrand = br.ReadBoolean();

        // Player Ignore Array
        for (int i = 0; i < 4; i++)
        {
            playerIgnoreArr[i] = br.ReadBoolean();
            multiHitPlayerIgnoreCounterArr[i] = br.ReadUInt16();
        }

        // References as IDs
        // Read IDs and store them temporarily. Actual references will be restored later.
        _tempOwnerIndex = br.ReadInt32();
        _tempOwnerSpellIndex = br.ReadInt32();

        // Clear actual references, they will be restored in ResolveReferences
        owner = null;
        ownerSpell = null;
    }

    /// <summary>
    /// Restores object references after all objects have been deserialized.
    /// This should be called by the central deserialization logic (e.g., in OnStageObjects).
    /// </summary>
    public void ResolveReferences()
    {
        // Restore Owner reference
        if (_tempOwnerIndex >= 0 && _tempOwnerIndex < GameManager.Instance.playerCount)
        {
            owner = GameManager.Instance.players[_tempOwnerIndex];

            // Restore Owner Spell reference (only if owner was successfully restored)
            if (owner != null && _tempOwnerSpellIndex >= 0 && _tempOwnerSpellIndex < owner.spellList.Count)
            {
                ownerSpell = owner.spellList[_tempOwnerSpellIndex];
            }
        }

        // Clear temporary IDs after use
        _tempOwnerIndex = -1;
        _tempOwnerSpellIndex = -1;
    }

}
