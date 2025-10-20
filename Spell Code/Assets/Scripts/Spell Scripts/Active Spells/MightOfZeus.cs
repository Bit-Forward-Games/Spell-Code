using UnityEngine;

public class MightOfZeus : SpellData
{
    public MightOfZeus()
    {
        spellName = "MightOfZeus";
        brands = new Brand[]{ Brand.Killeez };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_1001_0011_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        projectilePrefabs = new GameObject[1];

        spawnOffsetX = 30;
        spawnOffsetY = 0;
    }


    public override void CheckCondition()
    {
        // Implement the effect that occurs when the condition is met within the spell or any other spell that procs this effect
    }
}
