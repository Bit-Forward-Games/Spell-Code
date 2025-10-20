using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Ninja_Build_Blast_prj : BaseProjectile
{
    
    public Ninja_Build_Blast_prj()
    {
        projName = "Ninja_Build_Blast";
        hSpeed = 1f;
        vSpeed = 0f;
        lifeSpan = 120; // lasts for 120 logic frames
        deleteOnHit = false;
        animFrames = new AnimFrames(new List<int>(), new List<int>(){ 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 }, false);
    }
    
    public override void SpawnProjectile(bool facingRight, Vector2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
        this.hSpeed = (facingRight ? 1 : -1) * 3; // Set horizontal speed based on facing direction
    }
    public override void LoadProjectile()
    {

        deleteOnHit = false;
        projectileHitboxes = new HitboxGroup[2];

        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -20,
                    yOffset = 20,
                    width = 40,
                    height = 40,
                    xKnockback = 5,
                    yKnockback = 15,
                    damage = 20,
                    hitstun = 30,
                    attackLvl = 2,
                    //cancelOptions = new List<int> { } // No cancel options
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[1] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>(),
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();
        //okay so this logic is a bit wonky to understand but basically if the ball hits something,
        //it switches to the non-hitting hitbox group, sets its horizontal speed to 0,
        //and then waits until the animation is done to delete itself.
        if (logicFrame == animFrames.frameLengths.Take(8).Sum() || logicFrame >= animFrames.frameLengths.Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
        //this basically checks if the projectile hit something
        if (playerIgnoreArr.Any(ignore => ignore))
        {
            hSpeed = 0;
            activeHitboxGroupIndex = 1;

            playerIgnoreArr = new bool[4] { false, false, false, false };
            logicFrame = animFrames.frameLengths.Take(8).Sum()+1; //set the logic frame to the start of the end animation
        }
    }
}
