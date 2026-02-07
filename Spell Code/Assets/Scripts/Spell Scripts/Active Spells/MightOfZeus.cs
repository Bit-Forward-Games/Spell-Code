using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class MightOfZeus : SpellData
{
    public MightOfZeus()
    {
        spellName = "MightOfZeus";
        brands = new Brand[]{ Brand.Killeez };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0000_0000_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[1] { ProcCondition.ActiveOnHit };
        projectilePrefabs = new GameObject[3];

        description = "Summon down 3 lightning strikes, each granting 1 \"Rep\" if it hits. If you have 8 or more \"Reps\", this Spell-Code stuns!";

        spawnOffsetX = 15;
        spawnOffsetY = 0;
    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;

        if (projectileInstances[0].activeSelf && projectileInstances[0].GetComponent<BaseProjectile>().logicFrame == 3)
        {
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), projectileInstances[0].GetComponent<BaseProjectile>().facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX + 25), Fixed.FromInt(spawnOffsetY)));
        }

        if (projectileInstances[1].activeSelf && projectileInstances[1].GetComponent<BaseProjectile>().logicFrame == 3)
        {
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), projectileInstances[0].GetComponent<BaseProjectile>().facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX + 50), Fixed.FromInt(spawnOffsetY)));
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
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }
            cooldownCounter = cooldown;
        }

        
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnHit: // ActiveOnHit proc: Grant "Reps" and apply stun if conditions are met
                if (owner.reps >= 5 && defender.state == PlayerState.Hitstun)
                {
                    defender.stateSpecificArg += 45; // Stun duration in frames (.75 seconds)
                    defender.hSpd = Fixed.FromInt(0); // Stop horizontal movement
                    defender.vSpd = Fixed.FromInt(0); // Stop vertical movement

                }

                owner.reps++;
                break;
            default:
                break;
        }
    }
}
