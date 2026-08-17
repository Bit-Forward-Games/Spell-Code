using UnityEngine;
using System.Linq;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class DemonAura : SpellData
{
    public static ushort DemonAuraResetTime = 180;
    public DemonAura()
    {
        spellName = "Demon Aura";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 1;
        priorityOverride = 3;
        spellType = SpellType.Universal;
        procConditions = new ProcCondition[3] { ProcCondition.OnHitSpell, ProcCondition.OnHit, ProcCondition.OnUpdate };
        description = $"Hit Demon-X Spellcodes to increase Demon Aura<sprite name=\"DemonAura\"> from ranks D to X.\nAfter {DemonAuraResetTime/60f} seconds of not dealing damage, lose Demon Aura<sprite name=\"DemonAura\">.\nSpellcodes deal increased damage based on your Demon Aura<sprite name=\"DemonAura\">.";

    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnHit:
                if (owner.demonAura > 0)
                {
                    owner.demonAuraLifeSpanTimer = DemonAuraResetTime; //refresh demon aura lifespan timer on hit
                    owner.demonAura = (ushort)(Mathf.Ceil(owner.demonAura/20f)*20);
                }
                break;
            case ProcCondition.OnHitSpell:
                if(owner.demonAura > 0 && 
                !defender.hitboxData.ignoreEffectDamage &&
                IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile))
                {
                    // deal extra damage based on demon aura
                    defender.TakeEffectDamage(owner.demonAura/5, owner, GameManager.colors["red"]);
                }
                


                //increase demon aura by 20 if its a Demon-X spellcode
                // ownerSpell null-guard: an Aegis-reflected projectile restored from a rollback has
                // ownerSpell == null (the reflector's spellList doesn't contain the original spell,
                // so it serializes as -1) a bare deref here crashed the sim on that hit. FlowState
                // already guards its copy of this check.
                if(defender.hitboxData.parentProjectile.ownerSpell != null && defender.hitboxData.parentProjectile.ownerSpell.brands.Contains(Brand.DemonX) && !defender.hitboxData.parentProjectile.ignoreBrand)
                {
                    //only grant resource on the first hit of a multihit per player
                    if(!IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile))
                    {
                        break;
                    }

                    //grant the resource
                    owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                    owner.demonAuraLifeSpanTimer = DemonAuraResetTime;
                    owner.SpawnToast("RANK UP!", GameManager.colors["red"]);
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
                if (owner.demonAura == 0)
                {
                    owner.demonAuraLifeSpanTimer = 0;
                }
            }
                break;
            default:
                break;
        }

        
    }
}
