using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GiftOfPrometheus_Projectile : BaseProjectile
{
    
    public GiftOfPrometheus_Projectile()
    {
        projName = "GiftOfPrometheus";
        lifeSpan = 120; // lasts for 120 logic frames
        deleteOnHit = false;
        animFrames = new AnimFrames(new List<int>(), new List<int>(){ 6, 6, 6, 6, 6, 6, 6, 6, 4, 4, 4, 4, 4, 4 }, false);
    }
    
    public override void SpawnProjectile(bool facingRight, Vector2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
        this.hSpeed = (facingRight ? 1 : -1) * 2; // Set horizontal speed based on facing direction
    }
    public override void LoadProjectile()
    {
        base.LoadProjectile();

        deleteOnHit = false;
        projectileHitboxes = new HitboxGroup[2];

        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>(),
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[1] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -30,
                    yOffset = 30,
                    width = 60,
                    height = 60,
                    xKnockback = 5,
                    yKnockback = 15,
                    damage = 34,
                    hitstun = 30,
                    attackLvl = 2,
                    cancelOptions = new List<int> { } // No cancel options
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();
        //okay so this logic is a bit wonky to understand but basically if the ball hits something,
        //it switches to the non-hitting hitbox group, sets its horizontal speed to 0,
        //and then waits until the animation is done to delete itself.
        if (logicFrame == animFrames.frameLengths.Take(8).Sum() + 1)
        {
            hSpeed = 0;
        }
        if (logicFrame >= animFrames.frameLengths.Take(11).Sum() + 1)
        {
            activeHitboxGroupIndex = 1;
        }
        if (logicFrame >= animFrames.frameLengths.Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
        //this basically checks if the projectile hit something
    }
}
