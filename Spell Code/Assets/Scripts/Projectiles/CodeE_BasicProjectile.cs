using System.Collections.Generic;
using UnityEngine;

public class CodeE_BasicProjectile : BaseProjectile
{

    public CodeE_BasicProjectile()
    {
        projName = "CodeE_Basic_Projectile";
        hSpeed = 1f;
        vSpeed = 0f;
        lifeSpan = 300; // lasts for 300 logic frames

        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4 }, true);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public override void SpawnProjectile(bool facingRight, Vector2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        this.hSpeed = (facingRight ? 1 : -1) * 2; // Set horizontal speed based on facing direction
    }
}
