using UnityEngine;

public class PongShot : SpellData
{
    public PongShot()
    {
        spellName = "Pong Shot";
        brands = new Brand[] { Brand.VWave };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_1011_0000_0011; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {};
        description = "Diagonal downward shot.\nThis spell ricochets off surfaces, resetting its lifespan.\nWhile in Flow State<sprite name=\"FlowState\"> this spell has a longer lifespan.";
        projectilePrefabs = new GameObject[1];
        spawnOffsetX = 20;
        spawnOffsetY = 45;
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        //all of the special effect logic for this spellcode lies in the projectile script
        switch (targetProcCon)
        {
            
            default:
                break;
        }
    }
}
