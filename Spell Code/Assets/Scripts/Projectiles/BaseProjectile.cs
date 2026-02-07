using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public abstract class BaseProjectile : MonoBehaviour
{

    [HideInInspector]
    public string projName;
    [HideInInspector]
    public HitboxGroup[] projectileHitboxes;
    public Sprite[] sprites;
    [HideInInspector]
    public byte activeHitboxGroupIndex = 0;
    public Fixed hSpeed;
    public Fixed vSpeed;
    public FixedVec2 position;
    public bool facingRight;
    public int logicFrame;
    public ushort animationFrame; //which frame of animation the projectile is on
    public ushort lifeSpan = 60; //in logic frames
    public PlayerController owner;
    public SpellData ownerSpell;
    public bool[] playerIgnoreArr = new bool[4] { false, false, false, false }; //which players this projectile should ignore collisions with 
    public AnimFrames animFrames;
    public bool deleteOnHit = true;

    // Temporary storage for deserialized IDs before references are resolved
    private int _tempOwnerIndex = -1;
    private int _tempOwnerSpellIndex = -1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public virtual void SpawnProjectile(/*PlayerController owner,*/ bool facingRight, FixedVec2 spawnOffset/*, float hSpeed, float vSpeed, HitboxData[] hitboxDatas*/)
    {
        //this.owner = owner;
        this.facingRight = facingRight;
        this.position = owner.position + (new FixedVec2(spawnOffset.X * Fixed.FromInt((facingRight ? 1 : -1)), spawnOffset.Y));
        //this.hSpeed = hSpeed;
        //this.vSpeed = vSpeed;
        //this.hitboxDatas = hitboxDatas;
        this.activeHitboxGroupIndex = 0;
        this.logicFrame = 0;
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
        facingRight = true;
        _tempOwnerIndex = -1;
        _tempOwnerSpellIndex = -1;
    }
    public virtual void LoadProjectile()
    {
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
    public virtual void ProjectileUpdate()
    {
        //position.X += hSpeed;
        //position.Y += vSpeed;
        position += new FixedVec2(hSpeed, vSpeed);
        logicFrame++;

        // Check lifespan
        if (logicFrame >= lifeSpan)
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }

        //check if the projectile hit something and if it did, delete if necessary
        if (deleteOnHit)
        {
            if (playerIgnoreArr.Any(ignore => ignore))
            {
                ProjectileManager.Instance.DeleteProjectile(this);
            }
        }


        // Update animation frame
        animationFrame = GetCurrentFrameIndex(animFrames.frameLengths, animFrames.loopAnim);

    }

    public ushort GetCurrentFrameIndex(List<int> frameLengths, bool loopAnim)
    {
        int accumulatedLength = 0;
        int totalAnimationLength = frameLengths.Sum();
        int animFrame = loopAnim ? (logicFrame % totalAnimationLength) : Mathf.Clamp(logicFrame, 0, totalAnimationLength - 1);

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

    public void Serialize(BinaryWriter bw)
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

        // Player Ignore Array
        for (int i = 0; i < 4; i++)
        {
            bw.Write(playerIgnoreArr[i]);
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

    public void Deserialize(BinaryReader br)
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

        // Player Ignore Array
        for (int i = 0; i < 4; i++)
        {
            playerIgnoreArr[i] = br.ReadBoolean();
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
