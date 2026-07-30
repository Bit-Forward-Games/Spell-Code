using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using System.Linq;

public class ComboDemon : SpellData
{
    private string lastUsedProjName  = "";
    public ComboDemon()
    {
        spellName = "Combo Demon";
        cooldown = 60;
        spellType = SpellType.Passive;
        priorityOverride = 1;
        procConditions = new ProcCondition[] { ProcCondition.OnHit };
        brands = new Brand[1] { Brand.DemonX };
        description = $"Hitting an attack which is different from your last attack grants 1 rank of Demon Aura<sprite name=\"DemonAura\">.";
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
            case ProcCondition.OnHit:
                if(lastUsedProjName != defender.hitboxData.parentProjectile.projName)
                {
                    if(lastUsedProjName == "")
                    {
                        lastUsedProjName = defender.hitboxData.parentProjectile.projName;
                        return;
                    }
                    
                    lastUsedProjName = defender.hitboxData.parentProjectile.projName;
                    //grant the resource
                    owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                    owner.demonAuraLifeSpanTimer = DemonAura.DemonAuraResetTime;
                    owner.SpawnToast("RANK UP!", GameManager.colors["red"]);
                }
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(lastUsedProjName);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        lastUsedProjName = br.ReadString();
    }

}
