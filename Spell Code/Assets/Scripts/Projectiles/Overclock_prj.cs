using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class Overclock_prj : BaseProjectile
{
    public Overclock_prj()
    {
        projName = "Overclock";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 30; // lasts for 300 logic frames

        animFrames = new AnimFrames(new List<int>(), new List<int>() { 2, 2, 2, 2, 2}, false);

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
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
                    xOffset = -64,
                    yOffset = -64,
                    width = 128,
                    height = 128,
                    xKnockback = 2,
                    yKnockback = 2,
                    damage = 10,
                    hitstun = 20,
                    attackLvl = 2,
                    //cancelOptions = new List<int> { } // No cancel options
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
        base.ProjectileUpdate();
        if (logicFrame >= animFrames.frameLengths.Take(1).Sum() + 1)
        {
            activeHitboxGroupIndex = 1;
        }
        if (logicFrame >= animFrames.frameLengths.Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }

    }
}
