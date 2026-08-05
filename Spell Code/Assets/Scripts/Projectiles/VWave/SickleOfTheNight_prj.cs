using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SickleOfTheNight_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Sickle Of The Night";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 0;
        fadeIn = true;
        fadeOut = true;
        meleeProjectile = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 3, 4, 4, 4, 4}, false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
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
                    xOffset = 31*2,
                    yOffset = -17*2,
                    width = 20*2,
                    height = 20*2,
                    xKnockback = 6,
                    yKnockback = -2,
                    damage = 20,
                    hitstun = 25,
                    attackLvl = 2,
                    sweetSpot = true
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -6*2,
                    yOffset = 48*2,
                    width = 49*2,
                    height = 12*2,
                    xKnockback = 3,
                    yKnockback = 6,
                    damage = 15,
                    hitstun = 15,
                    attackLvl = 2,
                }
            },
            hitbox3 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = 40*2,
                    yOffset = 40*2,
                    width = 20*2,
                    height = 58*2,
                    xKnockback = 8,
                    yKnockback = 2,
                    damage = 15,
                    hitstun = 15,
                    attackLvl = 2,
                }
            },
            hitbox4 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -18*2,
                    yOffset = -26*2,
                    width = 52*2,
                    height = 15*2,
                    xKnockback = 2,
                    yKnockback = -4,
                    damage = 15,
                    hitstun = 15,
                    attackLvl = 2,
                }
            }
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
                animFrames.frameLengths.Take(5).Sum()+1
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(6).Sum()
            }
        };
        base.LoadProjectile();
    }

    
}
