using UnityEngine;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class QuarterReport : SpellData
{
    public bool doesCrit = false;
    private bool stockBonusGranted = false;
    public QuarterReport()
    {
        spellName = "Quarter Report";
        brands = new Brand[] { Brand.BigStox };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_1111_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[1] {ProcCondition.ActiveOnCast};
        description = "Medium-range shot.\nRandom chance based on Stock Stability<sprite name=\"StockStability\"> to enhance range and damage.\nGain 10% Stock Stability<sprite name=\"StockStability\">.";
        projectilePrefabs = new GameObject[2];
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
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[(doesCrit?1:0)].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }
            cooldownCounter = cooldown;
        }
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        if (!stockBonusGranted && owner != null)
        {
            owner.stockStability += 10;
            owner.SpawnToast("+10% STOCK STABILITY", Color.blue);
            stockBonusGranted = true;
        }
        doesCrit = false;
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                doesCrit = GameManager.Instance.GetNextRandom(0, 100) < owner.stockStability;
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(doesCrit);
        bw.Write(stockBonusGranted);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        doesCrit = br.ReadBoolean();
        stockBonusGranted = br.ReadBoolean();
    }

    
}
