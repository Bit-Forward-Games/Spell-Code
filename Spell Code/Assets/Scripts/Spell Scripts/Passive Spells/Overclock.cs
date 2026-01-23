using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class Overclock : SpellData
{

    public Overclock()
    {
        spellName = "Overclock";
        cooldown = 0;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] { ProcCondition.OnCast };
        brands = new Brand[1] { Brand.VWave };
        description = "While in Flow State, you overclock your Spell-Codes, Creating an Explosion on spell-cast.";
    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        //OnCast proc: Check if in flow state, if so, spawn an Overclock Explosion
        if (owner.flowState > 0)
        {
            activateFlag = true;
        }
        else { activateFlag = false; }
    }
}
