using UnityEngine;

public class SunOfApollo : SpellData
{
    public SunOfApollo()
    {
        spellName = "Sun Of Apollo";
        cooldown = 480;
        brands = new Brand[] { Brand.Killeez };
        spellInput = 0b_0000_0000_0000_0000_0010_1101_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit };
        description = "Delayed massive explosion.\nDeals Massively increased damage based on Reps<sprite name=\"Reps\">.";

        projectilePrefabs = new GameObject[1];
    }

    

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnHit: // ActiveOnHit proc: On hitting an enemy with THIS spell, gain 2 reps and deal damage based on current reps.
                defender.TakeEffectDamage(owner.reps * 4, owner, GameManager.colors["yellow"]);
                break;
            default:
                break;
        }
        ;
    }
}
