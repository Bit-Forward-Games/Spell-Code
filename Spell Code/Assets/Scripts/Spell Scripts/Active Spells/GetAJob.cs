using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class GetAJob : SpellData
{
    public bool doesCrit = false;
    public GetAJob()
    {
        spellName = "GetAJob";
        brands = new Brand[]{ Brand.BigStox };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0011_0100_0000_0011; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[1] {ProcCondition.ActiveOnCast };
        projectilePrefabs = new GameObject[2];
        description = "Lunge forward, presenting a job application. Has a chance equal to your \"Stock Stability\" to lunge further with a more powerful application.";
        spawnOffsetX = 36;
        spawnOffsetY = 36;
    }
    public override void LoadSpell()
    {
        owner.stockStability += 15;
        doesCrit = false;
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
            int speedBoost = doesCrit ? 12 : 8; // Example: If it's a critical hit, increase speed boost
            // Reset the activate flag
            activateFlag = false;
            owner.vSpd = Fixed.FromInt(4); // Launch the player upwards slightly
            owner.hSpd = owner.facingRight ? Fixed.FromInt(speedBoost) : Fixed.FromInt(-speedBoost); // Propel the player forward

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 1)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[(doesCrit ? 1 : 0)].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }
            cooldownCounter = cooldown;
        }

    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            
            case ProcCondition.ActiveOnCast:
                doesCrit = GameManager.Instance.seededRandom.Next(0, 100) < owner.stockStability;
                owner.lightArmor = doesCrit;
                break;
            default:
                break;
        }

        
    }
}
