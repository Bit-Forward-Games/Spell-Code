using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using Steamworks.ServerList;
using DG.Tweening.Core.Easing;

public class TrickshotAlley_prj : BaseProjectile
{
    public const int slowSpeed = 1;
    //public const int fastSpeed = 6;

    public const int highBounce = 6;
    //public const int lowBounce = 2;

    //public byte bounceCount = 0;
    
    public HurtboxData hurtbox = new HurtboxData
    {
        xOffset = -16,
        yOffset = 16,
        width =32,
        height = 32,
    };
    protected override void InitializeDefaults()
    {
        projName = "Trickshot Alley";
        hSpeed = Fixed.FromInt(1);
        vSpeed = Fixed.FromInt(0);
        lifeSpan = 0; 
        deleteOnHit = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6}, false);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
        
        //bounceCount = 0; // reset bounce count on spawn
        hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * slowSpeed); 
        vSpeed = Fixed.FromInt(highBounce); 
    }
    public override void LoadProjectile()
    {

        //bounceCount = 0;
        deleteOnHit = true;
        projectileHitboxes = new HitboxGroup[3];

        projectileHitboxes[1] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -12,
                    yOffset = 12,
                    width =24,
                    height = 24,
                    xKnockback = 4,
                    yKnockback = 4,
                    damage = 15,
                    hitstun = 20,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[2] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -12,
                    yOffset = 12,
                    width =24,
                    height = 24,
                    xKnockback = 5,
                    yKnockback = 5,
                    damage = 20,
                    hitstun = 30,
                    attackLvl = 2,
                    sweetSpot = true
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>(),
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        frameData = new FrameData
        {
            startFrames = new List<int>
            {
                0,
                animFrames.frameLengths.Take(16).Sum()+1
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(16).Sum(),
                animFrames.frameLengths.Sum()
            }
        };
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();
        ProcessTrickshotCollisisons();
        //okay so this logic is a bit wonky to understand but basically if the ball hits something,
        //it switches to the non-hitting hitbox group, sets its horizontal speed to 0,
        //and then waits until the animation is done to delete itself.
        if (logicFrame == animFrames.frameLengths.Take(16).Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }


        //activeHitboxGroupIndex = (byte)(logicFrame > animFrames.frameLengths.Take(16).Sum()? 1:0);
        
        vSpeed -= owner.gravity/Fixed.FromFloat(4f); // Apply gravity to the vertical speed
    }

    public void ProcessTrickshotCollisisons()
    {
        List<BaseProjectile> myProjectiles = ProjectileManager.Instance.activeProjectiles
        .Where(projectile => projectile != null && projectile.owner == owner && projectile != this)
        .ToList();

        foreach(BaseProjectile proj in myProjectiles)
        {
            if(HitboxManager.Instance.ProcessSingleProjectileCollisison(proj, hurtbox, position, out HitboxData hitbox, facingRight))
            {
                hSpeed = Fixed.FromInt(hitbox.xKnockback * (proj.facingRight ? 1 : -1)); 
                vSpeed = Fixed.FromInt(hitbox.yKnockback); 
                proj.playerIgnoreArr[owner.pID-1] = true;
                logicFrame = animFrames.frameLengths.Take(16).Sum()+1;
                ownerSpell.cooldownCounter-= 60;
            }
        }

    }

}
