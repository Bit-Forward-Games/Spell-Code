using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class UseTheCard_crit_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Use The Card Crit";
        lifeSpan = 0;
        meleeProjectile = true;
        multiHitCooldown = 20;
        maxMultiHitCount = 2;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 2, 2, 4, 4, 4, 4, 4, 4, 4, 3}, false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        base.SpawnProjectile(facingRight, spawnOffset);
    }

    public override void LoadProjectile()
    {
        projectileHitboxes = new HitboxGroup[4];
        projectileHitboxes[1] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -23*2,
                    yOffset = 13*2,
                    width = 48*2,
                    height = 26*2,
                    xKnockback = 2,
                    yKnockback = 3,
                    damage = 10,
                    hitstun = 20,
                    attackLvl = 2,
                    ignoreEffectDamage = true
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[2] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>(),
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[3] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -24*2,
                    yOffset = 13*2,
                    width = 48*2,
                    height = 26*2,
                    xKnockback = -3,
                    yKnockback = 2,
                    damage = 10,
                    hitstun = 20,
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
                animFrames.frameLengths.Take(2).Sum()+1,
                animFrames.frameLengths.Take(4).Sum()+1,
                animFrames.frameLengths.Take(5).Sum()+1

            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(4).Sum(),
                animFrames.frameLengths.Take(5).Sum(),
                animFrames.frameLengths.Take(8).Sum()


            }
        };
        base.LoadProjectile();
    }
}
