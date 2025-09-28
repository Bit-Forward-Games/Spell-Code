using System.Collections.Generic;
using UnityEngine;

public class CodeE_BasicProjectile : BaseProjectile
{

    public CodeE_BasicProjectile()
    {
        projName = "CodeE_Basic_Projectile";
        //hSpeed = 3f;
        //vSpeed = 0f;
        lifeSpan = 15; // lasts for 300 logic frames

        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4 }, true);

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, Vector2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        this.hSpeed = (facingRight ? 1 : -1) * 6; // Set horizontal speed based on facing direction
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
                    xOffset = -5,
                    yOffset = 5,
                    width = 20,
                    height = 10,
                    xKnockback = 3,
                    yKnockback = 7,
                    damage = 5,
                    hitstun = 15,
                    attackLvl = 1,
                    cancelOptions = new List<int> { } // No cancel options
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
    }
}
