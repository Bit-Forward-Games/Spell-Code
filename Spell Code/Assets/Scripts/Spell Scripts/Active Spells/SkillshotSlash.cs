using UnityEngine;

public class SkillshotSlash : SpellData
{
    public SkillshotSlash()
    {
        spellName = "SkillshotSlash";
        brands = new Brand[]{ Brand.VWave };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_0011_0000_0010; // Example input sequence
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
                ProjectileManager.Instance.SpawnProjectile(spellName, owner, owner.facingRight, new Vector2(10, 15));
            }
            // Reset the activate flag
            activateFlag = false;
        }
    }

    public override void ActiveOnHitProc(PlayerController defender)
    {
        //if we hit the sweet spot, set flow state to 300 (5 seconds worth)
        if (defender.hitboxData.sweetSpot)
        {
            owner.flowState = 300;
            Debug.Log("Sweet Spot Hit! Flow State set to 300.");
        }
    }


    public override void CheckCondition()
    {
        // Implement the effect that occurs when the condition is met within the spell or any other spell that procs this effect
    }
}
