using UnityEngine;

public class BladeOfAres : SpellData
{
    public BladeOfAres()
    {
        spellName = "Blade Of Ares";
        brands = new Brand[]{ Brand.Killeez };
        cooldown = 120;
        spellInput = 0b_0000_0000_0000_0000_0000_0111_0000_0010; 
        spellType = SpellType.Active;
        procConditions = new ProcCondition[2] { ProcCondition.ActiveOnHit, ProcCondition.ActiveOnCast };
        projectilePrefabs = new GameObject[1];
        description = "Short-range slash.\nHit this: Gain 1 Rep<sprite name=\"Reps\">.\nDeals increased damage based on Reps<sprite name=\"Reps\">.";

    }

   
  
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.ActiveOnHit: // ActiveOnHit proc: On hitting an enemy with THIS spell, gain 2 reps and deal damage based on current reps.
                owner.reps += 1;
                owner.SpawnToast("+1 REP", Color.yellow);
                defender.TakeEffectDamage(owner.reps * 2, owner);
                break;
            case ProcCondition.ActiveOnCast:
                owner.lightArmor = true;
                break;
            default:
                break;
        }
        ;
    }
}
