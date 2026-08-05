using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BrimstoneCycloneKick_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Brimstone Cyclone Kick";
        //hSpeed = 3f;
        //vSpeed = 0f;
        multiHitCooldown = 8;
        maxMultiHitCount = 3;
        lifeSpan = 30;
        fadeIn = true;
        fadeOut = true;
        meleeProjectile = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 3 }, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    // public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    // {
    //     base.SpawnProjectile(facingRight, spawnOffset);
    // }

    public override void LoadProjectile()
    {
        projectileHitboxes = new HitboxGroup[2];
        projectileHitboxes[1] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -48,
                    yOffset = 27*2,
                    width = 48*2,
                    height = 21*2,
                    xKnockback = 4,
                    yKnockback = 5,
                    damage = 5,
                    hitstun = 15,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -26,
                    yOffset = 7*2,
                    width = 26*2,
                    height = 8*2,
                    xKnockback = 4,
                    yKnockback = 5,
                    damage = 5,
                    hitstun = 15,
                    attackLvl = 2,
                }
            },
            hitbox3 = new List<HitboxData>{
                new HitboxData
                {
                    xOffset = 12,
                    yOffset = -1*2,
                    width = 12*2,
                    height = 6*2,
                    xKnockback = 4,
                    yKnockback = 5,
                    damage = 5,
                    hitstun = 15,
                    attackLvl = 2,
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
                4
            },
            endFrames = new List<int>
            {
                lifeSpan - 4
            }
        };
        base.LoadProjectile();
    }

    
}
