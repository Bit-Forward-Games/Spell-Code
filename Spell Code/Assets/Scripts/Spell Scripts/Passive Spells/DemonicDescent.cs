using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class DemonicDescent : SpellData
{

    public DemonicDescent()
    {
        spellName = "DemonicDescent";
        cooldown = 0;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[2] { ProcCondition.OnUpdate, ProcCondition.OnHitSpell };
        brands = new Brand[1] { Brand.DemonX };
        description = "While at 100% \"Demon Aura\", you gain mobility, and your Spells deal increased damage";
    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            //ActiveOnHit: Gain 10 Demon Aura on hitting an enemy with this spell.
            case ProcCondition.OnUpdate:
                owner.runSpeed = owner.demonAura >= 100 ? Fixed.FromFloat((owner.charData.runSpeed + 30)/10) : Fixed.FromFloat(owner.charData.runSpeed/10);
                owner.jumpForce = owner.demonAura >= 100 ? Fixed.FromFloat(owner.charData.jumpForce + 2) : Fixed.FromFloat(owner.charData.jumpForce);
                owner.slideSpeed = owner.demonAura >= 100 ? Fixed.FromFloat((owner.charData.slideSpeed + 20)/10) : Fixed.FromFloat(owner.charData.slideSpeed/10);
                break;
            //OnHitSpell: Deal Extra damage when hitting with a spell.
            case ProcCondition.OnHitSpell:
                if (owner.demonAura >= 100)
                {
                    defender.TakeEffectDamage(15);
                }
                break;
            default:
                break;
        }
    }
}
