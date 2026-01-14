using UnityEngine;

public class SkillshotSlash : SpellData
{
    public SkillshotSlash()
    {
        spellName = "SkillshotSlash";
        brands = new Brand[]{ Brand.VWave };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0000_0011_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        projectilePrefabs = new GameObject[1];
        description = "Slash through reality in an area in front of you, dealing increased damage if you are in \"Flow State\". If you hit the tip of Skillshot Slash, you enter \"Flow State\". ";

        spawnOffsetX = 10;
        spawnOffsetY = 20;
    }

   

    public override void ActiveOnHitProc(PlayerController defender)
    {
        if(owner.flowState > 0)
        {
            defender.TakeEffectDamage(15);
        }
        //if we hit the sweet spot, set flow state to 300 (5 seconds worth)
        if (defender.hitboxData.sweetSpot)
        {
            owner.flowState = PlayerController.maxFlowState;
            Debug.Log("Sweet Spot Hit! Flow State set to 300.");
        }
    }


    public override void CheckCondition()
    {
        // Implement the effect that occurs when the condition is met within the spell or any other spell that procs this effect
    }
}
