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
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnCast };
        projectilePrefabs = new GameObject[1];
        description = "Short-range slash.\nThis Spell has armor.";

    }

   
  
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                owner.armor = true;
                break;
            default:
                break;
        }
        ;
    }
}
