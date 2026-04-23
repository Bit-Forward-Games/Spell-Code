using UnityEngine;
using System.Linq;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class TridentOfPoseidon : SpellData
{
    public TridentOfPoseidon()
    {
        spellName = "Trident Of Poseidon";
        brands = new Brand[]{ Brand.Killeez };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0000_0111_0000_0011; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[3] { ProcCondition.ActiveOnHit, ProcCondition.OnHitBasic, ProcCondition.ActiveOnCast };
        projectilePrefabs = new GameObject[3];
        description = "Lunge Downward with Trident.\nHit this: Gain 1 Rep<sprite name=\"Reps\">.\nUpon landing, send forth a wave that goes farther based on Reps<sprite name=\"Reps\">.";

    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;
        if (projectileInstances[0].activeSelf && projectileInstances[0].GetComponent<BaseProjectile>().logicFrame >= projectileInstances[0].GetComponent<BaseProjectile>().animFrames.frameLengths.Take(3).Sum())
        {
            owner.vSpd = Fixed.FromInt(-10); // Launch the player upwards slightly
            owner.hSpd = Fixed.FromInt(0); // Propel the player forward

            //if you land and the wave projectiles dont already exist, spawn them
            if (owner.isGrounded && !projectileInstances[1].activeSelf && !projectileInstances[2].activeSelf)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(0)));
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), !owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(0)));

            }
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
            owner.vSpd = Fixed.FromInt(5); // Launch the player upwards slightly
            owner.hSpd = owner.facingRight ? Fixed.FromInt(3) : Fixed.FromInt(-3); // Propel the player forward

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
            //ActiveOnHit: Gain 10 Demon Aura on hitting an enemy with this spell.
            case ProcCondition.ActiveOnHit:
                owner.reps++;
                owner.SpawnToast("+1 REP", Color.yellow);
                break;
            default:
                break;
        }

    }
}
