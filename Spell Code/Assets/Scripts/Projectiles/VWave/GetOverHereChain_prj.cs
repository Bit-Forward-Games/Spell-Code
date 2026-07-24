using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class GetOverHereChain_prj : BaseProjectile
{
    
    protected override void InitializeDefaults()
    {
        projName = "Get Over Here Chain";
        deleteOnHit = false;
        lifeSpan = 600;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>(){1}, false);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        base.SpawnProjectile(facingRight, spawnOffset, "Get Over Here Chain");
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
