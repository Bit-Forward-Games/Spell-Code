using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AsuranBlades_Projectile : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Asuran Blades";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 30;
        deleteOnHit = true;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 2, 2, 2, 2 }, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset);

        //this is the base speed for the projectile before applying angle factors
        int fixedSpeed = 4;

        //the base spawn offset is 10,15 - so in order to get the same projectile to have 3 different trajectories, we base the trajectory on the spawn offset y value relative to 15
        this.vSpeed = Fixed.FromInt(-fixedSpeed); // all projectiles will go downwards at the same speed

        Fixed angleFactor30Degrees = Fixed.FromFloat(1.732f);

        //determine horizontal speed based on spawn offset
        if (spawnOffset.Y - new Fixed(15) > Fixed.FromInt(1))
        {
            //top projectile - -30 degree angle
            this.hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * (fixedSpeed+3));
        }
        else if(spawnOffset.Y - new Fixed(15) < -Fixed.FromInt(1))
        {
            //bottom projectile - -60 degree angle
            this.hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * (fixedSpeed-3));
        }
        else
        {
            //middle projectile - -45 degree angle
            this.hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * fixedSpeed);
        }
        
        // ownerSpell can be null after a reflect reassigns the owner; treat a missing spell as a zero
        // spawn offset rather than throwing inside the sim. Same value on every peer, so the branch
        // taken below stays identical across machines.
        int ownerSpawnOffsetX = ownerSpell != null ? ownerSpell.spawnOffsetX : 0;

        // Compared entirely in Fixed rather than via ToFloat(). This branch picks the blade's vertical
        // angle, so it feeds the simulation and ToFloat() puts IEEE rounding on the deciding value,
        // which is the class of thing that diverges across platforms. Fixed32 is bit-exact everywhere.
        Fixed spawnOffsetDelta = spawnOffset.X - Fixed.FromInt(ownerSpawnOffsetX);

        if(spawnOffsetDelta > Fixed.FromInt(1))
        {
            this.vSpeed = Fixed.FromInt(-fixedSpeed/2);
        }
        else if(spawnOffsetDelta > Fixed.FromInt(0))
        {
            this.vSpeed = Fixed.FromInt(0);
        }
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
                    xOffset = -10,
                    yOffset = 10,
                    width = 20,
                    height = 20,
                    xKnockback = 1,
                    yKnockback = 5,
                    damage = 15,
                    hitstun = 20,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        base.LoadProjectile();
    }
}
