using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class NoScopeShot : SpellData
{

    public NoScopeShot()
    {
        spellName = "No-Scope Shot";
        cooldown = 60;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] {ProcCondition.OnCastBasic };
        brands = new Brand[1] { Brand.VWave };
        description = "While in Flow State<sprite name=\"FlowState\">, basic attack consumes some Flow State<sprite name=\"FlowState\"> to become a long-range shot.";

        projectilePrefabs = new GameObject[1];
    }

    public override void SpellUpdate()
    {
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
            case ProcCondition.OnCastBasic:

                if (owner.flowState > 0 && cooldownCounter <= 0)
                {
                    owner.basicSpawnOverride = true;
                    owner.flowState = (ushort)Mathf.Max(owner.flowState - 60,0);
                    cooldownCounter = cooldown;
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                }
                break;
            default:
                break;
        }
    }
}
