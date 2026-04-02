using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class NoScopeShot_Prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "No Scope Shot";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 20; // lasts for 300 logic frames
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 2, 2, 2, 2 }, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        this.hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 16); // Set horizontal speed based on facing direction
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
                    xOffset = -32,
                    yOffset = 8,
                    width = 64*2,
                    height = 8*2,
                    xKnockback = 5,
                    yKnockback = 8,
                    damage = 15,
                    hitstun = 10,
                    attackLvl = 1,
                    basicAttackHitbox = true,
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        base.LoadProjectile();
    }
}
