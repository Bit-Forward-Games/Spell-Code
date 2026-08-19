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
        // Sickle passes the marked player's world position. Dropping this flag made the base class
        // add the caster's position again, which is why a Jokah-created mark appeared at a fixed,
        // incorrect location until something else moved it.
        base.SpawnProjectile(facingRight, spawnOffset, "Sickle Of The Night Mark", useAbsolutePosition);
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

        // BaseProjectile can delete and disable this object when its lifespan ends. Do not mutate a
        // pooled projectile after that reset.
        if (!gameObject.activeSelf)
        {
            return;
        }

        // The ordinary Sickle is updated through PlayerController's inventory spell loop, but the
        // enhanced Sickle created by The Jokah lives in extraSpells and does not receive that loop's
        // OnUpdate callback. Keep the mark's presentation lifecycle with the mark itself so both
        // owners follow the exact same deterministic path.
        SickleOfTheNight sourceSpell = ownerSpell as SickleOfTheNight;
        PlayerController markedPlayer = sourceSpell != null
            && sourceSpell.targetPID >= 0
            && GameManager.Instance != null
                ? GameManager.Instance.GetPlayerByPID(sourceSpell.targetPID)
                : null;

        bool markStillArmed = sourceSpell != null && sourceSpell.OwnsPendingBasicOverride();
        if (markStillArmed && markedPlayer != null && markedPlayer.isAlive)
        {
            position = markedPlayer.position;
            facingRight = markedPlayer.facingRight;
            return;
        }

        // The target died/disconnected, the follow-up basic was used, the override was replaced, or
        // rollback could no longer resolve the copied spell. Clear the owning state and use the same
        // ten-frame fade-out as the normal Sickle mark.
        if (sourceSpell != null)
        {
            sourceSpell.targetPID = -1;
        }
        if(logicFrame < lifeSpan-11)
        {
            logicFrame = lifeSpan - 10;
        }
    }

}
