using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AbaddonUppercutEnhanced_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Abaddon Uppercut Enhanced";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 0;
        maxMultiHitCount = 2;
        multiHitCooldown = 5;
        fadeIn = true;
        fadeOut = true;
        meleeProjectile = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 2, 2, 4, 4, 3, 2 }, false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset, "Abaddon Uppercut Enhanced");
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
                    xOffset = -30,
                    yOffset = 42,
                    width = 35*2,
                    height = 42*2,
                    xKnockback = 2,
                    yKnockback = 13,
                    damage = 10,
                    hitstun = 30,
                    attackLvl = 2,
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
                animFrames.frameLengths.Take(5).Sum()
            }
        };
        base.LoadProjectile();
    }

    
}
