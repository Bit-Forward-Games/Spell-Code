using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AegisOfAthenaDisplay_prj : BaseProjectile
{
    
    protected override void InitializeDefaults()
    {
        projName = "AegisOfAthenaDisplay_prj";
        //lifeSpan = 15; // lasts for 120 logic frames
        deleteOnHit = false;
        meleeProjectile = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>(){ 3, 3, 3, 3, 3, 3, 3, 3}, true);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
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

}
