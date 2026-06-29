using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AegisOfAthena : SpellData
{
    bool parryStored = false;
    public AegisOfAthena()
    {
        spellName = "Aegis Of Athena";
        cooldown = 120;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnParry, ProcCondition.OnCast, ProcCondition.OnHit, ProcCondition.OnCastEnd };
        brands = new Brand[1] { Brand.Killeez };
        projectilePrefabs = new GameObject[2];
        spawnOffsetX = 0;
        spawnOffsetY = 36;
        description = "Upon landing a sucessful parry, your next cast gains Perfect Armor and grants +1 Rep<sprite name=\"Reps\"> on hit while the perfect armor is active.";
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
        if (!parryStored)
        {
            ProjectileManager.Instance.DeleteProjectile(projectileInstances[0].GetComponent<BaseProjectile>());
        }

    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnParry:
                parryStored = true;
                break;
            case ProcCondition.OnCast:
                if(parryStored)
                {
                    if(cooldownCounter <= 0)
                    {
                        cooldownCounter = cooldown;
                        owner.superArmor = true;
                    }
                }
                break;
            case ProcCondition.OnHit:
                if(parryStored)
                {
                    //only grant resource on the first hit of a multihit per player
                    if(!IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile))
                    {
                        break;
                    }

                    //grant the resource
                    owner.reps++;
                    owner.SpawnToast("+1 Rep", GameManager.colors["yellow"]);
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
