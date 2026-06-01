using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class MightOfZeus_Projectile : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Might Of Zeus";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 0;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4 }, false);
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
                    xOffset = -10,
                    yOffset = 120,
                    width = 20,
                    height = 120,
                    xKnockback = 3,
                    yKnockback = 1,
                    damage = 15,
                    hitstun = 30,
                    attackLvl = 2,
                    //cancelOptions = new List<int> { } // No cancel options
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
                animFrames.frameLengths.Take(1).Sum()+1
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(3).Sum()
            }
        };
        base.LoadProjectile();
    }
}
