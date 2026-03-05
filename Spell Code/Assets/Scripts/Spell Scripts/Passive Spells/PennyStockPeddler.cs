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
        description = "Your Stock Stability\" is capped at 15%. all excess Stock Stability is converted into bonus damage upon hitting an empowered \"Big Stox\" Spell";
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        convertedStockStability = 0;
    }
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnUpdate:
                if (owner.stockStability != 15)
                {
                    convertedStockStability += (ushort)(owner.stockStability - 15);
                    owner.stockStability = 15;
                }
                break;
            case ProcCondition.OnHitSpell:
                if (defender.hitboxData.sweetSpot && defender.hitboxData.parentProjectile.ownerSpell.brands.Contains(Brand.BigStox))
                {
                    defender.TakeEffectDamage(convertedStockStability, owner);
                }
                break;
            default:
                break;
        }
    }
}
