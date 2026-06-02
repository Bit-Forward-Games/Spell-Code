using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class DemonXPassive : SpellData
{
    public static ushort DemonAuraResetTime = 180;
    public DemonXPassive()
    {
        spellName = "Demon-X Passive";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 1;
        priorityOverride = 3;
        spellType = SpellType.Universal;
        procConditions = new ProcCondition[3] { ProcCondition.OnHitSpell, ProcCondition.OnHit, ProcCondition.OnUpdate };
        description = $"Hit Demon-X Spellcodes to increase Demon Aura<sprite name=\"DemonAura\">.\nAfter {DemonAuraResetTime/60f} seconds of not dealing damage, lose Demon Aura<sprite name=\"DemonAura\">.\nSpellcodes deal increased damage based on your Demon Aura<sprite name=\"DemonAura\">.";

    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnHit:
                owner.demonAuraLifeSpanTimer = DemonAuraResetTime; //refresh demon aura lifespan timer on spell hit to 3 seconds (360 frames)
                break;
            case ProcCondition.OnHitSpell:
                if(owner.demonAura > 0)
                {
                    // deal extra damage based on demon aura
                    defender.TakeEffectDamage(owner.demonAura/5, owner, GameManager.colors["red"]);
                }
                


                //increase demon aura by 20 if its a Demon-X spellcode
                if(defender.hitboxData.parentProjectile.ownerSpell.brands[0] == Brand.DemonX && !defender.hitboxData.parentProjectile.ignoreBrand)
                {
                    owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                    owner.SpawnToast("+20 DEMON AURA", GameManager.colors["red"]);
                }
                
                break;
            case ProcCondition.OnUpdate:
            //if its been 3 seconds since you've damaged someone, remove your demon aura
            if (owner.demonAura > 0 && owner.demonAuraLifeSpanTimer > 0)
            {
                owner.demonAuraLifeSpanTimer--;
            }
            else
            {
                owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura - 1, 0, PlayerController.maxDemonAura);
            }
                break;
            default:
                break;
        }

        
    }
}
