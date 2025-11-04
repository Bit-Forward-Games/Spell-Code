using UnityEngine;
using BestoNet.Types;

public class BootsOfHermes : SpellData
{
    float baseJumpForce;
    public float jumpMultiplier = 1.5f;
    //int BootsOfHermesDuration = 180; // Duration of the speed boost in frames
    //int BootsOfHermesCounter = 0;

    public BootsOfHermes()
    {
        spellName = "BootsOfHermes";
        cooldown = 0;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] { ProcCondition.OnUpdate };
        brands = new Brand[1] { Brand.Killeez };
    }

    public override void SpellUpdate()
    {
        if (activateFlag)
        {
            //activateFlag = false;
            Debug.Log("Jump Boost Activated!");
            if (owner.jumpForce == baseJumpForce)
            {
                owner.jumpForce *= jumpMultiplier;
                Debug.Log($"{owner.jumpForce}");
                //BootsOfHermesCounter = BootsOfHermesDuration; // Reset counter
            }
        }
        else
        {
            if (owner.jumpForce != baseJumpForce)
            {
                owner.jumpForce = baseJumpForce; // Reset to base jump force
                Debug.Log("Jump Boost Deactivated, reset jump force.");
            }
        }
        //if (BootsOfHermesCounter > 0)
        //{
        //    BootsOfHermesCounter--;
        //    if (BootsOfHermesCounter == 0)
        //    {
        //        owner.jumpForce = baseJumpForce; // Reset to base jump foce when counter ends
        //    }
        //}
    }

    public override void LoadSpell()
    {
        baseJumpForce = owner.jumpForce; // Initialize base jump force
    }

    public override void CheckCondition()
    {

        if (owner.reps >= 3)
        {
            activateFlag = true;
        }
        else { activateFlag = false; }
    }
}
