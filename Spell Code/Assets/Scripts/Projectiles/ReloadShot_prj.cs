using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class ReloadShot_prj : BaseProjectile
{
    
    protected override void InitializeDefaults()
    {
        projName = "Reload Shot";
        hSpeed = Fixed.FromInt(1);
        vSpeed = Fixed.FromInt(0);
        lifeSpan = 0;
        deleteOnHit = false;
        animFrames = new AnimFrames(new List<int>(), new List<int>(){ 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 }, false);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
        this.hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 5); // Set horizontal speed based on facing direction
    }
    public override void LoadProjectile()
    {

        deleteOnHit = false;
        projectileHitboxes = new HitboxGroup[3];

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
                    yKnockback = 10,
                    damage = 25,
                    hitstun = 30,
                    attackLvl = 2,
                    sweetSpot = true
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[2] = new HitboxGroup
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
                    yKnockback = 10,
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
                0,
                animFrames.frameLengths.Take(2).Sum()+1
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(2).Sum(),
                animFrames.frameLengths.Take(18).Sum()

            }
        };
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();
        
        //okay so this logic is a bit wonky to understand but basically if the ball hits something,
        //it switches to the non-hitting hitbox group, sets its horizontal speed to 0,
        //and then waits until the animation is done to delete itself.
        if (logicFrame == animFrames.frameLengths.Take(18).Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
        //this basically checks if the projectile hit something
        if (playerIgnoreArr.Any(ignore => ignore))
        {
            hSpeed = Fixed.FromInt(0);

            playerIgnoreArr = new bool[4] { false, false, false, false };
            logicFrame = animFrames.frameLengths.Take(18).Sum()+1; //set the logic frame to the start of the end animation
        }
    }
}
