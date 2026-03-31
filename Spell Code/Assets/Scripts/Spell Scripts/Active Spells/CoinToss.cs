using UnityEngine;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class CoinToss : SpellData
{
    public bool doesCrit = false;
    public CoinToss()
    {
        spellName = "Coin Toss";
        brands = new Brand[] { Brand.BigStox };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0000_1101_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[1] {ProcCondition.ActiveOnCast};
        description = "Long-range arching coin.\n50% chance of increased damage.\nRandom chance based on Stock Stability<sprite name=\"StockStability\"> to guarantee damage.\nGain 10% Stock Stability<sprite name=\"StockStability\">.";
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
        base.LoadSpell();
        owner.stockStability += 10;
        owner.SpawnToast("+10% STOCK STABILITY", Color.blue);
        doesCrit = false;
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                int roll = GameManager.Instance.GetNextRandom(0, 100);
                Debug.Log($"[COINTOSS SYNC] Frame={GameManager.Instance.frameNumber} roll={roll} randomCallCount={GameManager.Instance.randomCallCount}");
                doesCrit = roll < owner.stockStability;
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(doesCrit);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        doesCrit = br.ReadBoolean();
    }

    
}
