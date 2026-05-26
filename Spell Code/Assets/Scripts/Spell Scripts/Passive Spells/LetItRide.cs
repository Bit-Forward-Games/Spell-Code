using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using System.Linq;

public class LetItRide : SpellData
{

    public LetItRide()
    {
        spellName = "Let It Ride";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnUpdate, ProcCondition.OnHitSpell };
        brands = new Brand[1] { Brand.BigStox };
        description = "Your Stock Stability<sprite name=\"StockStability\"> is cut in half.\nConvert consumed Stock Stability<sprite name=\"StockStability\"> into bonus damage on \"Crit\"<sprite name=\"StockStability\"> for your BigStox Spellcodes.";
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnUpdate:
                if (owner.stockStability != owner.stockStabilityModified/2)
                {
                    owner.stockStabilityModified = (ushort)((int)owner.stockStability/2);
                }
                break;
            case ProcCondition.OnHitSpell:
                if (defender.hitboxData.sweetSpot && defender.hitboxData.parentProjectile.ownerSpell.brands.Contains(Brand.BigStox))
                {
                    int effectDamage = owner.stockStability/2;
                    defender.TakeEffectDamage(effectDamage, owner, GameManager.colors["blue"]);
                }
                break;
            default:
                break;
        }
    }

}
