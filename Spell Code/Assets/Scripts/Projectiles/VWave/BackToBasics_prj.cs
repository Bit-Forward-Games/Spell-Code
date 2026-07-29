using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BackToBasics_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Back To Basics";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 8; // lasts for 300 logic frames
        deleteOnHit = true;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 2, 2, 2, 2 }, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset, "", useAbsolutePosition);
        this.hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 6); // Set horizontal speed based on facing direction
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
                    xOffset = -20,
                    yOffset = 14,
                    width = 40,
                    height = 28,
                    xKnockback = 3,
                    yKnockback = 2,
                    damage = 10,
                    hitstun = 15,
                    attackLvl = 1,
                    basicAttackHitbox = true,
                    sweetSpot = true
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        base.LoadProjectile();
    }
}
