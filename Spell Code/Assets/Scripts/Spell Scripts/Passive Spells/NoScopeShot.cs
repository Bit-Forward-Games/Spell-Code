using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class NoScopeShot : SpellData
{

    public NoScopeShot()
    {
        spellName = "NoScopeShot";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[2] { ProcCondition.ActiveOnHit, ProcCondition.OnCastBasic };
        brands = new Brand[1] { Brand.VWave };
        description = "While in \"Flow State,\" replace your basic attack with a long-range sniper shot. If this spell hits, consume all \"Flow State\" and deal increased damage based on the amount consumed.";

        projectilePrefabs = new GameObject[1];
    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            //ActiveOnHit: Gain 10 Demon Aura on hitting an enemy with this spell.
            case ProcCondition.ActiveOnHit:
                int consumedFlowState = defender.GetMaxHealth()*owner.flowState/PlayerController.maxFlowState/4;
                owner.flowState = 0;
                defender.TakeEffectDamage(consumedFlowState, owner);
                break;
            case ProcCondition.OnCastBasic:

                if (owner.flowState > 0)
                {
                    owner.basicSpawnOverride = true;
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                }
                break;
            default:
                break;
        }
    }
}
