using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BifronsBladeBasic_prj : BaseProjectile
{

    public BifronsBladeBasic_prj()
    {
        projName = "BifronsBladeBasic";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 45; // lasts for 300 logic frames

        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 3, 3, 3 }, false);

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
                    xOffset = -6*2,
                    yOffset = 17*2,
                    width = 57*2,
                    height = 33*2,
                    xKnockback = 1,
                    yKnockback = 7,
                    damage = 5,
                    hitstun = 40,
                    attackLvl = 2,
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

        if (logicFrame >= animFrames.frameLengths.Take(2).Sum()+1 && logicFrame <= animFrames.frameLengths.Take(3).Sum())
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
        
    }
}
