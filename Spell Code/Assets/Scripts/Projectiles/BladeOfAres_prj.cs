using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BladeOfAres_prj : BaseProjectile
{

    public BladeOfAres_prj()
    {
        projName = "BladeOfAres";
        lifeSpan = 45; 

        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 5, 5, 5}, false);

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
    }

    public override void LoadProjectile()
    {
        projectileHitboxes = new HitboxGroup[2];
        projectileHitboxes[1] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -13*2,
                    yOffset = 37*2,
                    width = 35*2,
                    height = 17*2,
                    xKnockback = 1,
                    yKnockback = 8,
                    damage = 10,
                    hitstun = 30,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = 12*2,
                    yOffset = 29*2,
                    width = 27*2,
                    height = 45*2,
                    xKnockback = 5,
                    yKnockback = 5,
                    damage = 10,
                    hitstun = 20,
                    attackLvl = 2
                }
            },
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
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        logicFrame++;
        // Update animation frame
        animationFrame = GetCurrentFrameIndex(animFrames.frameLengths, animFrames.loopAnim);

        Fixed xOffset = Fixed.FromInt(ownerSpell.spawnOffsetX);
        Fixed yOffset = Fixed.FromInt(ownerSpell.spawnOffsetY);
        Fixed direction = Fixed.FromInt(owner.facingRight ? 1 : -1);
        Fixed newX = owner.position.X + (xOffset * direction);
        Fixed newY = owner.position.Y + yOffset;

        position = new FixedVec2(newX, newY);

        if (logicFrame >= animFrames.frameLengths.Take(3).Sum()+1 && logicFrame <= animFrames.frameLengths.Take(4).Sum())
        {
            activeHitboxGroupIndex = 1;
        }
        else
        {
            activeHitboxGroupIndex = 0;
        }
        if (logicFrame >= animFrames.frameLengths.Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
        //this basically checks if the projectile hit something
        
    }
}
