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
        procConditions = new ProcCondition[2] { ProcCondition.OnHitBasic, ProcCondition.OnCastSpell };
        brands = new Brand[1] { Brand.BigStox };
        description = "On Basic hit: gain 100% Stock Stability<sprite name=\"StockStability\"> until next spell cast.";
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
                storedStockStability = owner.stockStability;
                owner.stockStability = 100;
                owner.SpawnToast($"+{100-storedStockStability}% STOCK STABILITY", Color.blue);
                break;
            case ProcCondition.OnCastSpell:
                owner.stockStability = storedStockStability;
                storedStockStability = 0;
                owner.SpawnToast($"STOCK STABILITY CONSUMED", Color.gray);
                break;
            default:
                break;
        }
    }
}
