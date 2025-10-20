using UnityEngine;

public class Ninja_Build_Blast : SpellData
{
    public Ninja_Build_Blast()
    {
        spellName = "Ninja_Build_Blast";
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_1101_0010_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        projectilePrefabs = new GameObject[1];
        spawnOffsetX = 10;
        spawnOffsetY = 20;
    }
    

    public override void ActiveOnHitProc(PlayerController defender)
    {
        int consumedFlow = owner.flowState;
        float percentMaxHealthDamage = (float)consumedFlow/(float)PlayerController.maxFlowState * 0.5f; // 50% of max health at full flow
        int damageToDeal = Mathf.CeilToInt(defender.GetMaxHealth() * percentMaxHealthDamage);
        owner.flowState = 0;
        defender.TakeEffectDamage(damageToDeal);
    }

    public override void CheckCondition()
    {
        // Implement the effect that occurs when the condition is met within the spell or any other spell that procs this effect
    }
}
