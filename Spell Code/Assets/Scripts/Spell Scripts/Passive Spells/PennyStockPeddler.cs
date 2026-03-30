using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using System.Linq;

public class PennyStockPeddler : SpellData
{
    public ushort convertedStockStability = 0;

    public PennyStockPeddler()
    {
        spellName = "PennyStockPeddler";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[2] { ProcCondition.OnUpdate, ProcCondition.OnHitSpell };
        brands = new Brand[1] { Brand.BigStox };
        description = "Stock Stability<sprite name=\"StockStability\"> is capped at 10%.\nConvert excess Stock Stability<sprite name=\"StockStability\"> into bonus damage on empowered \"Big Stox\" Spells.\nGain 10% Stock Stability<sprite name=\"StockStability\">.";
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        convertedStockStability = 0;
        owner.stockStability += 10;
        owner.SpawnToast("+10% STOCK STABILITY", Color.blue);
    }
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnUpdate:
                if (owner.stockStability != 10)
                {
                    convertedStockStability += (ushort)(owner.stockStability - 10);
                    owner.stockStability = 10;
                }
                break;
            case ProcCondition.OnHitSpell:
                if (defender.hitboxData.sweetSpot && defender.hitboxData.parentProjectile.ownerSpell.brands.Contains(Brand.BigStox))
                {
                    int effectDamage = (int)((convertedStockStability+10)*1.5)-10;
                    defender.TakeEffectDamage(effectDamage, owner);
                }
                break;
            default:
                break;
        }
    }
}
