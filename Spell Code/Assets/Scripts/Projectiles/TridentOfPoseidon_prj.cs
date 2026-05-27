using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using Steamworks.ServerList;

public class TridentOfPoseidon_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Trident Of Poseidon";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 0;
        meleeProjectile = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 3, 3, 3, 3 }, false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
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
                    xOffset = -20,
                    yOffset = 0,
                    width = 20*2,
                    height = 50*2,
                    xKnockback = 3,
                    yKnockback = -7,
                    damage = 15,
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
                animFrames.frameLengths.Sum()
            }
        };
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();

        //this logic allows infinite looping even with a beginning anim
        if(logicFrame == animFrames.frameLengths.Sum() - 1)
        {
            logicFrame = animFrames.frameLengths.Take(3).Sum();
        }

        if (logicFrame >= animFrames.frameLengths.Take(3).Sum())
        {
            if (owner.isGrounded)
            {
                logicFrame = animFrames.frameLengths.Sum();
            }
        }
        
    }
}
