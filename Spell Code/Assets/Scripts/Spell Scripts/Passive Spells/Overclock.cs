using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class Overclock : SpellData
{
    Fixed baseSpeed;
    public Fixed speedMultiplier = Fixed.FromInt(2);
    //int OverclockDuration = 180; // Duration of the speed boost in frames
    //int OverclockCounter = 0; 

    public Overclock()
    {
        spellName = "Overclock";
        cooldown = 0;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] { ProcCondition.OnUpdate };
        brands = new Brand[1] { Brand.VWave };
        description = "While in Flow State, you overclock your movement, increasing your run speed.";
    }

    public override void SpellUpdate()
    {
        if (activateFlag)
        {
            //activateFlag = false;
            Debug.Log("Speed Boost Activated!");
            if (owner.runSpeed == baseSpeed)
            {
                owner.runSpeed *= speedMultiplier;
                //OverclockCounter = OverclockDuration; // Reset counter
            }
        }
        else
        {
            owner.runSpeed = baseSpeed; // Reset to base speed
        }
        //if (OverclockCounter > 0)
        //{
        //    OverclockCounter--;
        //    if (OverclockCounter == 0)
        //    {
        //        owner.runSpeed = baseSpeed; // Reset to base speed when counter ends
        //    }
        //}


    }

    public override void LoadSpell()
    {
        baseSpeed = owner.runSpeed; // Initialize base speed
    }

    public override void CheckCondition()
    {

        if (owner.flowState > 0)
        {
            activateFlag = true;
        }
        else { activateFlag = false; }
    }
}
