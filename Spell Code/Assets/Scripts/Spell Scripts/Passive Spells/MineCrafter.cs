using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class MineCrafter : SpellData
{

    public MineCrafter()
    {
        spellName = "Mine Crafter";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] { ProcCondition.OnSlide };
        brands = new Brand[1] { Brand.VWave };
        description = "While in Flow State<sprite name=\"FlowState\">, slide crafts a mine.";

        projectilePrefabs = new GameObject[1];
        spawnOffsetX = 0;
        spawnOffsetY = 10;
    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        //OnCast proc: Check if in flow state, if so, spawn an Overclock Explosion
        switch(targetProcCon)
        {
            case ProcCondition.OnSlide:
                if (owner.flowState > 0)
                {
                    activateFlag = true;
                }
                else { activateFlag = false; }
                break;
            default:
                break;
        }
    }
}
