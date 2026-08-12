using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SpartanBeamGun_prj : BaseProjectile
{
    protected override void InitializeDefaults()
    {
        projName = "Spartan Beam Gun";
        //hSpeed = 3f;
        //vSpeed = 0f;
        //lifeSpan = 35; // lasts for 300 logic frames
        //deleteOnHit = true;
        meleeProjectile = true;
        fadeIn = true;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4}, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        hSpeed = Fixed.FromInt(0); // Set horizontal speed based on facing direction
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
                    xOffset = 29*2,
                    yOffset = 4*2,
                    width = 25*2,
                    height = 7*2,
                    xKnockback = 10,
                    yKnockback = 10,
                    damage = 30,
                    hitstun = 45,
                    attackLvl = 4,
                    sweetSpot = true
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = 38*2,
                    yOffset = 15*2,
                    width = 16*2,
                    height = 29*2,
                    xKnockback = 8,
                    yKnockback = 6,
                    damage = 20,
                    hitstun = 30,
                    attackLvl = 4,
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
                animFrames.frameLengths.Take(12).Sum()+1
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(13).Sum()
            }
        };
        base.LoadProjectile();
    }
}
