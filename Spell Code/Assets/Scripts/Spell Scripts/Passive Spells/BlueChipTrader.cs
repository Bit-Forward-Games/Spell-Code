using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BlueChipTrader : SpellData
{
    public ushort storedStockStability = 0;

    public BlueChipTrader()
    {
        spellName = "Blue Chip Trader";
        cooldown = 1;
        spellType = SpellType.Passive;
        priorityOverride = 1;
        procConditions = new ProcCondition[] { ProcCondition.OnHitBasic, ProcCondition.OnCastSpell };
        brands = new Brand[1] { Brand.BigStox };
        description = "On Basic hit: gain 50% Stock Stability<sprite name=\"StockStability\"> until next spell cast.";
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        storedStockStability = 0;
    }
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnHitBasic:
                storedStockStability += 50;
                owner.stockStability += 50;
                owner.stockStabilityModified = owner.stockStability;
                if(owner.stockStability > 100)
                {
                    int excessStocStability = owner.stockStability - 100;
                    owner.stockStability = 100;
                    owner.stockStabilityModified = owner.stockStability;
                    storedStockStability -= (ushort)excessStocStability;

                }
                owner.SpawnToast("+50% STOCK STABILITY", GameManager.colors["blue"]);
                break;
            case ProcCondition.OnCastSpell:
                owner.SpawnToast($"-{storedStockStability}% STOCK STABILITY", GameManager.colors["grey"]);
                owner.stockStability -= storedStockStability;
                owner.stockStabilityModified = owner.stockStability;
                storedStockStability = 0;
                
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(storedStockStability);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        storedStockStability = br.ReadUInt16();
    }
}
