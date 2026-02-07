using UnityEngine;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class QuarterReport : SpellData
{
    public QuarterReport()
    {
        spellName = "QuarterReport";
        brands = new Brand[] { Brand.BigStox };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_1111_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[1] { ProcCondition.ActiveOnHit };
        description = "TEMP_TEXT";
        projectilePrefabs = new GameObject[2];
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


            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 1)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[(GameManager.Instance.seededRandom.Next(0,100) < owner.stockStability?1:0)].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }
            cooldownCounter = cooldown;
        }
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        

    }

    
}
