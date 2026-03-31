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
        procConditions = new ProcCondition[1] { ProcCondition.ActiveOnHit };
        description = "Long Range Shot.\nHit this: Reset all other cooldowns.\nHit sweet-spot: enter Flow State<sprite name=\"FlowState\">\nConsume all Flow State<sprite name=\"FlowState\"> to reduce this cooldown.";
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
                    //float percentMaxHealthDamage = (float)consumedFlow/(float)PlayerController.maxFlowState * 0.5f; // 50% of max health at full flow
                    //int damageToDeal = Mathf.CeilToInt(defender.GetMaxHealth() * percentMaxHealthDamage);
                    //defender.TakeEffectDamage(damageToDeal);
                    float percentCooldownReduced = (float)currentFlow / (float)PlayerController.maxFlowState; // 100% cooldown reduction at full flow

                    cooldownCounter = (int)(cooldownCounter * percentCooldownReduced);
                    //if we hit the sweet spot, set flow state to 300 (5 seconds worth)
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
                    owner.flowState = PlayerController.maxFlowState;
                    cooldownCounter /= 2; // further reduce cooldown by 50% on sweet spot hit
                    owner.SpawnToast("FLOW STATE!", Color.green);
                }
                break;
            default:
                break;
        }

    }
}
