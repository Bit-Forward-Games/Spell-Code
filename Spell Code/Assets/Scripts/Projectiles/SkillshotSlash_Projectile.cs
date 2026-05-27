using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SkillshotSlash_Projectile : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Skillshot Slash";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 0;
        meleeProjectile = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 3, 3, 3, 3, 3 }, false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
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
                    xOffset = -5*2,
                    yOffset = 12*2,
                    width = 65*2,
                    height = 27*2,
                    xKnockback = 4,
                    yKnockback = 3,
                    damage = 15,
                    hitstun = 15,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = 60*2,
                    yOffset = 12*2,
                    width = 14*2,
                    height = 19*2,
                    xKnockback = 1,
                    yKnockback = 5,
                    damage = 20,
                    hitstun = 30,
                    attackLvl = 3,
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
                animFrames.frameLengths.Take(3).Sum()
            }
        };
        base.LoadProjectile();
    }

    
}
