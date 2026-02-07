using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class QuarterReport_prj : BaseProjectile
{

    public QuarterReport_prj()
    {
        projName = "QuarterReport";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 30;

        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 }, true);

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        this.hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 4); // Set horizontal speed based on facing direction
    }

    public override void LoadProjectile()
    {
        projectileHitboxes = new HitboxGroup[1];
        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -22,
                    yOffset = 18,
                    width = 23*2,
                    height = 18*2,
                    xKnockback = 4,
                    yKnockback = 5,
                    damage = 10,
                    hitstun = 15,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        base.LoadProjectile();
    }
}
