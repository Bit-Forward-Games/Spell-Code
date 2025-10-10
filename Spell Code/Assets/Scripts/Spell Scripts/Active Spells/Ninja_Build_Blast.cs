using UnityEngine;

public class Ninja_Build_Blast : SpellData
{
    public Ninja_Build_Blast()
    {
        spellName = "Ninja_Build_Blast";
        cooldown = 1.0f;
        spellInput = 0b_0000_0000_0000_0000_0110_0011_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        projectilePrefabs = new GameObject[1];
    }
    public override void SpellUpdate()
    {
        if (activateFlag)
        {
            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 0)
            {
                ProjectileManager.Instance.SpawnProjectile(spellName, owner, owner.facingRight, new Vector2(10,20));
            }
            // Reset the activate flag
            activateFlag = false;
        }
    }

    public override void CheckCondition()
    {
        // Implement any conditions that need to be checked before the spell can be activated
    }

    public override void ProcEffect()
    {
        // Implement the effect that occurs when the condition is met within the spell or any other spell that procs this effect
    }
}
