using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AuraFarmer : SpellData
{
    const int initDemonAuraTimestamp = 240;
    int demonAuraProcTimestamp = initDemonAuraTimestamp; //this is what logic frame the player has to be to gain the free demon aura
    public AuraFarmer()
    {
        spellName = "AuraFarmer";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnUpdate};
        brands = new Brand[1] { Brand.DemonX };
        description = "After 4 seconds of standing still, slowly gain \"Demon Aura\"";
    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnUpdate:
                if(owner.logicFrame >= demonAuraProcTimestamp && owner.demonAura < PlayerController.maxDemonAura)
                {
                    owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 1, 0, PlayerController.maxDemonAura);
                    demonAuraProcTimestamp += 4;
                    owner.demonAuraLifeSpanTimer = 360;
                }
                if(owner.demonAuraLifeSpanTimer < 355)
                {
                    demonAuraProcTimestamp = initDemonAuraTimestamp;
                }
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(demonAuraProcTimestamp);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        demonAuraProcTimestamp = br.ReadInt32();
    }
}
