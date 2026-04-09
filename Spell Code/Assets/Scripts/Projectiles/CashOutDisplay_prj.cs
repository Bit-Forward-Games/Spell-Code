using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class CashOutDisplay_prj : BaseProjectile
{
    
    protected override void InitializeDefaults()
    {
        projName = "CashOutDisplay_prj";
        lifeSpan = 15; // lasts for 120 logic frames
        deleteOnHit = false;
        animFrames = new AnimFrames(new List<int>(), new List<int>(){ 3, 3, 3, 3}, true);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
        vSpeed = Fixed.FromInt(8); // No vertical speed
    }
    public override void LoadProjectile()
    {

        deleteOnHit = false;
        projectileHitboxes = new HitboxGroup[1];
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

        vSpeed -= owner.gravity; // Apply gravity to the vertical speed
    }
}
