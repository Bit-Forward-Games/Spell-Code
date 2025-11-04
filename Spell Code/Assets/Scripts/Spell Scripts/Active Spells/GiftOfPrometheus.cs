using UnityEngine;

public class GiftOfPrometheus : SpellData
{
    public GiftOfPrometheus()
    {
        spellName = "GiftOfPromethius";
        cooldown = 180;
        brands = new Brand[] { Brand.Killeez };
        spellInput = 0b_0000_0000_0000_0000_0010_1101_0000_0100; // Example input sequence
        spellType = SpellType.Active;

        projectilePrefabs = new GameObject[1];
    }

    public override void ActiveOnHitProc(PlayerController defender)
    {
        owner.reps+= 2;
        defender.TakeEffectDamage(owner.reps*3);
        Debug.Log($"Gift of Prometheus proc: Dealt {owner.reps*3} damage to defender. Owner reps: {owner.reps}");
    }

    public override void CheckCondition()
    {
        // Implement the effect that occurs when the condition is met within the spell or any other spell that procs this effect
    }
}
