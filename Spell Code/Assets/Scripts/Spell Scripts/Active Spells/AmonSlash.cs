using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AmonSlash : SpellData
{
    public AmonSlash()
    {
        spellName = "Amon Slash";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 120;
        spellInput = 0b_0000_0000_0000_0000_0000_1100_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnCast };
        projectilePrefabs = new GameObject[1];
        description = "Short-range lunging slash.\n This Spellcode has Armor.";

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
            // If the remote owner's input for this frame is still being predicted,
            // hold off on the launch so we don't commit hSpd to a direction that may flip when
            // the real input arrives. activateFlag stays set; we retry next frame.
            if (ShouldDeferSpellLaunchForPrediction()) return;

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
            // case ProcCondition.ActiveOnHit:
            //     //defender.TakeEffectDamage(owner.demonAura/5, owner);
                
                
            //     break;
            case ProcCondition.ActiveOnCast:
                owner.armor = true;
                break;
            default:
                break;
        }

        
    }
}
