using UnityEngine;

public class GiftOfPrometheus : SpellData
{
    public GiftOfPrometheus()
    {
        spellName = "Active_Spell_3";
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_1001_1100_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        projectilePrefabs = new GameObject[1];
    }



    public override void CheckCondition()
    {
        // Implement the effect that occurs when the condition is met within the spell or any other spell that procs this effect
    }
}
