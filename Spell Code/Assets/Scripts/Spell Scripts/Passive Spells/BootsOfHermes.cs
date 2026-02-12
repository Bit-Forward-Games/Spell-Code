using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BootsOfHermes : SpellData
{
    public int hermesJumps = 0;

    public BootsOfHermes()
    {
        spellName = "BootsOfHermes";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] { ProcCondition.OnUpdate };
        brands = new Brand[1] { Brand.Killeez };
        description = "Gain 1 Jump for every 3 \"Reps\" you have.";
    }

    public override void SpellUpdate()
    {
        if(hermesJumps > 0)
        {
            if (owner.vSpd < owner.jumpForce && owner.state == PlayerState.Jump && owner.input.ButtonStates[1] == ButtonState.Pressed)
            {
                owner.vSpd = owner.jumpForce;
                hermesJumps--;
            }
        }
    }

    public override void LoadSpell()
    {
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnUpdate:
                //OnUpdate proc: Check if grounded, if so, set extra jumps based on reps
                if (owner.isGrounded)
                {
                    hermesJumps = Mathf.FloorToInt(owner.reps / 3);
                }
                break;
            default:
                break;
        }
    }
}
