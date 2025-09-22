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
        animFrames = new AnimFrames(new List<int>(), new List<int>(){ 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 }, false);
    }
    
    public override void SpawnProjectile(bool facingRight, Vector2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        this.hSpeed = (facingRight ? 1 : -1) * 3; // Set horizontal speed based on facing direction
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();
        if(logicFrame >= animFrames.frameLengths.Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
    }
}
