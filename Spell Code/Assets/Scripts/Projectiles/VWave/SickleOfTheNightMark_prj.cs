using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SickleOfTheNightMark_prj : BaseProjectile
{
    
    protected override void InitializeDefaults()
    {
        projName = "Sickle Of The Night Mark";
        deleteOnHit = false;
        lifeSpan = 65535; //this projectile should NOT delete itself unless deleted by its ownerSpell
        fadeOut = true;
        fadeIn = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>(){ 4, 4, 4, 4, 4, 4}, true);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset, "Sickle Of The Night Mark");
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
    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();
        if(ownerSpell.gameObject.GetComponent<SickleOfTheNight>().targetPID == -1 && logicFrame < lifeSpan-11)
        {
            logicFrame = lifeSpan - 10;
        }
    }

}
