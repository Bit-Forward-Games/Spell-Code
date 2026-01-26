using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AmonSlash : SpellData
{
    public AmonSlash()
    {
        spellName = "AmonSlash";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_1100_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[2] { ProcCondition.ActiveOnHit, ProcCondition.OnHitBasic };
        projectilePrefabs = new GameObject[1];
        description = "Lunge forward slashing in front of you, granting \"Demon Aura\" on hit. Your next basic attack consumes all \"Demon Aura\" and deals increased damage based on how much is consumed.";

        spawnOffsetX = 10;
        spawnOffsetY = 20;
    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }
        if (activateFlag)
        {

            // Reset the activate flag
            activateFlag = false;
            owner.vSpd = Fixed.FromInt(2); // Launch the player upwards slightly
            owner.hSpd = owner.facingRight ? Fixed.FromInt(6) : Fixed.FromInt(-6); // Propel the player forward

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
                owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                break;
            //OnHitBasic: Consume all Demon Aura on hitting an enemy with a basic attack, dealing that much bonus damage.
            case ProcCondition.OnHitBasic:
                if (owner.demonAura > 0)
                {
                    int damageToDeal = (int)(Mathf.Pow(owner.demonAura/10,2)/2);
                    defender.TakeEffectDamage(damageToDeal);
                    owner.demonAura = 0;
                }
                break;
            default:
                break;
        }

        
    }
}
