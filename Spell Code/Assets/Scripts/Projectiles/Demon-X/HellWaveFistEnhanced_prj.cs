using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class HellWaveFistEnhanced_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Hell Wave Fist Enhanced";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 45;
        maxMultiHitCount = 3;
        multiHitCooldown = 5;
        deleteOnHit = true;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4, 4, 4}, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        this.hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 5); // Set horizontal speed based on facing direction
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
                    xOffset = -16,
                    yOffset = 16,
                    width = 32,
                    height = 32,
                    xKnockback = 3,
                    yKnockback = 8,
                    damage = 6,
                    hitstun = 25,
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
