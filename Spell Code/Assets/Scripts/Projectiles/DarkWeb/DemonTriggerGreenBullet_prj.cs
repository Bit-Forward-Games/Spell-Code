using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class DemonTriggerGreenBullet_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Demon Trigger Green Bullet";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 30; 
        deleteOnHit = true;
        fadeOut = true;
        ignoreBrand = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4 }, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset, "Demon Trigger Green Bullet", useAbsolutePosition);
        hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 5); // Set horizontal speed based on facing direction
    }

    public override void LoadProjectile()
    {
        deleteOnHit = true;
        projectileHitboxes = new HitboxGroup[1];
        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -8*2,
                    yOffset = 3*2,
                    width = 16*2,
                    height = 5*2,
                    xKnockback = 3,
                    yKnockback = 2,
                    damage = 5,
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
