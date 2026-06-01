using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BladeOfAres_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Blade Of Ares";
        lifeSpan = 0;
        meleeProjectile = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 2, 2, 3, 5, 5, 5}, false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
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
                    xOffset = -13*2,
                    yOffset = 37*2,
                    width = 35*2,
                    height = 17*2,
                    xKnockback = 1,
                    yKnockback = 8,
                    damage = 15,
                    hitstun = 30,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = 3*2,
                    yOffset = 29*2,
                    width = 42*2,
                    height = 45*2,
                    xKnockback = 5,
                    yKnockback = 5,
                    damage = 15,
                    hitstun = 20,
                    attackLvl = 2
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
