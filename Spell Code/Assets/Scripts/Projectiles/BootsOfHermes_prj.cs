using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BootsOfHermes_prj : BaseProjectile
{
    protected override void InitializeDefaults()
    {
        projName = "Boots Of Hermes";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 0; // lasts for 300 logic frames
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 2, 2, 3, 3, 3, 3, 3}, false);
        ignoreBrand = true;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;

        //Play the Boots Of Hermes SFX
        SFX_Manager.Instance.PlaySpellcodeSound("Boots Of Hermes");
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
                    yOffset = 20,
                    width = 40,
                    height = 40,
                    xKnockback = 4,
                    yKnockback = 2,
                    damage = 15,
                    hitstun = 15,
                    attackLvl = 1,
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
   
}
