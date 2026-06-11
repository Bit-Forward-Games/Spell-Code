using BestoNet.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class RipAndTear_prj : BaseProjectile
{
    
    [NonSerialized] public ushort lifeTime = 0;
    [NonSerialized] private ushort baseLifeTime = 30;
    protected override void InitializeDefaults()
    {
        projName = "Rip And Tear";
        lifeSpan = 0;
        maxMultiHitCount = 10;
        multiHitCooldown = 15;
        meleeProjectile = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 3, 4, 4, 4, 4}, false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        lifeTime = 0;
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
                    xOffset = -25*2,
                    yOffset = 25*2,
                    width = 50*2,
                    height = 50*2,
                    xKnockback = 3,
                    yKnockback = 4,
                    damage = 5,
                    hitstun = 25,
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
                animFrames.frameLengths.Take(4).Sum()+1
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

        if (logicFrame == animFrames.frameLengths.Take(8).Sum()-1)
        {
            logicFrame = animFrames.frameLengths.Take(4).Sum() + 1; //manually loop the animation which we can do bcs this projectile's life is based on the owner's reps
        }


        if (lifeTime == owner.demonAura + animFrames.frameLengths.Take(4).Sum() + baseLifeTime || multiHitCount.Any(count => count <= 0))
        {
            logicFrame = animFrames.frameLengths.Sum();
            Array.Fill(multiHitCount, maxMultiHitCount);
        }
        lifeTime ++;
    }
    public override void ResetValues()
    {
        base.ResetValues();
        lifeTime = 0;
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(lifeTime);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        lifeTime = br.ReadUInt16();
    }
}
