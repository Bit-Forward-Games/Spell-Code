using UnityEngine;

public class BladeOfAres : SpellData
{
    public BladeOfAres()
    {
        spellName = "BladeOfAres";
        brands = new Brand[]{ Brand.Killeez };
        cooldown = 120;
        spellInput = 0b_0000_0000_0000_0000_0000_0111_0000_0010; 
        spellType = SpellType.Active;
        procConditions = new ProcCondition[1] { ProcCondition.ActiveOnHit };
        projectilePrefabs = new GameObject[1];
        description = "Strike down with the Blade of Ares, granting 1 \"Rep\" on hit, and dealing increased damage based on \"Reps\".";

    }

   
  
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.ActiveOnHit: // ActiveOnHit proc: On hitting an enemy with THIS spell, gain 2 reps and deal damage based on current reps.
                owner.reps += 1;
                defender.TakeEffectDamage(owner.reps * 2, owner);
                break;
            default:
                break;
        }
        ;
    }
}
