using System.Collections.Generic;
using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class DemonTriggerGuns_prj : BaseProjectile
{

    protected override void InitializeDefaults()
    {
        projName = "Demon Trigger Guns";
        meleeProjectile = true;
        fadeOut = true;
        fadeIn = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() {2, 2, 2, 2, 4, 4, 4, 2, 2, 2 }, false);
    }
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset, "Demon Trigger Guns", useAbsolutePosition);
    }
    

    public override void LoadProjectile()
    {
        projectileHitboxes = new HitboxGroup[0];
        base.LoadProjectile();
    }
}
