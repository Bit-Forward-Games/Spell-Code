using System.Linq;
using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class Reps : SpellData
{
    public static ushort DemonAuraResetTime = 180;
    public Reps()
    {
        spellName = "Reps";
        brands = new Brand[]{ Brand.Killeez };
        cooldown = 1;
        priorityOverride = 3;
        spellType = SpellType.Universal;
        procConditions = new ProcCondition[1] { ProcCondition.OnHitSpell};
        description = $"Hit Killeez Spellcodes to Gain Reps<sprite name=\"Reps\">.\nOn Respawn, lose all Reps<sprite name=\"Reps\">.\nSpellcodes deal increased damage based on Reps<sprite name=\"Reps\">.";
    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnHitSpell:
                // deal extra damage based on demon aura
                if(owner.reps > 0 && !defender.hitboxData.ignoreEffectDamage &&
                !IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile))
                {
                    defender.TakeEffectDamage(owner.reps, owner, GameManager.colors["yellow"]);
                }
                

                if(defender.hitboxData.parentProjectile.ownerSpell.brands[0] == Brand.Killeez && !defender.hitboxData.parentProjectile.ignoreBrand)
                {
                    //only grant resource on the first hit of a multihit per player
                    if(IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile))
                    {
                        break;
                    }

                    //grant the resource
                    owner.reps++;
                    owner.SpawnToast("+1 Rep", GameManager.colors["yellow"]);
                }
                
                break;
            default:
                break;
        }

        
    }
}
