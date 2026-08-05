using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class ArmoryOfHephaestusHammer_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Armory Of Hephaestus Hammer";
        lifeSpan = 0;
        meleeProjectile = true;
        fadeIn = true;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2, 2}, false);
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
                    xOffset = 5*2,
                    yOffset = 42*2,
                    width = 28*2,
                    height = 15*2,
                    xKnockback = 1,
                    yKnockback = 8,
                    damage = 25,
                    hitstun = 25,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = 15*2,
                    yOffset = 32*2,
                    width = 24*2,
                    height = 41*2,
                    xKnockback = 5,
                    yKnockback = 5,
                    damage = 25,
                    hitstun = 25,
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
                animFrames.frameLengths.Take(6).Sum()+1
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(7).Sum()
            }
        };
        base.LoadProjectile();
    }
}
