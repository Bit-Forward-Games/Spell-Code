using UnityEngine;

public class JumpBoost_Spell : SpellData
{
    float baseJumpForce;
    public float jumpMultiplier = 1.5f;
    //int jumpBoostDuration = 180; // Duration of the speed boost in frames
    //int jumpBoostCounter = 0;

    public JumpBoost_Spell()
    {
        spellName = "JumpBoost";
        cooldown = 180;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] { ProcCondition.OnUpdate };
        brands = new Brand[1] { Brand.VWave };
    }

    public override void SpellUpdate()
    {
        if (activateFlag)
        {
            activateFlag = false;
            Debug.Log("Jump Boost Activated!");
            if (owner.jumpForce == baseJumpForce)
            {
                owner.jumpForce *= jumpMultiplier;
                Debug.Log($"{owner.jumpForce}");
                //jumpBoostCounter = jumpBoostDuration; // Reset counter
            }
        }
        //if (jumpBoostCounter > 0)
        //{
        //    jumpBoostCounter--;
        //    if (jumpBoostCounter == 0)
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
        if (owner.flowState > 0)
        {
            activateFlag = true;
        }
        else { activateFlag = false; }
    }
}
