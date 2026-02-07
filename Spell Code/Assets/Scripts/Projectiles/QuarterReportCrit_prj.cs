using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class QuarterReportCrit_prj : BaseProjectile
{

    public QuarterReportCrit_prj()
    {
        projName = "QuarterReportCrit";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 180; 

        animFrames = new AnimFrames(new List<int>(), new List<int>() { 6, 6, 6, 6, 6, 6, 6, 6 }, true);

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
     
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        this.hSpeed = Fixed.FromFloat((facingRight ? 1 : -1) * 5.33f); // Set horizontal speed based on facing direction
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
                    xOffset = -48,
                    yOffset = 48,
                    width = 48*2,
                    height = 48*2,
                    xKnockback = 6,
                    yKnockback = 8,
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
