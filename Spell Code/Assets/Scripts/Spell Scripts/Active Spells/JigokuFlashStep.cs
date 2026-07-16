using UnityEngine;
using BestoNet.Types;
using System.Linq;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class JigokuFlashStep : SpellData
{
    short markedOpponentPID = -1;
    const int teleportOffset = 20;
    public JigokuFlashStep()
    {
        spellName = "Jigoku Flash Step";
        cooldown = 360;
        spellType = SpellType.Active;
        spellInput = 0b_0000_0000_0000_0000_0110_0001_0000_0100;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit, ProcCondition.OnCast, ProcCondition.OnUpdate };
        brands = new Brand[1] { Brand.DemonX };
        projectilePrefabs = new GameObject[2];
        description = "Longe-range kunai.\n On-hit, mark the opponent. Your next cast teleports you behind the marked opponent.";
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        markedOpponentPID = -1;
    }

 

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnHit:
                markedOpponentPID = defender.pID;
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), true, GameManager.Instance.GetPlayerByPID(markedOpponentPID).position);
                break;
            case ProcCondition.OnCast:
                if(markedOpponentPID >= 0)
                {
                    // Skip the teleport if the marked player disconnected (null), but still clear the
                    // mark + projectile so state stays consistent on every client.
                    PlayerController markedOpponent = GameManager.Instance.GetPlayerByPID(markedOpponentPID);
                    owner.TeleportToDestination(markedOpponent.position + new FixedVec2(Fixed.FromInt(markedOpponent.facingRight?-teleportOffset:teleportOffset), Fixed.FromInt(0)));
                    owner.facingRight = markedOpponent.facingRight;
                    markedOpponentPID = -1;
                    ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());

                }
                
                break;
            case ProcCondition.OnUpdate:
                //handle the marked vfx
                if (projectileInstances[1].activeSelf)
                {
                    projectileInstances[1].GetComponent<BaseProjectile>().position = GameManager.Instance.GetPlayerByPID(markedOpponentPID).position;
                }
                else
                {
                    markedOpponentPID = -1;
                }
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(markedOpponentPID);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        markedOpponentPID = br.ReadInt16();
    }
    
}
