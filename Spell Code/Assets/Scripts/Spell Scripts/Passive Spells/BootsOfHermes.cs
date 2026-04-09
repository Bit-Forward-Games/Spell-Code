using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BootsOfHermes : SpellData
{

    public BootsOfHermes()
    {
        spellName = "Boots Of Hermes";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] { ProcCondition.OnUpdate };
        brands = new Brand[1] { Brand.Killeez };
        description = "Gain 1 Jump for every 3 Reps<sprite name=\"Reps\"> you have.";
    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnUpdate:
                owner.maxJumpCount = (byte)(owner.charData.jumpCount + Mathf.FloorToInt(owner.reps / 3));
                break;
            default:
                break;
        }
    }

    
}
