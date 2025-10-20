using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SkillshotSlash_Projectile : BaseProjectile
{

    public SkillshotSlash_Projectile()
    {
        projName = "SkillshotSlash";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 30; // lasts for 300 logic frames

        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 3, 3, 3, 3, 3 }, false);

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, Vector2 spawnOffset)
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
                    xOffset = -5*2,
                    yOffset = 12*2,
                    width = 65*2,
                    height = 27*2,
                    xKnockback = 2,
                    yKnockback = 3,
                    damage = 10,
                    hitstun = 15,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = 60*2,
                    yOffset = 12*2,
                    width = 14*2,
                    height = 19*2,
                    xKnockback = 1,
                    yKnockback = 5,
                    damage = 15,
                    hitstun = 25,
                    attackLvl = 3,
                    sweetSpot = true
                }
            },
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
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();
        //okay so this logic is a bit wonky to understand but basically if the ball hits something,
        //it switches to the non-hitting hitbox group, sets its horizontal speed to 0,
        //and then waits until the animation is done to delete itself.
        if (logicFrame >= animFrames.frameLengths.Take(2).Sum()+1 && logicFrame <= animFrames.frameLengths.Take(3).Sum())
        {
            activeHitboxGroupIndex = 1;
        }
        else
        {
            activeHitboxGroupIndex = 0;
        }
        if (logicFrame >= animFrames.frameLengths.Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
        //this basically checks if the projectile hit something
        
    }
}
