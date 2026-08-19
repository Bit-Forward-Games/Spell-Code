using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class WolfOfWallstreet_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Wolf Of Wallstreet";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 0;
        meleeProjectile = true;
        multiHitCooldown = 10;
        maxMultiHitCount = 2;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 }, false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
    }

    public override void LoadProjectile()
    {
        projectileHitboxes = new HitboxGroup[3];
        projectileHitboxes[1] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -2*2,
                    yOffset = 22*2,
                    width = 40*2,
                    height = 47*2,
                    xKnockback = 6,
                    yKnockback = 5,
                    damage = 10,
                    hitstun = 35,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>
            {
                {
                new HitboxData
                {
                    xOffset = -23*2,
                    yOffset = -9*2,
                    width = 43*2,
                    height = 9*2,
                    xKnockback = 7,
                    yKnockback = 5,
                    damage = 10,
                    hitstun = 35,
                    attackLvl = 2,
                }
            }
            },
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[2] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -2*2,
                    yOffset = 25*2,
                    width = 40*2,
                    height = 47*2,
                    xKnockback = 6,
                    yKnockback = 5,
                    damage = 10,
                    hitstun = 35,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>
            {
                {
                new HitboxData
                {
                    xOffset = -23*2,
                    yOffset = 28*2,
                    width = 43*2,
                    height = 9*2,
                    xKnockback = 6,
                    yKnockback = 5,
                    damage = 10,
                    hitstun = 35,
                    attackLvl = 2,
                }
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
                animFrames.frameLengths.Take(3).Sum()+1,
                animFrames.frameLengths.Take(7).Sum()+1

            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(4).Sum(),
                animFrames.frameLengths.Take(8).Sum(),
            }
        };
        base.LoadProjectile();
    }

}
