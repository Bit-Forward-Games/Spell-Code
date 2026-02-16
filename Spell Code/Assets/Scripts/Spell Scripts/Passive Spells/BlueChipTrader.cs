using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BlueChipTrader : SpellData
{
    public ushort storedStockStability = 0;

    public BlueChipTrader()
    {
        spellName = "BlueChipTrader";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[2] { ProcCondition.OnHitBasic, ProcCondition.OnCastSpell};
        brands = new Brand[1] { Brand.BigStox };
        description = "Gain 10% \"Stock Stability\" upon hitting your basic spell, consumed upon Using your next spell-code, ";
    }

    public override void LoadSpell()
    {
        storedStockStability = 0;
    }
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnHitBasic:
                storedStockStability += 10;
                owner.stockStability += 10;
                break;
            case ProcCondition.OnCastSpell:
                owner.stockStability -= storedStockStability;
                storedStockStability = 0;
                break;
            default:
                break;
        }
    }
}
