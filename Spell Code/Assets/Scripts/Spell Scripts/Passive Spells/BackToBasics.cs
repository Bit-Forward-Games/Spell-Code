using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BackToBasics : SpellData
{
    public const int flowStateCost = 60;
    public const int flowStateIncrease = 240;
    public BackToBasics()
    {
        spellName = "Back To Basics";
        cooldown = 60;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] {ProcCondition.OnUpdate, ProcCondition.ActiveOnHit };
        brands = new Brand[1] { Brand.VWave };
        description = $"While not in Flow State<sprite name=\"FlowState\">, basic attack gains a sweet spot, providing {flowStateIncrease/60} seconds of Flow State<sprite name=\"FlowState\"> when hitting near the end.";

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
            case ProcCondition.OnUpdate:
                if( cooldownCounter == 0 && 
                    owner.flowState == 0 &&
                    owner.basicProjectileInstance.activeSelf && 
                    owner.basicProjectileInstance.GetComponent<BaseProjectile>().logicFrame == owner.basicProjectileInstance.GetComponent<BaseProjectile>().lifeSpan - 6)
                {
                    cooldownCounter = cooldown;
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(),
                    owner.basicProjectileInstance.GetComponent<BaseProjectile>().facingRight,
                    owner.basicProjectileInstance.GetComponent<BaseProjectile>().position,true);
                    
                    ProjectileManager.Instance.DeleteProjectile(owner.basicProjectileInstance.GetComponent<BaseProjectile>());
                }

                break;
            case ProcCondition.ActiveOnHit:
                owner.SpawnToast($"+{flowStateIncrease/60}SEC FLOW STATE", GameManager.colors["green"]);
                owner.flowState = (ushort)Mathf.Min(owner.flowState + flowStateIncrease,FlowState.maxFlowState);
                break;
            default:
                break;
        }
    }
}
