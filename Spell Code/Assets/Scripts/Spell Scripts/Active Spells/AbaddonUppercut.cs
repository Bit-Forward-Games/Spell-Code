using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AbaddonUppercut : SpellData
{
    public const ushort demonAuraThreshold = 50;
    public AbaddonUppercut()
    {
        spellName = "Abaddon Uppercut";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0001_0001_0000_0011; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {ProcCondition.ActiveOnCast };
        projectilePrefabs = new GameObject[2];
        description = $"Short-range rising Uppercut.This spell double-hits if over {demonAuraThreshold}% Demon Aura<sprite name=\"DemonAura\">. This spell has Super Armor.";

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
            owner.vSpd = Fixed.FromInt(15); // Launch the player upwards slightly
            owner.hSpd = owner.facingRight ? Fixed.FromInt(2) : Fixed.FromInt(-2); // Propel the player forward

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 0)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[owner.demonAura> demonAuraThreshold ? 1:0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }
            cooldownCounter = owner.vibeCoding?(int)(cooldown+((spellInput & 0xFu)*30)):cooldown;
        }

    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                owner.superArmor = true;
                break;
            default:
                break;
        }
    }
}
