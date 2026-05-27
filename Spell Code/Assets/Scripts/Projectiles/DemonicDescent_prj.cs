using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class DemonicDescent_prj : BaseProjectile
{
    protected override void InitializeDefaults()
    {
        projName = "Demonic Descent";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 60; // lasts for 300 logic frames
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 3, 3}, false);
        ignoreBrand = true;
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
                    xOffset = -20*2,
                    yOffset = 20*2,
                    width = 40*2,
                    height = 40*2,
                    xKnockback = 0,
                    yKnockback = 0,
                    damage = 2,
                    hitstun = 0,
                    attackLvl = 1,
                    basicAttackHitbox = true
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

        //okay so this logic is a bit wonky to understand but basically if the ball hits something,
        //it switches to the non-hitting hitbox group, sets its horizontal speed to 0,
        //and then waits until the animation is done to delete itself.
        if (logicFrame >= animFrames.frameLengths.Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
        activeHitboxGroupIndex = (byte)(logicFrame > animFrames.frameLengths.Take(2).Sum()?1:0);

    }
}
