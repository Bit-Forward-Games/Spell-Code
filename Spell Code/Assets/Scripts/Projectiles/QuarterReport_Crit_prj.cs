using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class QuarterReportCrit_prj : BaseProjectile
{

    public QuarterReportCrit_prj()
    {
        projName = "QuarterReport";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 45; 

        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 }, true);

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        this.hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 5); // Set horizontal speed based on facing direction
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
                    yOffset = 36,
                    width = 23*2,
                    height = 36*2,
                    xKnockback = 4,
                    yKnockback = 5,
                    damage = 20,
                    hitstun = 45,
                    attackLvl = 3,
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        base.LoadProjectile();
    }
}
