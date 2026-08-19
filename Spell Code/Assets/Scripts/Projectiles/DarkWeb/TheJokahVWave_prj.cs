using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class TheJokahVWave_prj : BaseProjectile
{
    
    protected override void InitializeDefaults()
    {
        projName = "The Jokah VWave";
        //lifeSpan = 15;
        deleteOnHit = false;
        fadeOut = true;
        fadeIn = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>(){ 3, 3, 3, 3, 3, 3, 3, 3}, true);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
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
