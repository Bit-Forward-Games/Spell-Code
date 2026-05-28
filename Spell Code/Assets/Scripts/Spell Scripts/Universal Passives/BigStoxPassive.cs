using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BigStoxPassive : SpellData
{
    public static ushort bigStoxCritDamage = 15;
    public BigStoxPassive()
    {
        spellName = "BigStox Passive";
        brands = new Brand[]{ Brand.BigStox };
        cooldown = 1;
        priorityOverride = 5;
        spellType = SpellType.Universal;
        procConditions = new ProcCondition[2] { ProcCondition.OnStart, ProcCondition.OnHitSpell};
        description = $"On Spawn: Gain 10% Stock Stability<sprite name=\"StockStability\"> for every BigStox Spellcode you have.\n hitting a Spellcode has a random chance based on Stock Stability<sprite name=\"StockStability\"> to \"Crit\", dealing increased damage.";

    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnHitSpell:
                // we let Bigstox Spells handle their own Crit logic, this is so that we dont roll for crit 2 times for a bigstox spell on both cast and hit
                if(defender.hitboxData.parentProjectile.ownerSpell.brands[0] != Brand.BigStox || !defender.hitboxData.sweetSpot)
                {
                    //non bigstox spells can crit here
                    if(GameManager.Instance.GetNextRandom(0, 100) < owner.stockStabilityModified)
                    {
                        defender.TakeEffectDamage(bigStoxCritDamage,owner, GameManager.colors["blue"]);
                    }
                }
                break;
            case ProcCondition.OnStart:
                foreach(SpellData spell in owner.spellList)
                {
                    if(spell.brands[0] == Brand.BigStox)
                    {
                        owner.stockStability +=10;
                    }
                }
                owner.stockStabilityModified = owner.stockStability;
                break;
            default:
                break;
        }

        
    }
}
