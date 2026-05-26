using UnityEngine;

public class TrickshotAlley : SpellData
{
    public TrickshotAlley()
    {
        spellName = "Trickshot Alley";
        brands = new Brand[]{ Brand.VWave };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0011_1000_0000_0011; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit };
        projectilePrefabs = new GameObject[1];
        description = "Lobbing Grenade.\nHit the grenade to launch it, partially refunding this spell's cooldown.";

    }

   
  
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            //all effect logic is performed in the projectile
            default:
                break;
        }
    }
}
