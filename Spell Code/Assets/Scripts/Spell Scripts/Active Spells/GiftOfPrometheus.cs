using UnityEngine;

public class GiftOfPrometheus : SpellData
{
    public GiftOfPrometheus()
    {
        spellName = "Gift Of Prometheus";
        brands = new Brand[] { Brand.Killeez };
        cooldown = 300;
        spellInput = 0b_0000_0000_0000_0000_0000_0000_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {};
        description = "Spawn flames around you.\nThis Spellcode deals damage over time to those in it\nThis Spellcode's duration increases with Reps<sprite name=\"Reps\">.";
        projectilePrefabs = new GameObject[1];
        spawnOffsetX = 0;
        spawnOffsetY = 0;
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
