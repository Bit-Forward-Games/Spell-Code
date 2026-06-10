using UnityEngine;

public class ShotReflector : SpellData
{
    public ShotReflector()
    {
        spellName = "Shot Reflector";
        brands = new Brand[]{ Brand.VWave };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0100_0100_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {};
        projectilePrefabs = new GameObject[1];
        description = "Slow Moving Shield.\nAbsorbs opponent projectiles, launching a counter-projectile and partially refunding this spell's cooldown.";

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
