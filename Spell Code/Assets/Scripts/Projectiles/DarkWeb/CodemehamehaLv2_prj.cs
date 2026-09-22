using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class CodemehamehaLv2_prj : BaseProjectile
{
    protected override void InitializeDefaults()
    {
        projName = "Codemehameha Lv2";
        //hSpeed = 3f;
        //vSpeed = 0f;
        //lifeSpan = 35; // lasts for 300 logic frames
        //deleteOnHit = true;
        meleeProjectile = true;
        fadeIn = true;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4, 4, 4}, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset, "", useAbsolutePosition);
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
                    xOffset = 0,
                    yOffset = 9*2,
                    width = 116*2,
                    height = 18*2,
                    xKnockback = 6,
                    yKnockback = 4,
                    damage = 25,
                    hitstun = 45,
                    attackLvl = 3,
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
        frameData = new FrameData
        {
            startFrames = new List<int>
            {
                animFrames.frameLengths.Take(2).Sum()+1
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(6).Sum()
            }
        };
        base.LoadProjectile();
    }
}
