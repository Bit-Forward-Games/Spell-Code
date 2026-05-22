using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BootsOfHermes : SpellData
{

    public BootsOfHermes()
    {
        spellName = "Boots Of Hermes";
        cooldown = 30;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnUpdate, ProcCondition.OnJump };
        brands = new Brand[1] { Brand.Killeez };
        projectilePrefabs = new GameObject[1];
        description = "Gain 1 Jump for every 3 Reps<sprite name=\"Reps\"> you have.\nArial jumps now explode";
    }
    public override void SpellUpdate()
    {
        //basic cooldown handling
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }

    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnUpdate:
                owner.maxJumpCount = (byte)(owner.charData.jumpCount + Mathf.FloorToInt(owner.reps / 3));
                break;
            case ProcCondition.OnJump:
                if(owner.jumpCount < owner.maxJumpCount - 2)
                {
                    if(cooldownCounter <= 0)
                    {
                        cooldownCounter = cooldown;
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(0), Fixed.FromInt(0)));
                    }
                }
                break;
            default:
                break;
        }
    }

    
}
