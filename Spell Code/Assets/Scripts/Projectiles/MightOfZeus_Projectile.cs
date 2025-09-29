using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MightOfZeus_Projectile : BaseProjectile
{

    public MightOfZeus_Projectile()
    {
        projName = "MightOfZeus";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 30; // lasts for 300 logic frames

        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4 }, true);

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, Vector2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
    }

    public override void LoadProjectile()
    {
        base.LoadProjectile();
        projectileHitboxes = new HitboxGroup[1];
        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = 0,
                    yOffset = 3,
                    width = 80,
                    height = 6,
                    xKnockback = 0,
                    yKnockback = 1,
                    damage = 5,
                    hitstun = 30,
                    attackLvl = 1,
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
        if (logicFrame >= animFrames.frameLengths.Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
        //this basically checks if the projectile hit something
        
    }
}
