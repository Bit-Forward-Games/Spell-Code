using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class DemonTriggerBlade_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Demon Trigger Blade";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 0;
        meleeProjectile = true;
        fadeIn = true;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 3, 3, 3 }, false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset, "Demon Trigger Blade");
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
                    xOffset = -26*2,
                    yOffset = 18*2,
                    width = 66*2,
                    height = 12*2,
                    xKnockback = 4,
                    yKnockback = -3,
                    damage = 15,
                    hitstun = 30,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -4*2,
                    yOffset = 6*2,
                    width = 50*2,
                    height = 6*2,
                    xKnockback = 4,
                    yKnockback = -3,
                    damage = 15,
                    hitstun = 15,
                    attackLvl = 2,
                    sweetSpot = true
                }
            },
            hitbox3 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -26*2,
                    yOffset = 36*2,
                    width = 13*2,
                    height = 30*2,
                    xKnockback = 4,
                    yKnockback = -3,
                    damage = 15,
                    hitstun = 15,
                    attackLvl = 2
                }
            },
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>(),
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        frameData = new FrameData
        {
            startFrames = new List<int>
            {
                animFrames.frameLengths.Take(3).Sum()+1
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(4).Sum()
            }
        };
        base.LoadProjectile();
    }

    
}
