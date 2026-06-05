using UnityEngine;

public class ReloadShot : SpellData
{
    public ReloadShot()
    {
        spellName = "Reload Shot";
        brands = new Brand[] { Brand.VWave };
        cooldown = 480;
        spellInput = 0b_0000_0000_0000_0000_1101_0010_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit };
        description = "Long Range Shot.\nHit this: Reset all other cooldowns.\nConsume all Flow State<sprite name=\"FlowState\"> to reduce this cooldown.";
        projectilePrefabs = new GameObject[1];
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.ActiveOnHit:
                //ActiveOnHit proc: when this spell hits an enemy, consume all Flow State to reduce cooldowns
                int currentFlow = owner.flowState;
                if (currentFlow > 0)
                {
                    // Integer-only cooldown reduction: (cooldown * flow) / maxFlow
                    cooldownCounter = (int)((long)cooldownCounter * currentFlow / FlowState.maxFlowState);
                    //if we dont hit the sweet spot, set flow state to 0
                    if (!defender.hitboxData.sweetSpot)
                    {
                        owner.flowState = 0;
                    }
                }
                //and reset cooldowns of all other spells
                for (int i = 0; i < owner.spellList.Count; i++)
                {
                    if (owner.spellList[i] != this)
                    {
                        owner.spellList[i].cooldownCounter = 0;
                    }
                }
                if (defender.hitboxData.sweetSpot)
                {
                    cooldownCounter /= 2; // further reduce cooldown by 50% on sweet spot hit
                }
                break;
            default:
                break;
        }

    }
}
