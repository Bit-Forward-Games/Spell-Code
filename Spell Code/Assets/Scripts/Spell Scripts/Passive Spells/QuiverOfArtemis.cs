using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class QuiverOfArtemis : SpellData
{
    public const ushort demonAuraThreshold = 50;
    public QuiverOfArtemis()
    {
        spellName = "Quiver Of Artemis";
        cooldown = 120;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnSlide};
        brands = new Brand[1] { Brand.Killeez };
        projectilePrefabs = new GameObject[3];
        description = $"Your slide fires a volley of arrows.\nThe arrows gain duration based on Reps<sprite name=\"Reps\">.";
        spawnOffsetX = 15;
        spawnOffsetY = 0;
    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;

        if (owner.state == PlayerState.Slide && projectileInstances[0].activeSelf && projectileInstances[0].GetComponent<BaseProjectile>().logicFrame == 10)
        {
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), projectileInstances[0].GetComponent<BaseProjectile>().facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
        }

        if (owner.state == PlayerState.Slide && projectileInstances[1].activeSelf && projectileInstances[1].GetComponent<BaseProjectile>().logicFrame == 10)
        {
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), projectileInstances[0].GetComponent<BaseProjectile>().facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
        }

        //basic cooldown handling
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }

    }
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnSlide:
                if(cooldownCounter <= 0)
                {
                    cooldownCounter = cooldown;
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[owner.demonAura >= demonAuraThreshold?1:0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                }
                break;
            default:
                break;
        }
    }

}
