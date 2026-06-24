using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BlueChipTrader : SpellData
{
    public ushort storedStockStability = 0;
    public const ushort stockStabilityProvidedAmount = 30;

    public BlueChipTrader()
    {
        spellName = "Blue Chip Trader";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnHitBasic, ProcCondition.OnHitSpell };
        brands = new Brand[1] { Brand.BigStox };
        description = $"On Basic hit: gain {stockStabilityProvidedAmount}% Stock Stability<sprite name=\"StockStability\"> until next spell cast.";
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
                storedStockStability += stockStabilityProvidedAmount;
                owner.stockStability += stockStabilityProvidedAmount;
                owner.stockStabilityModified = owner.stockStability;
                if(owner.stockStability > 100)
                {
                    int excessStocStability = owner.stockStability - 100;
                    owner.stockStability = 100;
                    owner.stockStabilityModified = owner.stockStability;
                    storedStockStability -= (ushort)excessStocStability;

                }

                //play the Blue Chip Trader SFX
                SFX_Manager.Instance.PlaySpellcodeSound("Blue Chip Trader");

                owner.SpawnToast($"+{stockStabilityProvidedAmount}% STOCK STABILITY", GameManager.colors["blue"]);
                break;
            case ProcCondition.OnHitSpell:
                owner.SpawnToast($"RESET STOCK STABILITY", GameManager.colors["grey"]);
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
