using UnityEngine;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class CoinToss : SpellData
{
    public bool doesCrit = false;
    public CoinToss()
    {
        spellName = "CoinToss";
        brands = new Brand[] { Brand.BigStox };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0000_1101_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[1] {ProcCondition.ActiveOnCast};
        description = "Toss a coin, dealing massive damage if it lands on heads. There is a chance equal to your \"Stock Stability\" that you throw ad Loaded coin. You gain 15% \"Stock Stability\".";
        projectilePrefabs = new GameObject[3];
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
            byte projectileIndex = (byte)(doesCrit ? 0 : (GameManager.Instance.GetNextRandom(0, 100) < 50?1:2));

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 1)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[projectileIndex].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }
            cooldownCounter = cooldown;
        }
    }

    public override void LoadSpell()
    {
        owner.stockStability += 15;
        doesCrit = false;
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                doesCrit = GameManager.Instance.seededRandom.Next(0, 100) < owner.stockStability;

                break;
            default:
                break;
        }
    }

    
}
