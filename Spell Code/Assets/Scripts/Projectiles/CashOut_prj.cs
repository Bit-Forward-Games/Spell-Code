using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class CashOut_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Cash Out";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 15; // lasts for 20 logic frames
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 2, 2, 2, 2 }, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);

        //this is the base speed for the projectile before applying angle factors
        int fixedSpeed = 8;

        //the base spawn offset is 10,15 - so in order to get the same projectile to have 3 different trajectories, we base the trajectory on the spawn offset y value relative to 15
        this.hSpeed = Fixed.FromFloat((facingRight ? 1 : -1) * (fixedSpeed-Mathf.Abs(spawnOffset.Y.ToFloat() -36)));

        //the vspd of the projectile is based on the spawn offset, so if the projectile spawns higher on the character it is more angled up
        this.vSpeed = Fixed.FromFloat((spawnOffset.Y.ToFloat() -36)* 3);
        
        
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
                    xOffset = -6,
                    yOffset = 6,
                    width = 12,
                    height = 12,
                    xKnockback = 1,
                    yKnockback = 5,
                    damage = 15,
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
