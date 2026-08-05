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
    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(vSpeedStored);
        bw.Write(storedFacingRight);
        bw.Write(storedVspeed.RawValue);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        vSpeedStored = br.ReadBoolean();
        storedFacingRight = br.ReadBoolean();
        storedVspeed = new Fixed(br.ReadInt32());
    }
}
