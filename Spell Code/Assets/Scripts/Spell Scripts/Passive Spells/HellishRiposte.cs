using UnityEngine;
using BestoNet.Types;
using System.Linq;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class HellishRiposte : SpellData
{
    bool parryStored = false;
    public HellishRiposte()
    {
        spellName = "Hellish Riposte";
        cooldown = 120;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnParry, ProcCondition.OnHit, ProcCondition.OnCastEnd };
        brands = new Brand[1] { Brand.DemonX };
        projectilePrefabs = new GameObject[2];
        spawnOffsetX = 0;
        spawnOffsetY = 36;
        description = "Upon landing a sucessful parry, your next hit grants +20% Demon Aura<sprite name=\"DemonAura\"> on hit and deals extra Damage.";
    }
    public override void SpellUpdate()
    {
        //basic cooldown handling
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }
        
        if (!projectileInstances[0].activeSelf && parryStored)
        {
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX ), Fixed.FromInt(spawnOffsetY)));
        }
        if (projectileInstances[0].activeSelf && projectileInstances[0].GetComponent<BaseProjectile>().logicFrame == projectileInstances[0].GetComponent<BaseProjectile>().animFrames.frameLengths.Take(4).Sum()+1)
        {
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), projectileInstances[0].GetComponent<BaseProjectile>().facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
        }

    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnParry:
                parryStored = true;
                break;
            case ProcCondition.OnHit:
                if(parryStored)
                {
                    if(cooldownCounter <= 0)
                    {
                        cooldownCounter = cooldown;
                        defender.TakeEffectDamage(10,owner,GameManager.colors["red"]);
                        
                        //only grant resource on the first hit of a multihit per player
                        if(!IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile))
                        {
                            break;
                        }

                        //grant the resource
                        owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                        owner.SpawnToast("+20 DEMON AURA", GameManager.colors["red"]);
                    }
                }
                break;
            case ProcCondition.OnCastEnd:
                parryStored = false;
                break;
            default:
                break;
        }
    }
    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(parryStored);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        parryStored = br.ReadBoolean();
    }
    
}
