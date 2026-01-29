using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AsuranBlades : SpellData
{
    public AsuranBlades()
    {
        spellName = "AsuranBlades";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_0010_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[2] { ProcCondition.ActiveOnHit, ProcCondition.OnSlide };
        projectilePrefabs = new GameObject[4];

        description = "Leap back sending forth 3 shuriken, each granting \"Demon Aura\" on hit. If you have 50% or more \"Demon Aura\", your slide summons a blade";

        spawnOffsetX = 15;
        spawnOffsetY = 0;
    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;

        if (projectileInstances[0].activeSelf && projectileInstances[0].GetComponent<BaseProjectile>().logicFrame == 3)
        {
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
        }

        if (projectileInstances[1].activeSelf && projectileInstances[1].GetComponent<BaseProjectile>().logicFrame == 3)
        {
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY + 2)));
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
            owner.vSpd = Fixed.FromInt(8); // Launch the player upwards slightly
            owner.hSpd = owner.facingRight ? Fixed.FromInt(-2) : Fixed.FromInt(2); // Propel the player backwatds slightly

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 2)
            {
                
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY-2)));

            }
            cooldownCounter = cooldown;
        }


    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            //ActiveOnHit: Gain 10 Demon Aura on hitting an enemy with this spell.
            case ProcCondition.ActiveOnHit:
                owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                break;
            //OnHitBasic: Consume all Demon Aura on hitting an enemy with a basic attack, dealing that much bonus damage.
            case ProcCondition.OnSlide:
                if (owner.demonAura >= 50)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[3].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(16), Fixed.FromInt(0)));
                }
                break;
            default:
                break;
        }
    }
}
