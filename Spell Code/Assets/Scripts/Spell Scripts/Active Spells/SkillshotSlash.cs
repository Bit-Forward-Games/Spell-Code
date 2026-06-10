using UnityEngine;

public class SkillshotSlash : SpellData
{
    public SkillshotSlash()
    {
        spellName = "Skillshot Slash";
        brands = new Brand[]{ Brand.VWave };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_0011_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit };
        projectilePrefabs = new GameObject[1];
        description = "Medium-range slash.\nHitting this partially refunds cooldown when in Flow State <sprite name=\"FlowState\">.";

    }

   
  
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnHit:
                //ActiveOnHit proc: when this spell hits an enemy, refund some cooldown if in Flow State
                if (owner.flowState > 0)
                {
                    cooldownCounter-=60;
                }
                break;
            default:
                break;
        }
    }
}
