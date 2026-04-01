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
        description = "On Basic hit: gain 20% Stock Stability<sprite name=\"StockStability\"> until next spell cast.";
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
                storedStockStability += 20;
                owner.stockStability += 20;
                if(owner.stockStability > 100)
                {
                    int excessStocStability = owner.stockStability - 100;
                    owner.stockStability = 100;
                    storedStockStability -= (ushort)excessStocStability;

                }
                owner.SpawnToast("+20% STOCK STABILITY", Color.blue);
                break;
            case ProcCondition.OnCastSpell:
                owner.stockStability -= storedStockStability;
                storedStockStability = 0;
                owner.SpawnToast($"+{storedStockStability}% STOCK STABILITY", Color.gray);
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
