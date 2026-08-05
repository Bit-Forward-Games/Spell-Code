using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BrimestoneCycloneKick : SpellData
{
    private Fixed storedVspeed = Fixed.FromInt(0);
    private bool vSpeedStored = false;
    private bool storedFacingRight = true;
    private const int hSpeed = 6;
    public BrimestoneCycloneKick()
    {
        spellName = "Brimstone Cyclone Kick";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_1000_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnCast, ProcCondition.OnCodeweaveEnter, ProcCondition.OnUpdate };
        projectilePrefabs = new GameObject[1];
        spawnOffsetX = 0;
        spawnOffsetY = 0;
        description = "Lunging Cyclone kick.\nThis Spellcode follows your rising or falling momentum.";

    }

    // public override void SpellUpdate()
    // {
    //     if (projectileInstances.Count < 1) return;
    //     if (cooldownCounter > 0)
    //     {
    //         cooldownCounter--;
    //         return;
    //     }
    //     if (activateFlag)
    //     {
    //         // Reset the activate flag
    //         activateFlag = false;
    //         //owner.vSpd = Fixed.FromInt(2); // Launch the player upwards slightly
    //         owner.hSpd = owner.facingRight ? Fixed.FromInt(hSpeed) : Fixed.FromInt(-hSpeed); // Propel the player forward

    //         // Instantiate the projectile prefab at the player's position
    //         // Assuming you have a reference to the player GameObject
    //         if (owner != null && projectilePrefabs.Length > 0)
    //         {
    //             ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
    //         }
    //         cooldownCounter = owner.vibeCoding?(int)(cooldown+((spellInput & 0xFu)*30)):cooldown;
    //         //if(vibeCasted) owner.SpawnToast("VIBE CODED", GameManager.colors["grey"]);
    //         //vibeCasted = false;
    //     }

    // }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                if(!vSpeedStored)
                {
                    storedVspeed = owner.vSpd;
                    vSpeedStored = true;
                }
                storedFacingRight = owner.facingRight;
                break;
            case ProcCondition.OnCodeweaveEnter:
                storedVspeed = owner.vSpd;
                vSpeedStored = true;
                break;
            case ProcCondition.OnUpdate:
                if(owner.state != PlayerState.CodeWeave && owner.state != PlayerState.CodeRelease)
                {
                    vSpeedStored = false;
                    storedVspeed = Fixed.FromInt(0);
                }
                if (projectileInstances[0].activeSelf)
                {
                    owner.vSpd = storedVspeed;
                    owner.hSpd = storedFacingRight ? Fixed.FromInt(hSpeed) : Fixed.FromInt(-hSpeed); // Propel the player forward
                }

                break;
            default:
                break;
        }

        
    }
}
