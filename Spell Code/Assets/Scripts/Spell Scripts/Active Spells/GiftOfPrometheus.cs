using UnityEngine;

public class GiftOfPrometheus : SpellData
{
    public GiftOfPrometheus()
    {
        spellName = "GiftOfPromethius";
        cooldown = 480;
        brands = new Brand[] { Brand.Killeez };
        spellInput = 0b_0000_0000_0000_0000_0010_1101_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit };
        description = "Send forth an orb of promethian flame, granting 2 \"Reps\" on hit. Deals massive damage, increased by how many \"Reps\" you currently have";

        projectilePrefabs = new GameObject[1];
    }

    

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnHit: // ActiveOnHit proc: On hitting an enemy with THIS spell, gain 2 reps and deal damage based on current reps.
                owner.reps += 2;
                defender.TakeEffectDamage(owner.reps * 5, owner);
                break;
            default:
                break;
        }
        ;
    }
}
