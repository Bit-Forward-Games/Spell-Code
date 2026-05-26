using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class KilleezPassive : SpellData
{
    public static ushort DemonAuraResetTime = 180;
    public KilleezPassive()
    {
        spellName = "Killeez Passive";
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
                if(owner.reps > 0)
                {
                    defender.TakeEffectDamage(owner.reps, owner, GameManager.colors["yellow"]);
                }
                

                //increase demon aura by 20 if its a Demon-X spellcode
                if(defender.hitboxData.parentProjectile.ownerSpell.brands[0] == Brand.Killeez)
                {
                    owner.reps++;
                    owner.SpawnToast("+1 Rep", GameManager.colors["yellow"]);
                }
                
                break;
            default:
                break;
        }

        
    }
}
