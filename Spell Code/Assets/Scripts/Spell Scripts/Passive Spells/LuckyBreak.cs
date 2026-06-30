using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using System.Linq;

public class LuckyBreak : SpellData
{
    public const int extraBasicDamage = 10;
    public LuckyBreak()
    {
        spellName = "Lucky Break";
        cooldown = 60;
        spellType = SpellType.Passive;
        priorityOverride = 1;
        procConditions = new ProcCondition[] { ProcCondition.OnHitBasic };
        brands = new Brand[1] { Brand.BigStox };
        description = $"Your Basic attack can crit<sprite name=\"StockStability\"> for +{extraBasicDamage} damage.";
    }
    public override void SpellUpdate()
    {
        //basic cooldown handling
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }

    }
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnHitBasic:
                if(IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile))
                {
                    if(GameManager.Instance.GetNextRandom(0, 100) < owner.stockStabilityModified && !defender.hitboxData.ignoreEffectDamage)
                    {
                        defender.TakeEffectDamage(extraBasicDamage,owner, GameManager.colors["blue"]);
                    }
                }
                break;
            default:
                break;
        }
    }

}
