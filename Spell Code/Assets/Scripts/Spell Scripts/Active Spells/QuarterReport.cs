using UnityEngine;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class QuarterReport : SpellData
{
    public QuarterReport()
    {
        spellName = "QuarterReport";
        brands = new Brand[] { Brand.BigStox };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0000_1111_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[0] {};
        description = "Send forth your quarterly projections materialized. This spell has a chance equal to your \"Stock Stability\" to gain increased size and damage. Gain 15% \"Stock Stability\".";
        projectilePrefabs = new GameObject[2];
        spawnOffsetX = 15;
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


            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 1)
            {
                bool doesCrit = GameManager.Instance.seededRandom.Next(0, 100) < owner.stockStability;
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[(doesCrit?1:0)].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }
            cooldownCounter = cooldown;
        }
    }

    public override void LoadSpell()
    {
        owner.stockStability += 15;
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {

    }

    
}
