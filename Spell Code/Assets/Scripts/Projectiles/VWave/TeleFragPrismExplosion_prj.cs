using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class TeleFragPrismExplosion_prj : BaseProjectile
{
    protected override void InitializeDefaults()
    {
        projName = "Tele-Frag Prism Explosion";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 0; // lasts for 300 logic frames
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 3, 3}, false);
        ignoreBrand = true;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        base.SpawnProjectile(facingRight, spawnOffset, "Tele-Frag Prism Explosion");
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
                    xKnockback = 4,
                    yKnockback = 2,
                    damage = 15,
                    hitstun = 15,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -16,
                    yOffset = 16,
                    width = 32,
                    height = 32,
                    xKnockback = 4,
                    yKnockback = 4,
                    damage = 15,
                    hitstun = 20,
                    attackLvl = 2,
                    sweetSpot = true
                }
            },
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
        frameData = new FrameData
        {
            startFrames = new List<int>
            {
                animFrames.frameLengths.Take(2).Sum()+1
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Sum()
            }
        };
        base.LoadProjectile();
    }
   
}
