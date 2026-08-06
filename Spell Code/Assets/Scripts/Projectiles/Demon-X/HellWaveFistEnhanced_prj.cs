using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class HellWaveFistEnhanced_prj : BaseProjectile
{
    private const int speed = 6;
    protected override void InitializeDefaults()
    {
        projName = "Hell Wave Fist Enhanced";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 45;
        maxMultiHitCount = 3;
        multiHitCooldown = 5;
        deleteOnHit = true;
        fadeIn = true;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4, 4, 4}, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 6); // Set horizontal speed based on facing direction
    }

    public override void LoadProjectile()
    {
        deleteOnHit = true;
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
                    xKnockback = 3,
                    yKnockback = 5,
                    damage = 6,
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
                8
            },
            endFrames = new List<int>
            {
                lifeSpan
            }
        };
        base.LoadProjectile();
    }
}
