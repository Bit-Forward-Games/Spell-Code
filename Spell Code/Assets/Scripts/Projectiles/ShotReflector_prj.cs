using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using Steamworks.ServerList;
using DG.Tweening.Core.Easing;

public class ShotReflector_prj : BaseProjectile
{
    public const ushort slowSpeed = 1;
    public const ushort fastSpeed = 6;
    public HurtboxData hurtbox = new HurtboxData
    {
        xOffset = -16,
        yOffset = 48,
        width =32,
        height = 96,
    };
    protected override void InitializeDefaults()
    {
        projName = "Shot Reflector";
        // hSpeed = Fixed.FromInt(1);
        // vSpeed = Fixed.FromInt(0);
        lifeSpan = 0; 
        deleteOnHit = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() {6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 }, false);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
        
        //bounceCount = 0; // reset bounce count on spawn
        hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 1); 
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
                    xOffset = -16,
                    yOffset = 48,
                    width =32,
                    height = 96,
                    xKnockback = 2,
                    yKnockback = 5,
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
                    xOffset = -32,
                    yOffset = 16,
                    width =64,
                    height = 32,
                    xKnockback = 4,
                    yKnockback = 2,
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
        if (logicFrame == animFrames.frameLengths.Take(16).Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
    }

    public void ProcessTrickshotCollisisons()
    {
        List<BaseProjectile> opponentProjectiles = ProjectileManager.Instance.activeProjectiles
        .Where(projectile => projectile != null && projectile.owner != owner )
        .ToList();

        foreach(BaseProjectile proj in opponentProjectiles)
        {
            if(HitboxManager.Instance.ProcessSingleProjectileCollisison(proj, hurtbox, position, out HitboxData hitbox, facingRight))
            {
                hSpeed = Fixed.FromInt(fastSpeed * (facingRight ? 1 : -1)); 
                proj.playerIgnoreArr[owner.pID-1] = true;
                logicFrame = animFrames.frameLengths.Take(16).Sum()+1;
                ownerSpell.cooldownCounter-= 60;

                //Play the Shot Reflector Hit SFX
                //SFX_Manager.Instance.PlaySpellcodeSound("Shot Reflector Hit");
            }
        }

    }

}
