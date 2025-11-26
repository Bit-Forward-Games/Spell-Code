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
        projectilePrefabs = new GameObject[3];

        description = "Summon down 3 lightning strikes, each granting 1 \"Rep\" if it hits. If you have 5 or more \"Reps\", this Spell-Code stuns!";

        spawnOffsetX = 15;
        spawnOffsetY = 0;
    }

    public override void ActiveOnHitProc(PlayerController defender)
    {
        

        if (owner.reps >= 5 && defender.state == PlayerState.Hitstun)
        {
            defender.stateSpecificArg += 60; // Stun duration in frames (1 second)
            Debug.Log($"Might of Zeus proc: Owner reps: {owner.reps}, Defender stun duration: {defender.stateSpecificArg} frames");
        }

        owner.reps++;
    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;

        if (projectileInstances[0].activeSelf && projectileInstances[0].GetComponent<BaseProjectile>().logicFrame == 6)
        {
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), projectileInstances[0].GetComponent<BaseProjectile>().facingRight, new Vector2(spawnOffsetX + 30, spawnOffsetY));
        }

        if (projectileInstances[1].activeSelf && projectileInstances[1].GetComponent<BaseProjectile>().logicFrame == 6)
        {
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), projectileInstances[0].GetComponent<BaseProjectile>().facingRight, new Vector2(spawnOffsetX + 60, spawnOffsetY));
        }


        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }
        if (activateFlag)
        {

            // Reset the activate flag
            activateFlag = false;


            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 0)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new Vector2(spawnOffsetX, spawnOffsetY));
            }
            cooldownCounter = cooldown;
        }

        
    }

    public override void CheckCondition()
    {
        // Implement the effect that occurs when the condition is met within the spell or any other spell that procs this effect
    }
}
