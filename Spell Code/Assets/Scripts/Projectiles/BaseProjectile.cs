using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public abstract class BaseProjectile : MonoBehaviour
{

    [HideInInspector]
    public string projName;
    [HideInInspector]
    public HitboxGroup[] projectileHitboxes;
    public Sprite[] sprites;
    [HideInInspector]
    public byte activeHitboxGroupIndex = 0;
    public float hSpeed;
    public float vSpeed;
    public Vector2 position;
    public bool facingRight;
    public int logicFrame;
    public ushort animationFrame; //which frame of animation the projectile is on
    public ushort lifeSpan = 60; //in logic frames
    public PlayerController owner;
    public SpellData ownerSpell;
    public bool[] playerIgnoreArr = new bool[4] { false, false, false, false }; //which players this projectile should ignore collisions with 
    public AnimFrames animFrames;
    public bool deleteOnHit = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public virtual void SpawnProjectile(/*PlayerController owner,*/ bool facingRight, Vector2 spawnOffset/*, float hSpeed, float vSpeed, HitboxData[] hitboxDatas*/)
    {
        //this.owner = owner;
        this.facingRight = facingRight;
        this.position = owner.position + (new Vector2(spawnOffset.x * (owner.facingRight ? 1 : -1), spawnOffset.y));
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
        hSpeed = 0;
        vSpeed = 0;
        position = Vector2.zero;
        playerIgnoreArr = new bool[4] { false, false, false, false };
        facingRight = true;
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
        position.x += hSpeed;
        position.y += vSpeed;
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

    
}
