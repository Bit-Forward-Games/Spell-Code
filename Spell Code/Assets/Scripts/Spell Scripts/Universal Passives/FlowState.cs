using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class FlowState : SpellData
{
    public static ushort maxFlowState = 600;
    public static ushort flowstateExtraDamage = 10;
    public FlowState()
    {
        spellName = "Flow State";
        brands = new Brand[]{ Brand.VWave };
        cooldown = 1;
        priorityOverride = 3;
        spellType = SpellType.Universal;
        procConditions = new ProcCondition[1] { ProcCondition.OnHitSpell};
        description = $"Hit the Red part of VWave Spellcodes to enter Flow State<sprite name=\"FlowState\"> for {maxFlowState/60} seconds.\nSpellcodes deal increased damage while in Flow State<sprite name=\"FlowState\">.";

    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnHitSpell:
                // deal extra damage based on demon aura
                if(owner.flowState > 0)
                {
                    defender.TakeEffectDamage(flowstateExtraDamage, owner, GameManager.colors["green"]);
                }
                

                //enter flowstate if you hit a sweetspot on a vwave spell
                if(defender.hitboxData.parentProjectile.ownerSpell.brands[0] == Brand.VWave && defender.hitboxData.sweetSpot && !defender.hitboxData.parentProjectile.ignoreBrand)
                {
                    owner.flowState = maxFlowState;
                    owner.SpawnToast("FLOW STATE", GameManager.colors["green"]);

                    //Play the Sweet Spot SFX
                    SFX_Manager.Instance.PlaySound(Sounds.SWEET_SPOT_HIT);
                }
                
                break;
            default:
                break;
        }

        
    }
}
