using UnityEngine;

public class Ninja_Build_Blast : SpellData
{
    public Ninja_Build_Blast()
    {
        spellName = "Ninja_Build_Blast";
        brands = new Brand[] { Brand.VWave };
        cooldown = 600;
        spellInput = 0b_0000_0000_0000_0000_1101_0010_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[1] { ProcCondition.ActiveOnHit };
        description = "Project forth a massive orb which consumes all of your \"Flow State\" and reduces all cooldowns based on the amount consumed. Hit this SpellCode early to enter \"Flow State\"";
        projectilePrefabs = new GameObject[1];
        spawnOffsetX = 10;
        spawnOffsetY = 20;
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        //ActiveOnHit proc: when this spell hits an enemy, consume all Flow State to reduce cooldowns
        int consumedFlow = owner.flowState;
        if (consumedFlow > 0)
        {
            //float percentMaxHealthDamage = (float)consumedFlow/(float)PlayerController.maxFlowState * 0.5f; // 50% of max health at full flow
            //int damageToDeal = Mathf.CeilToInt(defender.GetMaxHealth() * percentMaxHealthDamage);
            //defender.TakeEffectDamage(damageToDeal);
            float percentCooldownReduced = (float)consumedFlow / (float)PlayerController.maxFlowState; // 100% cooldown reduction at full flow
            for (int i = 0; i < owner.spellList.Count; i++)
            {
                owner.spellList[i].cooldownCounter = (int)((float)owner.spellList[i].cooldownCounter * percentCooldownReduced);
            }
            owner.flowState = 0;
        }

        //if we hit the sweet spot, set flow state to 300 (5 seconds worth)
        if (defender.hitboxData.sweetSpot)
        {
            owner.flowState = PlayerController.maxFlowState;
            cooldownCounter /= 2; // further reduce cooldown by 50% on sweet spot hit
            Debug.Log("Sweet Spot Hit! Flow State set to 300.");
        }
    }
}
