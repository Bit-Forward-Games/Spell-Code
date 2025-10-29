using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SpeedBoost_Spell : SpellData
{
    Fixed baseSpeed;
    public Fixed speedMultiplier = Fixed.FromInt(2); // Speed multiplier when boost is active
    //int speedBoostDuration = 180; // Duration of the speed boost in frames
    //int speedBoostCounter = 0; 

    public SpeedBoost_Spell()
    {
        spellName = "SpeedBoost";
        cooldown = 180;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] { ProcCondition.OnUpdate };
        brands = new Brand[1] { Brand.Killeez };
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
                //speedBoostCounter = speedBoostDuration; // Reset counter
            }
        }
        /*else
        {
            owner.runSpeed = baseSpeed; // Reset to base speed
        }*/
        //if (speedBoostCounter > 0)
        //{
        //    speedBoostCounter--;
        //    if (speedBoostCounter == 0)
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
        if (owner.reps > 3)
        {
            activateFlag = true;
        }
        else { activateFlag = false; }
    }
}
