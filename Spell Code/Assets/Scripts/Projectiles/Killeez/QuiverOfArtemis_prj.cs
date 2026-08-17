using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class QuiverOfArtemis_prj : BaseProjectile
{
    private const int speed = 4;
    private const int baseLifeSpan = 10;
    protected override void InitializeDefaults()
    {
        projName = "Quiver Of Artemis";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 30; // lasts for 300 logic frames
        deleteOnHit = true;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4, 4 }, true);
        ignoreBrand = true;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 4);
        vSpeed = Fixed.FromInt(-4);
        lifeSpan = (ushort)(baseLifeSpan + 5 * owner.reps);
    }

    public override void LoadProjectile()
    {
        deleteOnHit = true;
        projectileHitboxes = new HitboxGroup[1];
        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -24,
                    yOffset = 24,
                    width = 16,
                    height = 16,
                    xKnockback = 3,
                    yKnockback = 2,
                    damage = 10,
                    hitstun = 10,
                    attackLvl = 1
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -14,
                    yOffset = 14,
                    width = 16,
                    height = 16,
                    xKnockback = 3,
                    yKnockback = 2,
                    damage = 10,
                    hitstun = 10,
                    attackLvl = 1
                }
            },
            hitbox3 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = 3,
                    yOffset = 3,
                    width = 16,
                    height = 16,
                    xKnockback = 3,
                    yKnockback = 2,
                    damage = 10,
                    hitstun = 10,
                    attackLvl = 1
                }
            },
            hitbox4 = new List<HitboxData>()
        };
        base.LoadProjectile();
    }
}
