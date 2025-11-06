using UnityEngine;

public class MightOfZeus : SpellData
{
    public MightOfZeus()
    {
        spellName = "MightOfZeus";
        brands = new Brand[]{ Brand.Killeez };
        cooldown = 300;
        spellInput = 0b_0000_0000_0000_0000_0000_0000_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        projectilePrefabs = new GameObject[1];
        description = "Summon down a lightning strike which grants you 1 \"Rep\" if it hits. If you have 5 or more \"Reps\", this Spell-Code stuns!";

        spawnOffsetX = 30;
        spawnOffsetY = 0;
    }

    public override void ActiveOnHitProc(PlayerController defender)
    {
        owner.reps++;

        if (owner.reps >= 5 && defender.state == PlayerState.Hitstun)
        {
            defender.stateSpecificArg += 60; // Stun duration in frames (1 second)
            Debug.Log($"Might of Zeus proc: Owner reps: {owner.reps}, Defender stun duration: {defender.stateSpecificArg} frames");
        }
    }

    public override void CheckCondition()
    {
        // Implement the effect that occurs when the condition is met within the spell or any other spell that procs this effect
    }
}
