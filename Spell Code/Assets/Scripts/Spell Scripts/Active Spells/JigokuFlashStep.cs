using UnityEngine;
using BestoNet.Types;
using System.Linq;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using YamlDotNet.Core;

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
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit, ProcCondition.OnCast, ProcCondition.OnUpdate, ProcCondition.OnKill };
        brands = new Brand[1] { Brand.DemonX };
        projectilePrefabs = new GameObject[2];
        description = "Long-range kunai. On-hit, mark the opponent. Your next cast teleports you behind the marked opponent.";
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
                // Use defender.position directly instead of looking the pID straight back up.
                // GetPlayerByPID returns null for pID 0 (playerNPCs is empty online) and for a slot
                // that is not connected, and this deref was unguarded,an NRE here is inside the
                // sim, which freezes the online match rather than just skipping a spawn. defender is
                // already known non-null on the line above, and resolves to the same object.
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), true, defender.position, true);
                break;
            case ProcCondition.OnCast:
                if(markedOpponentPID >= 0)
                {
                    // Skip the teleport if the marked player disconnected (null), but still clear the
                    // mark + projectile so state stays consistent on every client.
                    PlayerController markedOpponent = GameManager.Instance.GetPlayerByPID(markedOpponentPID);
                    if (markedOpponent != null)
                    {
                        owner.TeleportToDestination(markedOpponent.position + new FixedVec2(Fixed.FromInt(markedOpponent.facingRight?-teleportOffset:teleportOffset), Fixed.FromInt(0)));
                        owner.facingRight = markedOpponent.facingRight;
                    }
                    markedOpponentPID = -1;
                    ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());

                }
                
                break;
            case ProcCondition.OnUpdate:
                //handle the marked vfx
                if (projectileInstances[1].activeSelf)
                {
                    // Guard EVERY deref, the marked player can be gone entirely (disconnect in a 3/4P
                    // match -> GetPlayerByPID returns null; stale -1 pID would even index players[-2])
                    // an unguarded deref crashes the sim on every client.
                    PlayerController marked = markedOpponentPID >= 0 ? GameManager.Instance.GetPlayerByPID(markedOpponentPID) : null;
                    if (marked != null && marked.isAlive)
                    {
                        projectileInstances[1].GetComponent<BaseProjectile>().position = marked.position;
                    }
                    else
                    {
                        ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());
                        markedOpponentPID = -1;
                    }
                }
                else
                {
                    markedOpponentPID = -1;
                }
                break;
            case ProcCondition.OnKill:
                //handle the marked vfx
                if (projectileInstances[1].activeSelf)
                {
                    if (defender.pID == markedOpponentPID)
                    {
                        ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());
                        markedOpponentPID = -1;
                    }
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
