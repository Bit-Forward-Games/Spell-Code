using UnityEngine;

public class SkillshotSlash : SpellData
{
    public SkillshotSlash()
    {
        spellName = "SkillshotSlash";
        brands = new Brand[]{ Brand.VWave };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_0011_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[1] { ProcCondition.ActiveOnHit };
        projectilePrefabs = new GameObject[1];
        description = "Medium-range slash.\nHit sweet-spot: Enter Flow State<sprite name=\"FlowState\">.\nDeals increased damage in Flow State <sprite name=\"FlowState\">.";

    }

   
  
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnHit:
                //ActiveOnHit proc: when this spell hits an enemy, deal extra damage if in Flow State
                if (owner.flowState > 0)
                {
                    defender.TakeEffectDamage(10, owner);
                }
                //if we hit the sweet spot, set flow state to 600 (10 seconds worth)
                if (defender.hitboxData.sweetSpot)
                {
                    owner.flowState = PlayerController.maxFlowState;
                    owner.SpawnToast("FLOW STATE!", Color.green);
                }
                break;
            default:
                break;
        }
    }
}
