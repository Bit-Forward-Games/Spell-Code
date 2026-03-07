using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AuraFarmer : SpellData
{
    int demonAuraProcTimestamp = 180; //this is what logic frame the player has to be to gain the free demon aura
    public AuraFarmer()
    {
        spellName = "AuraFarmer";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnUpdate};
        brands = new Brand[1] { Brand.DemonX };
        description = "After 6 seconds of standing still, slowly gain \"Demon Aura\"";
    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnUpdate:
                if(owner.logicFrame >= demonAuraProcTimestamp && owner.demonAura < PlayerController.maxDemonAura)
                {
                    owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                    demonAuraProcTimestamp +=120;
                    owner.demonAuraLifeSpanTimer = 360;
                    owner.SpawnToast("+20 DEMON AURA", Color.red);
                }
                break;
            default:
                break;
        }
    }
}
